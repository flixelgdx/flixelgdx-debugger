// MIT License
//
// Copyright (c) 2026 stringdotjar
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools that read and use any panel the game built, built-in or custom, the way a person would in
/// the debugger app.
/// </summary>
/// <remarks>
/// <para>
/// Every panel is a tree of widgets that the game describes. These tools read that tree and send
/// the same events the app sends when the person clicks, types, or picks something, so an agent
/// can use a panel a game developer wrote without any tool written for it.
/// </para>
/// <para>
/// A game only builds a panel while someone holds it open. Reading a panel holds it open for
/// agents until <c>close_panel</c> is called or the MCP server stops.
/// </para>
/// </remarks>
[McpServerToolType]
internal sealed class PanelTools(ToolContext ctx)
{
  /// <summary>The owner name agents use when they hold a panel open.</summary>
  public const string Owner = "mcp";

  private const string SessionHelp = "Game id from list_games; optional with one game.";
  private const string PanelHelp = "The panel id from list_panels, for example save.";
  private const string WidgetHelp =
      "The widget's name (from read_panel) or its numeric id. Names are stable between runs.";

  private static readonly TimeSpan _treeTimeout = TimeSpan.FromSeconds(2);
  private static readonly TimeSpan _answerTimeout = TimeSpan.FromMilliseconds(1200);

