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
using System.Text.Json;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools that walk the scene tree and read or edit one object's properties.
/// </summary>
/// <remarks>
/// The inspector's selection lives in the game, so <c>inspect</c> and <c>set_property</c> change
/// the object the person sees selected in the Inspector panel too.
/// </remarks>
[McpServerToolType]
internal sealed class SceneTools(ToolContext ctx)
{
  private static readonly TimeSpan _selectTimeout = TimeSpan.FromSeconds(2);

  [McpServerTool(Name = "get_scene", ReadOnly = true)]
  [Description(
      "Lists the objects of the game's scene tree: state, groups, sprites, and UI widgets. Each " +
      "entry has a scene 'path' (for example state.3.0), a numeric 'id' (stable, preferred), " +
      "its 'type', and how many children it has. Use 'root' and 'depth' to look inside a " +
      "branch.")]
  public async Task<string> GetScene(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      [Description(
          "Start at this object: a numeric id or a scene path (a number or a string). " +
          "Default: all roots.")]
      JsonElement? root = null,
      [Description("How many levels below the start to list (0 to 20).")] int depth = 3,
      [Description("The most entries to return (1 to 1000).")] int limit = 200,
      [Description("Ask the game for a fresh snapshot first.")] bool refresh = false,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    if (refresh)
    {
      await ToolContext.ActAsync(
          game,
          BuiltInFeeds.Scene,
          () => game.RequestSceneTreeAsync(cancellationToken),
          static p => p is ScenePayload,
          cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    depth = Math.Clamp(depth, 0, 20);
    limit = Math.Clamp(limit, 1, 1000);
    var tree = game.Scene;
    var paths = SceneAddress.BuildPaths(tree);
    var starts = new List<SceneNode>();
    var rootText = root is { ValueKind: not JsonValueKind.Null } given
        ? ToolArgs.Text(given, "root")
        : null;
    if (string.IsNullOrWhiteSpace(rootText))
    {
      starts.AddRange(tree.Roots);
    }
    else if (SceneAddress.TryResolveId(tree, rootText, out var id) &&
        tree.TryGetNode(id, out var node))
    {
      starts.Add(node);
    }
    else
    {
      throw new McpException(
          $"No object matches '{rootText}' in the scene tree. Call get_scene without 'root' to " +
          "see the roots, or pass refresh=true if the scene just changed.");
    }

    var shown = 0;
    var truncated = false;
    var stack = new Stack<(SceneNode Node, int Level)>();
    for (var i = starts.Count - 1; i >= 0; i--)
    {
      stack.Push((starts[i], 0));
    }

    return Json.Object(writer =>
    {
      writer.WriteNumber("total", tree.Nodes.Count);
      writer.WriteStartArray("nodes");
      while (stack.Count > 0)
      {
        var (node, level) = stack.Pop();
        if (shown == limit)
        {
          truncated = true;
          break;
        }

        var children = tree.GetChildren(node.Id);
        writer.WriteStartObject();
        writer.WriteString("path", paths.GetValueOrDefault(node.Id, node.Label));
        writer.WriteNumber("id", node.Id);
        writer.WriteString("type", node.Type);
        writer.WriteNumber("children", children.Count);
        if (node.TrackedRoot)
        {
          writer.WriteBoolean("tracked", true);
        }

        writer.WriteEndObject();
        shown++;
        if (level < depth)
        {
          for (var i = children.Count - 1; i >= 0; i--)
          {
            stack.Push((children[i], level + 1));
          }
        }
      }

      writer.WriteEndArray();
      writer.WriteBoolean("truncated", truncated);
    });
  }

  [McpServerTool(Name = "inspect", ReadOnly = true)]
  [Description(
      "Selects one object in the game's inspector and returns its properties, grouped by " +
      "section (the object's class and its base classes). Each property has a name, value, " +
      "kind, and whether it is editable. This also changes the selection the person sees in " +
      "the Inspector panel.")]
  public async Task<string> Inspect(
      [Description(
          "The object: a numeric id (preferred) or a scene path like state.3.0. A number or " +
          "a string.")]
      JsonElement target,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var state = await SelectAsync(game, ToolArgs.Text(target, "target"), cancellationToken)
        .ConfigureAwait(false);
    return Json.Object(w =>
    {
      w.WriteNumber("id", state.SelectedId);
      w.WriteString("path", state.Path);
      w.WriteStartArray("sections");
      foreach (var section in state.Sections)
      {
        w.WriteStartObject();
        w.WriteString("type", section.TypeName);
        w.WriteString("name", section.DisplayName);
        w.WriteStartArray("properties");
        foreach (var row in section.Properties)
        {
          ToolContext.WriteRow(w, row);
        }

        w.WriteEndArray();
        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "set_property")]
  [Description(
      "Edits one property of a game object, like typing into the Inspector panel, and returns " +
      "the value the game reports afterwards. Use inspect first to see property names and " +
      "which are editable. The value is parsed by the property's kind (bool: true or false; " +
      "numbers: plain digits; string: any text). This also changes the person's inspector " +
      "selection.")]
  public async Task<string> SetProperty(
      [Description(
          "The object: a numeric id (preferred) or a scene path like state.3.0. A number or " +
          "a string.")]
      JsonElement target,
      [Description("The property name, for example x.")] string property,
      [Description("The new value as text.")] string value,
      [Description(
          "The section (class name) the property is in. Only needed when several sections " +
          "have a property with that name.")]
      string? section = null,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var state = await SelectAsync(game, ToolArgs.Text(target, "target"), cancellationToken)
        .ConfigureAwait(false);
    var (sectionIndex, rowIndex) = FindRow(state, section, property);
    var row = state.Sections[sectionIndex].Properties[rowIndex];
    if (!row.Editable)
    {
      throw new McpException($"The property '{row.Name}' is read-only.");
    }

    if (!ValueFormat.TryParse(row.Value.Kind, value, out var parsed))
    {
      throw new McpException(
          $"Cannot use '{value}' for '{row.Name}', which is a " +
          $"{ToolContext.KindName(row.Value.Kind)} property.");
    }

    var id = state.SelectedId;
    var expected = ValueFormat.EditText(parsed);
    var after = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Inspector,
        () => game.SetPropertyAsync(
            id, (ushort)sectionIndex, (ushort)rowIndex, parsed, cancellationToken),
        p => p is InspectorStatePayload s && s.SelectedId == id &&
            Matches(s, sectionIndex, rowIndex, expected),
        cancellationToken: cancellationToken).ConfigureAwait(false) as InspectorStatePayload;

    // When the game did not report the new value in time, fall back to the latest state it sent.
    var latest = after ?? game.Inspector;
    var current = latest is not null && latest.SelectedId == id
        ? ValueFormat.EditText(latest.Sections[sectionIndex].Properties[rowIndex].Value)
        : ValueFormat.EditText(row.Value);
    return Json.Object(w =>
    {
      w.WriteNumber("id", id);
      w.WriteString("section", state.Sections[sectionIndex].TypeName);
      w.WriteString("property", row.Name);
      w.WriteString("before", ValueFormat.EditText(row.Value));
      w.WriteString("after", current);
      w.WriteBoolean("confirmed", after is not null);
    });
  }

  private static bool Matches(InspectorStatePayload state, int section, int row, string expected) =>
      section < state.Sections.Count && row < state.Sections[section].Properties.Count &&
      ValueFormat.EditText(state.Sections[section].Properties[row].Value) == expected;

  private static (int Section, int Row) FindRow(
      InspectorStatePayload state, string? section, string property)
  {
    var hits = new List<(int Section, int Row)>();
    for (var s = 0; s < state.Sections.Count; s++)
    {
      var candidate = state.Sections[s];
      if (!string.IsNullOrWhiteSpace(section) &&
          !string.Equals(candidate.TypeName, section, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(candidate.DisplayName, section, StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      for (var r = 0; r < candidate.Properties.Count; r++)
      {
        if (string.Equals(
                candidate.Properties[r].Name, property, StringComparison.OrdinalIgnoreCase))
        {
          hits.Add((s, r));
        }
      }
    }

    if (hits.Count == 1)
    {
      return hits[0];
    }

    if (hits.Count > 1)
    {
      throw new McpException(
          $"Several sections have a property '{property}'. Pass 'section' as one of: " +
          string.Join(", ", hits.Select(h => state.Sections[h.Section].TypeName)));
    }

    var names = state.Sections
        .Where(s => string.IsNullOrWhiteSpace(section) ||
            string.Equals(s.TypeName, section, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.DisplayName, section, StringComparison.OrdinalIgnoreCase))
        .SelectMany(s => s.Properties.Select(p => p.Name))
        .Take(40);
    throw new McpException(
        $"No property '{property}' on this object. Properties: {string.Join(", ", names)}");
  }

  // Selects an object and returns its inspector state, waiting for the game's answer. When the
  // object is already selected, the latest state is used because the game only resends on change.
  private static async Task<InspectorStatePayload> SelectAsync(
      GameSession game, string target, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(target))
    {
      throw new McpException("'target' must be an object id or a scene path.");
    }

    var byId = SceneAddress.TryParseId(target, out var id);
    var path = target.Trim();
    bool IsTarget(InspectorStatePayload s) =>
        byId ? s.SelectedId == id : string.Equals(s.Path, path, StringComparison.Ordinal);

    if (game.Inspector is { } current && IsTarget(current))
    {
      return current;
    }

    var state = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Inspector,
        () => byId
            ? game.SelectAsync(id, cancellationToken)
            : game.SelectPathAsync(path, cancellationToken),
        p => p is InspectorStatePayload s && IsTarget(s),
        _selectTimeout,
        cancellationToken).ConfigureAwait(false) as InspectorStatePayload;
    return state ?? throw new McpException(
        $"The game did not select '{target}'. The object may not exist (any more); call " +
        "get_scene to see the current ids and paths.");
  }
}
