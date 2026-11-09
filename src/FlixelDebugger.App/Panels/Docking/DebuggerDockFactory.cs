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

using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Panels.Docking;

/// <summary>
/// Builds the default arrangement of the debugger's panels from the dock hint each game panel
/// carries.
/// </summary>
/// <remarks>
/// <para>
/// The app knows no panel by name. The game says where each panel goes, and the default layout is:
/// </para>
/// <list type="bullet">
/// <item><description>
/// left column: every <see cref="PanelDock.Left"/> panel in its own group, stacked;
/// </description></item>
/// <item><description>
/// middle: every <see cref="PanelDock.Center"/> panel as a tab of one group (this group always
/// exists, even when it is empty);
/// </description></item>
/// <item><description>
/// right column: every <see cref="PanelDock.Right"/> panel in its own group, stacked;
/// </description></item>
/// <item><description>
/// bottom, across the full width: every <see cref="PanelDock.Bottom"/> panel as a tab of one group.
/// </description></item>
/// </list>
/// <para>
/// A panel the game marks as not shown is added to its place and then closed, so the Debug menu
/// can bring it back where it belongs.
/// </para>
/// <para>
/// Closing a tab hides the panel instead of destroying it, so the Debug menu can bring it back.
/// </para>
/// </remarks>
public sealed class DebuggerDockFactory : Factory
{
  /// <summary>
  /// The id of the tab group in the middle, where panels without a better place go.
  /// </summary>
  public const string MiddleDockId = "middle";

  /// <summary>The id prefix of the groups in the left column.</summary>
  public const string LeftDockId = "left";

  /// <summary>The id prefix of the groups in the right column.</summary>
  public const string RightDockId = "right";

  /// <summary>The id of the tab group at the bottom.</summary>
  public const string BottomDockId = "bottom";

  private readonly IPanelRegistry _registry;

  /// <summary>
  /// Creates a factory.
  /// </summary>
  /// <param name="registry">Tells which panels exist and provides their ViewModels.</param>
  public DebuggerDockFactory(IPanelRegistry registry)
  {
    ArgumentNullException.ThrowIfNull(registry);
    _registry = registry;
    HideToolsOnClose = true;
  }

  /// <summary>
  /// Creates the dock item for a registered panel.
  /// </summary>
  /// <param name="id">The panel id.</param>
  /// <returns>A new tool whose context is the panel's ViewModel.</returns>
  /// <exception cref="KeyNotFoundException">No panel has that id.</exception>
  public PanelTool CreateTool(string id)
  {
    if (!_registry.TryGet(id, out var definition))
    {
      throw new KeyNotFoundException($"No panel is registered with the id '{id}'.");
    }

    return new PanelTool
    {
      Id = definition.Id,
      Title = definition.Title,
      Context = _registry.GetViewModel(definition.Id),
      CanClose = true,
      CanPin = false,
      CanFloat = false,
    };
  }

  /// <summary>
  /// Returns the id prefix of the groups a panel with the given dock hint belongs to.
  /// </summary>
  /// <param name="dock">The dock hint.</param>
  /// <returns>The group id or id prefix.</returns>
  public static string GroupIdOf(PanelDock dock) => dock switch
  {
    PanelDock.Left => LeftDockId,
    PanelDock.Right => RightDockId,
    PanelDock.Bottom => BottomDockId,
    _ => MiddleDockId,
  };