  [McpServerTool(Name = "list_panels", ReadOnly = true)]
  [Description(
      "Lists the debug panels the game built, built-in and custom: id, title, and whether the " +
      "game is sending it right now. Use read_panel to see a panel's widgets.")]
  public string ListPanels([Description(SessionHelp)] string? session = null)
  {
    var game = ctx.Game(session);
    return Json.Object(w =>
    {
      w.WriteStartArray("panels");
      foreach (var panel in game.Panels)
      {
        w.WriteStartObject();
        w.WriteString("id", panel.Id);
        w.WriteString("title", panel.Title);
        w.WriteString("dock", panel.Dock.ToString().ToLowerInvariant());
        w.WriteBoolean("open", panel.HasTree);
        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "read_panel", ReadOnly = true)]
  [Description(
      "Reads a panel as a tree of widgets (labels, buttons, checkboxes, text boxes, tables, " +
      "trees, property grids, graphs, and more) with their current values. Each widget has an " +
      "id and usually a name; pass either to the other panel tools. Long lists are cut to " +
      "'maxItems' entries (tables keep the newest rows).")]
  public async Task<string> ReadPanel(
      [Description(PanelHelp)] string panelId,
      [Description(SessionHelp)] string? session = null,
      [Description("The most list entries per widget (1 to 500).")] int maxItems = 50,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var root = await OpenAsync(game, panelId, cancellationToken).ConfigureAwait(false);
    maxItems = Math.Clamp(maxItems, 1, 500);
    return Json.Object(w =>
    {
      w.WriteString("panel", panelId);
      w.WritePropertyName("root");
      WriteWidget(w, root, maxItems);
    });
  }

  [McpServerTool(Name = "close_panel")]
  [Description(
      "Lets go of a panel read_panel opened, so the game stops building it for agents. The " +
      "panel stays open in the app if the person has it on screen.")]
  public async Task<string> ClosePanel(
      [Description(PanelHelp)] string panelId,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    await game.SetPanelOpenAsync(panelId, Owner, false, cancellationToken).ConfigureAwait(false);
    return "closed";
  }

  [McpServerTool(Name = "panel_click")]
  [Description(
      "Clicks a button of a panel. Buttons that ask the person for confirmation in the app are " +
      "clicked without asking. Returns what the game changed in the panel.")]
  public Task<string> Click(
      [Description(PanelHelp)] string panelId,
      [Description(WidgetHelp)] string widget,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default) =>
      SendAsync(session, panelId, widget, [WidgetType.Button], WidgetEvent.Click,
          static (_, _) => [], cancellationToken);

  [McpServerTool(Name = "panel_set")]
  [Description(
      "Sets the value of a checkbox, toggle, dropdown, text box, number field, slider, color " +
      "picker, tab strip, expander, or split, like a person changing it. Booleans are " +
      "true/false, a dropdown takes an item's text or index, a color takes #AARRGGBB or " +
      "#RRGGBB. Returns what the game changed in the panel.")]
  public Task<string> Set(
      [Description(PanelHelp)] string panelId,
      [Description(WidgetHelp)] string widget,
      [Description("The new value as text.")] string value,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default) =>
      SendAsync(session, panelId, widget,
          [WidgetType.Checkbox, WidgetType.Toggle, WidgetType.Dropdown, WidgetType.TextBox,
              WidgetType.Number, WidgetType.Slider, WidgetType.Color, WidgetType.Tabs,
              WidgetType.Expander, WidgetType.Split],
          WidgetEvent.Change, (node, _) => [ParseChange(node, value)], cancellationToken);

  [McpServerTool(Name = "panel_submit")]
  [Description(
      "Types a line into a text box and presses Enter, for example a console command. Returns " +
      "what the game changed in the panel.")]
  public Task<string> Submit(
      [Description(PanelHelp)] string panelId,
      [Description(WidgetHelp)] string widget,
      [Description("The text to submit.")] string text,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default) =>
      SendAsync(session, panelId, widget, [WidgetType.TextBox], WidgetEvent.Submit,
          (_, _) => [UiValue.FromString(text)], cancellationToken);

  [McpServerTool(Name = "panel_row")]
  [Description(
      "Acts on a row of a table or a node of a tree: 'select' it, 'activate' it (like a " +
      "double click), or pick a 'menu' item (by its text or index; see the widget's menu in " +
      "read_panel). Use 'clear' to clear the selection. Row ids come from read_panel.")]
  public Task<string> Row(
      [Description(PanelHelp)] string panelId,
      [Description(WidgetHelp)] string widget,
      [Description("select, activate, menu, or clear.")] string action,
      [Description("The row or node id (not needed for clear).")] long rowId = -1,
      [Description("The menu item's text or index, for the menu action.")] string? menuItem = null,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var kind = action.Trim().ToLowerInvariant() switch
    {
      "select" or "clear" => WidgetEvent.Select,
      "activate" => WidgetEvent.Activate,
      "menu" => WidgetEvent.Menu,
      _ => throw new McpException("The action must be select, activate, menu, or clear."),
    };
    var clear = action.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase);
    return SendAsync(session, panelId, widget, [WidgetType.Table, WidgetType.Tree], kind,
        (node, _) =>
        {
          if (clear)
          {
            return [UiValue.Null];
          }

          var id = UiValue.FromLong(rowId);
          return kind == WidgetEvent.Menu ? [id, UiValue.FromInt(MenuIndex(node, menuItem))] : [id];
        },
        cancellationToken);
  }

  [McpServerTool(Name = "panel_edit")]
  [Description(
      "Edits one editable value of a property grid, like typing into an inspector. 'section' " +
      "and 'row' are the titles from read_panel (or their indices). The value is parsed by " +
      "the row's kind. Returns what the game changed in the panel.")]
  public Task<string> Edit(
      [Description(PanelHelp)] string panelId,
      [Description(WidgetHelp)] string widget,
      [Description("The section title or index.")] string section,
      [Description("The row name or index.")] string row,
      [Description("The new value as text.")] string value,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default) =>
      SendAsync(session, panelId, widget, [WidgetType.Properties], WidgetEvent.Edit,
          (node, _) => ParseEdit(node, section, row, value), cancellationToken);

  [McpServerTool(Name = "panel_event")]
  [Description(
      "Sends any widget event, for gestures the other panel tools do not cover, such as " +
      "picking, dragging, panning, or zooming a canvas. 'event' is one of click, change, " +
      "submit, select, activate, menu, edit, pick, drag, pan, zoom. Each argument is typed " +
      "with a prefix: b:true, i:3, l:42, f:1.5, d:0.25, s:text, or null. For example a canvas " +
      "pick at 100,50 is event 'pick' with args ['f:100', 'f:50'].")]
  public Task<string> Event(
      [Description(PanelHelp)] string panelId,
      [Description(WidgetHelp)] string widget,
      [Description("The event name.")] string @event,
      [Description("The typed arguments.")] string[]? args = null,
      [Description(SessionHelp)] string? session = null,
      CancellationToken cancellationToken = default)
  {
    if (!Enum.TryParse<WidgetEvent>(@event.Trim(), true, out var kind) ||
        !Enum.IsDefined(kind))
    {
      throw new McpException($"Unknown event '{@event}'.");
    }

    var values = (args ?? []).Select(ParseTyped).ToArray();
    return SendAsync(session, panelId, widget, [], kind, (_, _) => values, cancellationToken);
  }

  private static async Task<WidgetNode> OpenAsync(
      GameSession game, string panelId, CancellationToken cancellationToken)
  {
    if (!game.TryGetPanel(panelId, out var panel))
    {
      throw new McpException(
          $"The game has no panel '{panelId}'. Panels: " +
          string.Join(", ", game.Panels.Select(p => p.Id)));
    }

    if (panel.Snapshot() is { } ready)
    {
      await game.SetPanelOpenAsync(panelId, Owner, true, cancellationToken).ConfigureAwait(false);
      return ready;
    }

    using var watch = PanelWatch.Start(game, panelId);
    if (!await game.SetPanelOpenAsync(panelId, Owner, true, cancellationToken)
        .ConfigureAwait(false))
    {
      throw new McpException($"Could not open the panel '{panelId}': the connection is closed.");
    }

    var end = DateTime.UtcNow + _treeTimeout;
    while (DateTime.UtcNow < end)
    {
      await watch.WaitAsync(end - DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
      if (panel.Snapshot() is { } tree)
      {
        return tree;
      }
    }

    throw new McpException($"The game did not send the panel '{panelId}' in time.");
  }

  private async Task<string> SendAsync(
      string? session,
      string panelId,
      string widget,
      WidgetType[] allowed,
      WidgetEvent kind,
      Func<WidgetNode, WidgetNode, UiValue[]> args,
      CancellationToken cancellationToken)
  {
    var game = ctx.Game(session);
    var root = await OpenAsync(game, panelId, cancellationToken).ConfigureAwait(false);
    var node = Find(root, widget) ?? throw new McpException(
        $"The panel '{panelId}' has no widget '{widget}'. Named widgets: " +
        string.Join(", ", root.Walk().Where(n => n.Name.Length > 0).Select(n => n.Name)));
    if (allowed.Length > 0 && Array.IndexOf(allowed, node.Type) < 0)
    {
      throw new McpException(
          $"The widget '{widget}' is a {TypeName(node.Type)}, which does not take this action.");
    }

    if (!(node.Get(WidgetProp.Enabled)?.AsBool(true) ?? true))
    {
      throw new McpException($"The widget '{widget}' is disabled right now.");
    }

    var values = args(node, root);
    using var watch = PanelWatch.Start(game, panelId);
    if (!await game.SendPanelEventAsync(panelId, node.Id, kind, values, cancellationToken)
        .ConfigureAwait(false))
    {
      throw new McpException($"Could not send to the panel '{panelId}': the connection is closed.");
    }

    var (ops, newTree) = await watch.WaitAsync(_answerTimeout, cancellationToken)
        .ConfigureAwait(false);
    game.TryGetPanel(panelId, out var state);
    var names = (state?.Snapshot() ?? root).Walk().ToDictionary(n => n.Id, n => n);
    return Json.Object(w =>
    {
      w.WriteString("sent", kind.ToString().ToLowerInvariant());
      w.WriteBoolean("newTree", newTree);
      w.WriteStartArray("changed");
      foreach (var op in ops)
      {
        w.WriteStartObject();
        w.WriteNumber("widget", op.Widget);
        if (names.TryGetValue(op.Widget, out var changed) && changed.Name.Length > 0)
        {
          w.WriteString("name", changed.Name);
        }

        w.WriteString("property", op.Key.ToString().ToLowerInvariant());
        w.WritePropertyName("value");
        var current = state?.Get(op.Widget, op.Key) ?? op.Value;
        WriteValue(w, current, 20);
        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  private static WidgetNode? Find(WidgetNode root, string widget)
  {
    var text = widget.Trim();
    foreach (var node in root.Walk())
    {
      if (string.Equals(node.Name, text, StringComparison.Ordinal))
      {
        return node;
      }
    }

    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
    {
      foreach (var node in root.Walk())
      {
        if (node.Id == id)
        {
          return node;
        }
      }
    }

    return null;
  }

  private static UiValue ParseChange(WidgetNode node, string text)
  {
    var trimmed = text.Trim();
    switch (node.Type)
    {
      case WidgetType.Checkbox or WidgetType.Toggle or WidgetType.Expander:
        return bool.TryParse(trimmed, out var flag)
            ? UiValue.FromBool(flag)
            : throw new McpException("This widget takes true or false.");
      case WidgetType.TextBox:
        return UiValue.FromString(text);
      case WidgetType.Dropdown or WidgetType.Tabs:
        var items = node.Get(WidgetProp.Items)?.Items ?? [];
        for (var i = 0; i < items.Count; i++)
        {
          if (string.Equals(items[i].ToDisplay(), trimmed, StringComparison.OrdinalIgnoreCase))
          {
            return UiValue.FromInt(i);
          }
        }

        var culture = CultureInfo.InvariantCulture;
        return int.TryParse(trimmed, NumberStyles.Integer, culture, out var index)
            && index >= 0 && index < items.Count
            ? UiValue.FromInt(index)
            : throw new McpException(
                "Pick one of: " + string.Join(", ", items.Select(i => i.ToDisplay())));
      case WidgetType.Color:
        var hex = trimmed.TrimStart('#');
        if (hex.Length == 6)
        {
          hex = "FF" + hex;
        }

        return hex.Length == 8 &&
            uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb)
            ? UiValue.FromInt(unchecked((int)argb))
            : throw new McpException("A color is #AARRGGBB or #RRGGBB.");
      default:
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? UiValue.FromDouble(n)
            : throw new McpException("This widget takes a number.");
    }
  }

  private static UiValue[] ParseEdit(WidgetNode node, string section, string row, string text)
  {
    var sections = node.Get(WidgetProp.Sections)?.Items ?? [];
    var s = IndexOf(sections, section, static v => v[0].AsString());
    if (s < 0)
    {
      throw new McpException(
          $"No section '{section}'. Sections: " +
          string.Join(", ", sections.Select(v => v[0].AsString())));
    }

    var rows = sections[s][2].Items;
    var r = IndexOf(rows, row, static v => v[0].AsString());
    if (r < 0)
    {
      throw new McpException(
          $"No row '{row}' in '{sections[s][0].AsString()}'. Rows: " +
          string.Join(", ", rows.Select(v => v[0].AsString())));
    }

    var current = rows[r];
    if (!current[2].AsBool() || current[1].Kind == ValueKind.Error)
    {
      throw new McpException($"The row '{current[0].AsString()}' is read-only.");
    }

    var kind = current[1].Kind;
    if (!ValueFormat.TryParse(kind, text, out var parsed))
    {
      throw new McpException(
          $"Cannot use '{text}' for '{current[0].AsString()}', which is a " +
          $"{ToolContext.KindName(kind)} value.");
    }

    return [UiValue.FromInt(s), UiValue.FromInt(r), UiValue.FromProperty(parsed)];
  }

  private static int IndexOf(
      IReadOnlyList<UiValue> list, string wanted, Func<UiValue, string> name)
  {
    for (var i = 0; i < list.Count; i++)
    {
      if (string.Equals(name(list[i]), wanted.Trim(), StringComparison.OrdinalIgnoreCase))
      {
        return i;
      }
    }

    return int.TryParse(wanted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
        && index >= 0 && index < list.Count
        ? index
        : -1;
  }

  private static int MenuIndex(WidgetNode node, string? item)
  {
    var menu = node.Get(WidgetProp.Menu)?.Items ?? [];
    var index = item is null ? -1 : IndexOf(menu, item, static v => v.AsString());
    return index >= 0
        ? index
        : throw new McpException(
            "Pick a menu item: " + string.Join(", ", menu.Select(v => v.AsString())));
  }

  private static UiValue ParseTyped(string arg)
  {
    var text = arg.Trim();
    if (text.Equals("null", StringComparison.OrdinalIgnoreCase))
    {
      return UiValue.Null;
    }

    var colon = text.IndexOf(':', StringComparison.Ordinal);
    if (colon != 1)
    {
      throw new McpException($"The argument '{arg}' needs a type prefix such as f: or s:.");
    }

    var body = text[2..];
    var culture = CultureInfo.InvariantCulture;
    return text[0] switch
    {
      'b' when bool.TryParse(body, out var b) => UiValue.FromBool(b),
      'i' when int.TryParse(body, NumberStyles.Integer, culture, out var i) => UiValue.FromInt(i),
      'l' when long.TryParse(body, NumberStyles.Integer, culture, out var l) => UiValue.FromLong(l),
      'f' when float.TryParse(body, NumberStyles.Float, culture, out var f) => UiValue.FromFloat(f),
      'd' when double.TryParse(body, NumberStyles.Float, culture, out var d) =>
          UiValue.FromDouble(d),
      's' => UiValue.FromString(arg.TrimStart()[2..]),
      _ => throw new McpException($"Cannot read the argument '{arg}'."),
    };
  }

  private static string TypeName(WidgetType type) => type switch
  {
    WidgetType.TextBox => "text_box",
    _ => type.ToString().ToLowerInvariant(),
  };

  // Writes a widget with the properties that matter to an agent. Purely visual ones (sizes,
  // spacing, style bits) are left out to save tokens.
  private static void WriteWidget(Utf8JsonWriter w, WidgetNode node, int maxItems)
  {
    w.WriteStartObject();
    w.WriteNumber("id", node.Id);
    w.WriteString("type", TypeName(node.Type));
    if (node.Name.Length > 0)
    {
      w.WriteString("name", node.Name);
    }

    foreach (var (key, value) in node.Props.OrderBy(p => p.Key))
    {
      switch (key)
      {
        case WidgetProp.Width or WidgetProp.Height or WidgetProp.Grow or WidgetProp.Style
            or WidgetProp.Spacing or WidgetProp.Horizontal or WidgetProp.Smooth
            or WidgetProp.Scale or WidgetProp.Wrap or WidgetProp.Interact:
          continue;
        case WidgetProp.Image:
          w.WriteBoolean("hasImage", value.Kind == ValueKind.Bytes);
          continue;
        case WidgetProp.Items when node.Type == WidgetType.Table:
          WriteRows(w, value.Items, maxItems);
          continue;
        case WidgetProp.Items when node.Type == WidgetType.Tree:
          WriteNodes(w, value.Items, maxItems);
          continue;
        case WidgetProp.Sections:
          WriteSections(w, value.Items);
          continue;
        case WidgetProp.Samples:
          w.WriteNumber("sampleCount", value.Count);
          if (value.Count > 0)
          {
            w.WritePropertyName("latest");
            WriteValue(w, value[value.Count - 1], maxItems);
          }

          continue;
        default:
          w.WritePropertyName(key.ToString().ToLowerInvariant());
          WriteValue(w, value, maxItems);
          continue;
      }
    }

    if (node.Children.Count > 0)
    {
      w.WriteStartArray("children");
      foreach (var child in node.Children)
      {
        WriteWidget(w, child, maxItems);
      }

      w.WriteEndArray();
    }

    w.WriteEndObject();
  }

  // Tables keep the newest rows, because that is what a log or a list of events cares about.
  private static void WriteRows(Utf8JsonWriter w, IReadOnlyList<UiValue> rows, int maxItems)
  {
    w.WriteNumber("rowCount", rows.Count);
    w.WriteStartArray("rows");
    for (var i = Math.Max(0, rows.Count - maxItems); i < rows.Count; i++)
    {
      var row = rows[i];
      w.WriteStartObject();
      w.WriteNumber("id", row[0].AsLong(-1));
      w.WriteStartArray("cells");
      foreach (var cell in row[2].Items)
      {
        w.WriteStringValue(cell.Kind == ValueKind.List ? cell[0].ToDisplay() : cell.ToDisplay());
      }

      w.WriteEndArray();
      if (row[3].AsString().Length > 0)
      {
        w.WriteString("detail", row[3].AsString());
      }

      w.WriteEndObject();
    }

    w.WriteEndArray();
  }

  private static void WriteNodes(Utf8JsonWriter w, IReadOnlyList<UiValue> nodes, int maxItems)
  {
    w.WriteNumber("nodeCount", nodes.Count);
    w.WriteStartArray("nodes");
    for (var i = 0; i < Math.Min(nodes.Count, maxItems); i++)
    {
      var node = nodes[i];
      w.WriteStartObject();
      w.WriteNumber("id", node[0].AsLong(-1));
      w.WriteNumber("parent", node[1].AsLong(-1));
      w.WriteString("text", node[2].AsString());
      if (node[3].AsString().Length > 0)
      {
        w.WriteString("detail", node[3].AsString());
      }

      w.WriteEndObject();
    }

    w.WriteEndArray();
  }

  private static void WriteSections(Utf8JsonWriter w, IReadOnlyList<UiValue> sections)
  {
    w.WriteStartArray("sections");
    foreach (var section in sections)
    {
      w.WriteStartObject();
      w.WriteString("title", section[0].AsString());
      w.WriteStartArray("rows");
      foreach (var row in section[2].Items)
      {
        w.WriteStartObject();
        w.WriteString("name", row[0].AsString());
        w.WriteString("value", row[1].ToDisplay());
        w.WriteString("kind", ToolContext.KindName(row[1].Kind));
        w.WriteBoolean("editable", row[2].AsBool() && row[1].Kind != ValueKind.Error);
        w.WriteEndObject();
      }

      w.WriteEndArray();
      w.WriteEndObject();
    }

    w.WriteEndArray();
  }

  private static void WriteValue(Utf8JsonWriter w, UiValue value, int maxItems)
  {
    switch (value.Kind)
    {
      case ValueKind.Null:
        w.WriteNullValue();
        break;
      case ValueKind.Bool:
        w.WriteBooleanValue(value.AsBool());
        break;
      case ValueKind.Int or ValueKind.Long:
        w.WriteNumberValue(value.AsLong());
        break;
      case ValueKind.Float or ValueKind.Double:
        var number = value.AsDouble();
        if (double.IsFinite(number))
        {
          w.WriteNumberValue(number);
        }
        else
        {
          w.WriteStringValue(number.ToString(CultureInfo.InvariantCulture));
        }

        break;
      case ValueKind.List:
        w.WriteStartArray();
        for (var i = 0; i < Math.Min(value.Count, maxItems); i++)
        {
          WriteValue(w, value[i], maxItems);
        }

        w.WriteEndArray();
        break;
      case ValueKind.Bytes:
        w.WriteStringValue($"<{value.AsBytes()?.Length ?? 0} bytes>");
        break;
      default:
        w.WriteStringValue(value.ToDisplay());
        break;
    }
  }
}