  /// <inheritdoc/>
  public override IRootDock CreateLayout()
  {
    var lefts = new List<string>();
    var centers = new List<string>();
    var rights = new List<string>();
    var bottoms = new List<string>();
    foreach (var definition in _registry.Definitions)
    {
      var list = definition.Dock switch
      {
        PanelDock.Left => lefts,
        PanelDock.Right => rights,
        PanelDock.Bottom => bottoms,
        _ => centers,
      };
      list.Add(definition.Id);
    }

    var middle = Group(double.NaN, MiddleDockId, [.. centers]);
    middle.IsCollapsable = false;
    var columns = new List<IDockable>();
    if (lefts.Count > 0)
    {
      columns.Add(Column(0.3, LeftDockId, lefts));
      columns.Add(new ProportionalDockSplitter());
    }

    columns.Add(middle);
    if (rights.Count > 0)
    {
      columns.Add(new ProportionalDockSplitter());
      columns.Add(Column(0.22, RightDockId, rights));
    }

    var top = new ProportionalDock
    {
      Orientation = Orientation.Horizontal,
      VisibleDockables = CreateList([.. columns]),
    };

    var rows = new List<IDockable> { top };
    if (bottoms.Count > 0)
    {
      rows.Add(new ProportionalDockSplitter());
      rows.Add(Group(0.2, BottomDockId, [.. bottoms]));
    }

    var main = new ProportionalDock
    {
      Id = "main",
      Orientation = Orientation.Vertical,
      VisibleDockables = CreateList([.. rows]),
    };

    var root = CreateRootDock();
    root.Id = "root";
    root.IsCollapsable = false;
    root.VisibleDockables = CreateList<IDockable>(main);
    root.ActiveDockable = main;
    root.DefaultDockable = main;
    root.HiddenDockables = CreateList<IDockable>();
    return root;
  }

  /// <summary>
  /// Moves a panel into the middle tab group, which is the debugger's main document area.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This runs when the person picks "Dock as Tabbed Document" from a tab's menu. Dock.Avalonia
  /// only knows how to move a tab into a document dock, and the debugger's layout has none (every
  /// group is a tool group), so without this override the menu item did nothing.
  /// </para>
  /// <para>
  /// If a layout ever does have a document dock, the default Dock.Avalonia behavior is used. A
  /// panel that is already in the middle group stays where it is.
  /// </para>
  /// </remarks>
  /// <param name="dockable">The tab to move.</param>
  public override void DockAsDocument(IDockable dockable)
  {
    ArgumentNullException.ThrowIfNull(dockable);
    var enabled = DockCapabilityResolver.IsEnabled(
        dockable,
        DockCapability.DockAsDocument,
        DockCapabilityResolver.ResolveOperationDock(dockable));
    if (!enabled || dockable.Owner is not IDock source || FindRoot(source) is not { } root)
    {
      return;
    }

    if (FindDockable(root, d => d is IDocumentDock) is not null)
    {
      base.DockAsDocument(dockable);
      return;
    }

    if (FindDockable(root, d => d is IToolDock { Id: MiddleDockId }) is not IDock middle ||
        ReferenceEquals(middle, source))
    {
      return;
    }

    MoveDockable(source, middle, dockable, middle.VisibleDockables?.LastOrDefault());
    SetActiveDockable(dockable);
  }

  /// <inheritdoc/>
  public override void InitLayout(IDockable layout)
  {
    HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
    {
      [nameof(IDockWindow)] = () => new HostWindow(),
    };

    base.InitLayout(layout);
  }

  // Every panel of a column gets its own group, stacked top to bottom with equal heights.
  private ProportionalDock Column(double proportion, string id, List<string> panelIds)
  {
    var items = new List<IDockable>();
    for (var i = 0; i < panelIds.Count; i++)
    {
      if (items.Count > 0)
      {
        items.Add(new ProportionalDockSplitter());
      }

      items.Add(Group(double.NaN, $"{id}-{i + 1}", panelIds[i]));
    }

    return new ProportionalDock
    {
      Proportion = proportion,
      Orientation = Orientation.Vertical,
      VisibleDockables = CreateList([.. items]),
    };
  }

  private ToolDock Group(double proportion, string id, params string[] ids)
  {
    var tools = new List<IDockable>();
    foreach (var panelId in ids)
    {
      tools.Add(CreateTool(panelId));
    }

    return new ToolDock
    {
      Id = id,
      Proportion = proportion,
      ActiveDockable = tools.Count > 0 ? tools[0] : null,
      VisibleDockables = CreateList([.. tools]),
    };
  }
}
