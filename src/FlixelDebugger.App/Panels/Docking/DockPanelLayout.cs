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

using System.Text.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Panels.Docking;

/// <summary>
/// The <see cref="IPanelLayout"/> of the real app, built on the Dock.Avalonia model.
/// </summary>
/// <remarks>
/// <para>
/// This class owns the dock layout and everything that touches Dock types: showing and hiding tabs,
/// rebuilding the default arrangement, and saving and loading the layout as JSON. The rest of the
/// app only sees <see cref="IPanelLayout"/>.
/// </para>
/// <para>
/// Saving writes a <see cref="SavedLayout"/>: just the splits and their sizes, the tab groups, the
/// tabs in each group, and the closed tabs. Loading rebuilds the dock model from that, so none of
/// the dock's many default settings end up in the settings file.
/// </para>
/// <para>
/// Panels come from the <see cref="IPanelRegistry"/>, which the game's panel list fills. A panel
/// registered later is placed where its dock hint says as soon as it is registered. Before any game
/// connected, the layout is just an empty middle group; the first panel list then builds the
/// default arrangement. A saved layout keeps its tabs even for panels no game announced yet.
/// </para>
/// <para>
/// The dock model is the only place that knows what is on screen. A panel is <em>open</em> when its
/// tab is not in the root's hidden list, and <em>visible</em> when it is open and is the active tab
/// of its group. Every change the person makes in the dock (closing a tab, switching tabs) is read
/// back from the factory events and passed on through <see cref="IPanelLayout.VisibilityChanged"/>,
/// so nothing else has to keep its own copy.
/// </para>
/// </remarks>
public sealed class DockPanelLayout : IPanelLayout
{
  private readonly IPanelRegistry _registry;
  private readonly DebuggerDockFactory _factory;
  private IRootDock? _root;
  private bool _loading;
  private bool _emptyDefault;

  /// <summary>
  /// Creates the layout service. The layout itself is built on first use.
  /// </summary>
  /// <param name="registry">Tells which panels exist.</param>
  public DockPanelLayout(IPanelRegistry registry)
  {
    ArgumentNullException.ThrowIfNull(registry);
    _registry = registry;
    _factory = new DebuggerDockFactory(registry);
    _factory.DockableHidden += (_, e) => OnTabHidden(e.Dockable);
    _factory.DockableRestored += (_, e) => RaiseIfPanel(e.Dockable);
    _factory.ActiveDockableChanged += (_, e) => RaiseForGroupOf(e.Dockable);
    registry.Changed += (_, _) => SyncWithRegistry();
  }

  /// <inheritdoc/>
  public event EventHandler? RootChanged;

  /// <inheritdoc/>
  public event EventHandler<PanelVisibilityChangedEventArgs>? VisibilityChanged;

  /// <inheritdoc/>
  public IRootDock? Root
  {
    get
    {
      return EnsureRoot();
    }
  }

  /// <inheritdoc/>
  public bool IsOpen(string id)
  {
    ArgumentNullException.ThrowIfNull(id);
    return FindTool(id) is { } tool && !IsHidden(tool);
  }

  /// <inheritdoc/>
  public bool IsVisible(string id)
  {
    ArgumentNullException.ThrowIfNull(id);
    return FindTool(id) is { } tool && !IsHidden(tool) && IsFront(tool);
  }

  /// <inheritdoc/>
  public bool SetVisible(string id, bool visible)
  {
    ArgumentNullException.ThrowIfNull(id);
    var tool = FindTool(id);
    if (tool is null)
    {
      if (!visible || !_registry.TryGet(id, out _))
      {
        return false;
      }

      AddToPlace(id);
      Raise(id);
      return true;
    }

    var hidden = IsHidden(tool);
    if (visible)
    {
      if (hidden)
      {
        _factory.RestoreDockable(tool);
        if (!IsAttached(tool))
        {
          // The dock the tab came from is gone, so put it in the middle group instead.
          EnsureRoot().HiddenDockables?.Remove(tool);
          var middle = FindMiddle()
              ?? throw new InvalidOperationException("The layout has no tab group.");
          middle.VisibleDockables?.Add(tool);
          tool.Owner = middle;
        }
      }

      // Open is not enough: a tab behind another one is open but cannot be seen, so always bring it
      // forward.
      _factory.SetActiveDockable(tool);
    }
    else if (!hidden)
    {
      _factory.HideDockable(tool);
    }

    Raise(id);
    return true;
  }

  /// <inheritdoc/>
  public bool Reveal(string id)
  {
    ArgumentNullException.ThrowIfNull(id);
    return SetVisible(id, true);
  }

  /// <inheritdoc/>
  public void Reset()
  {
    _root = BuildDefault();
    RootChanged?.Invoke(this, EventArgs.Empty);
  }

  /// <inheritdoc/>
  public string? Save()
  {
    var root = EnsureRoot();
    if (root.VisibleDockables?.FirstOrDefault(d => d is IDock) is not { } main ||
        Capture(main) is not { } top)
    {
      return null;
    }

    var hidden = new List<SavedTab>();
    foreach (var item in root.HiddenDockables ?? [])
    {
      if (item is PanelTool tool && !string.IsNullOrEmpty(tool.Id))
      {
        var group = (tool.OriginalOwner ?? tool.Owner) as IToolDock;
        hidden.Add(
            new SavedTab { Id = tool.Id, Title = tool.Title, Group = NullIfEmpty(group?.Id) });
      }
    }

    var saved = new SavedLayout
    {
      Version = SavedLayout.CurrentVersion,
      Root = top,
      Hidden = hidden.Count > 0 ? hidden : null,
    };
    return JsonSerializer.Serialize(saved, SavedLayoutJsonContext.Default.SavedLayout);
  }

  /// <inheritdoc/>
  public bool Restore(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return false;
    }

    SavedLayout? saved;
    try
    {
      saved = JsonSerializer.Deserialize(text, SavedLayoutJsonContext.Default.SavedLayout);
    }
    catch (JsonException)
    {
      return false;
    }

    if (saved is not { Version: SavedLayout.CurrentVersion, Root: { } top })
    {
      return false;
    }

    var tabs = new List<SavedTab>();
    CollectTabs(top, tabs);
    tabs.AddRange(saved.Hidden ?? []);
    tabs.RemoveAll(t => string.IsNullOrEmpty(t?.Id));
    if (tabs.Count == 0)
    {
      return false;
    }

    _loading = true;
    try
    {
      RegisterUnknown(tabs);
      var placed = new HashSet<string>(StringComparer.Ordinal);
      if (Build(top, placed) is not IDock main)
      {
        return false;
      }

      var root = _factory.CreateRootDock();
      root.Id = "root";
      root.IsCollapsable = false;
      root.VisibleDockables = _factory.CreateList<IDockable>(main);
      root.ActiveDockable = main;
      root.DefaultDockable = main;
      root.HiddenDockables = _factory.CreateList<IDockable>();
      _factory.InitLayout(root);
      _root = root;
      _emptyDefault = false;
      foreach (var tab in saved.Hidden ?? [])
      {
        if (!string.IsNullOrEmpty(tab?.Id) && placed.Add(tab.Id))
        {
          AddHidden(tab.Id, tab.Group);
        }
      }

      AddMissingPanels();
    }
    finally
    {
      _loading = false;
    }

    RootChanged?.Invoke(this, EventArgs.Empty);
    return true;
  }

  private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

  private static double? SizeOf(IDockable dockable) =>
      double.IsNaN(dockable.Proportion) ? null : dockable.Proportion;

  // Turns a part of the dock model into its saved form. Splitters and anything that is not a split
  // or a tab group give null, because they are rebuilt or not used.
  private static SavedNode? Capture(IDockable dockable)
  {
    switch (dockable)
    {
      case IToolDock group:
        var tabs = new List<SavedTab>();
        foreach (var item in group.VisibleDockables ?? [])
        {
          if (item is PanelTool tool && !string.IsNullOrEmpty(tool.Id))
          {
            tabs.Add(new SavedTab { Id = tool.Id, Title = tool.Title });
          }
        }

        return new SavedNode
        {
          Id = NullIfEmpty(group.Id),
          Size = SizeOf(group),
          Tabs = tabs,
          Active = (group.ActiveDockable as PanelTool)?.Id,
        };
      case IProportionalDock split:
        var children = new List<SavedNode>();
        foreach (var item in split.VisibleDockables ?? [])
        {
          if (Capture(item) is { } child)
          {
            children.Add(child);
          }
        }

        return new SavedNode
        {
          Id = NullIfEmpty(split.Id),
          Size = SizeOf(split),
          Split = split.Orientation,
          Children = children,
        };
      default:
        return null;
    }
  }

  private static void CollectTabs(SavedNode? node, List<SavedTab> tabs)
  {
    if (node is null)
    {
      return;
    }

    tabs.AddRange(node.Tabs ?? []);
    foreach (var child in node.Children ?? [])
    {
      CollectTabs(child, tabs);
    }
  }

  // Rebuilds a saved part of the layout. A tab that already appeared is skipped, and a group with
  // no tabs is dropped (except the middle group, which always exists), along with a split that
  // ends up empty.
  private IDockable? Build(SavedNode? node, HashSet<string> placed)
  {
    if (node is null)
    {
      return null;
    }

    if (node.Split is { } orientation)
    {
      var items = new List<IDockable>();
      foreach (var child in node.Children ?? [])
      {
        if (Build(child, placed) is { } built)
        {
          if (items.Count > 0)
          {
            items.Add(new ProportionalDockSplitter());
          }

          items.Add(built);
        }
      }

      return items.Count == 0 ? null : new ProportionalDock
      {
        Id = node.Id ?? string.Empty,
        Proportion = node.Size ?? double.NaN,
        Orientation = orientation,
        VisibleDockables = _factory.CreateList([.. items]),
      };
    }

    var tools = new List<IDockable>();
    PanelTool? active = null;
    foreach (var tab in node.Tabs ?? [])
    {
      if (!string.IsNullOrEmpty(tab?.Id) && placed.Add(tab.Id))
      {
        var tool = _factory.CreateTool(tab.Id);
        tools.Add(tool);
        if (tab.Id == node.Active)
        {
          active = tool;
        }
      }
    }

    var middle = node.Id == DebuggerDockFactory.MiddleDockId;
    return tools.Count == 0 && !middle ? null : new ToolDock
    {
      Id = node.Id ?? string.Empty,
      Proportion = node.Size ?? double.NaN,
      IsCollapsable = !middle,
      ActiveDockable = active ?? tools.FirstOrDefault(),
      VisibleDockables = _factory.CreateList([.. tools]),
    };
  }

  // Registers every saved panel no game announced yet, with the title it was saved with, so its tab
  // keeps its place until a game that has it connects.
  private void RegisterUnknown(List<SavedTab> tabs)
  {
    var unknown = new List<PanelDefinition>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var tab in tabs)
    {
      if (!_registry.TryGet(tab.Id, out _) && seen.Add(tab.Id))
      {
        unknown.Add(new PanelDefinition(tab.Id, tab.Title ?? tab.Id, PanelDock.Center, true));
      }
    }

    _registry.RegisterAll(unknown);
  }

  // Adds a closed tab to the group it was closed from (or where its dock hint says, when that group
  // is gone) and closes it again, so showing it later brings it back there.
  private void AddHidden(string id, string? groupId)
  {
    var tool = _factory.CreateTool(id);
    var target = (groupId is null ? null : FindGroupById(groupId)) ?? PlaceOf(id)
        ?? throw new InvalidOperationException("The layout has no tab group.");
    var active = target.ActiveDockable;
    _factory.AddDockable(target, tool);
    _factory.HideDockable(tool);
    if (active is not null && target.VisibleDockables?.Contains(active) == true)
    {
      target.ActiveDockable = active;
    }
  }

  private static IEnumerable<IDockable> Walk(IDockable dockable)
  {
    yield return dockable;
    if (dockable is IDock dock && dock.VisibleDockables is { } visible)
    {
      foreach (var child in visible)
      {
        foreach (var item in Walk(child))
        {
          yield return item;
        }
      }
    }

    if (dockable is IRootDock root)
    {
      foreach (var list in new[] { root.HiddenDockables, root.LeftPinnedDockables,
          root.RightPinnedDockables, root.TopPinnedDockables, root.BottomPinnedDockables })
      {
        foreach (var child in list ?? [])
        {
          foreach (var item in Walk(child))
          {
            yield return item;
          }
        }
      }
    }
  }

  private IRootDock BuildDefault()
  {
    // Building fires dock events while the new root is not in place yet, so they are ignored.
    _loading = true;
    try
    {
      var root = _factory.CreateLayout();
      _factory.InitLayout(root);
      _root = root;
      _emptyDefault = _registry.Definitions.Count == 0;
      foreach (var definition in _registry.Definitions)
      {
        if (!definition.Shown && FindTool(definition.Id) is { } tool)
        {
          _factory.HideDockable(tool);
        }
      }

      return root;
    }
    finally
    {
      _loading = false;
    }
  }

  private IRootDock EnsureRoot() => _root ?? BuildDefault();

  private PanelTool? FindTool(string id)
  {
    foreach (var item in Walk(EnsureRoot()))
    {
      if (item is PanelTool tool && tool.Id == id)
      {
        return tool;
      }
    }

    return null;
  }

  private bool IsHidden(IDockable tool) => EnsureRoot().HiddenDockables?.Contains(tool) == true;

  // A tab is in front when it is the active one of its group. A group with no active tab shows its
  // first one.
  private static bool IsFront(PanelTool tool)
  {
    if (tool.Owner is not IDock owner)
    {
      return true;
    }

    if (owner.ActiveDockable is { } active)
    {
      return ReferenceEquals(active, tool);
    }

    return owner.VisibleDockables?.OfType<PanelTool>().FirstOrDefault() is not { } first ||
        ReferenceEquals(first, tool);
  }

  private static bool IsAttached(PanelTool tool) =>
      tool.Owner is IDock owner && owner.VisibleDockables?.Contains(tool) == true;

  private IToolDock? FindMiddle()
  {
    IToolDock? first = null;
    foreach (var item in Walk(EnsureRoot()))
    {
      if (item is IToolDock dock)
      {
        if (dock.Id == DebuggerDockFactory.MiddleDockId)
        {
          return dock;
        }

        first ??= dock;
      }
    }

    return first;
  }

  // Puts a new tab where the panel's dock hint says: the first group of its column, or the middle
  // group when that place is gone. A panel the game does not show at first is closed right away.
  private PanelTool AddToPlace(string id)
  {
    var tool = _factory.CreateTool(id);
    var shown = !_registry.TryGet(id, out var definition) || definition.Shown;
    var target = PlaceOf(id) ?? throw new InvalidOperationException("The layout has no tab group.");

    _factory.AddDockable(target, tool);
    if (shown)
    {
      _factory.SetActiveDockable(tool);
    }
    else
    {
      _factory.HideDockable(tool);
    }

    return tool;
  }

  // The group a new tab of the panel goes to: the first group of its dock hint's column, or the
  // middle group when that place is gone.
  private IToolDock? PlaceOf(string id)
  {
    var place = _registry.TryGet(id, out var definition)
        ? FindGroup(DebuggerDockFactory.GroupIdOf(definition.Dock))
        : null;
    return place ?? FindMiddle();
  }

  private IToolDock? FindGroupById(string id)
  {
    foreach (var item in Walk(EnsureRoot()))
    {
      if (item is IToolDock dock && dock.Id == id)
      {
        return dock;
      }
    }

    return null;
  }

  private IToolDock? FindGroup(string id)
  {
    foreach (var item in Walk(EnsureRoot()))
    {
      if (item is IToolDock dock && dock.Id is { } dockId &&
          (dockId == id || dockId.StartsWith(id + "-", StringComparison.Ordinal)))
      {
        return dock;
      }
    }

    return null;
  }

  // Adds a tab for every registered panel the layout does not have, such as a panel a game
  // announced.
  private void AddMissingPanels()
  {
    foreach (var definition in _registry.Definitions)
    {
      if (FindTool(definition.Id) is null)
      {
        AddToPlace(definition.Id);
      }
    }
  }

  private void SyncWithRegistry()
  {
    if (_root is null || _loading)
    {
      return;
    }

    if (_emptyDefault && _registry.Definitions.Count > 0)
    {
      // The first panel list arrived while the layout was still empty: build the real default.
      Reset();
      return;
    }

    foreach (var definition in _registry.Definitions)
    {
      if (FindTool(definition.Id) is { } tool)
      {
        var viewModel = _registry.GetViewModel(definition.Id);
        if (!ReferenceEquals(tool.Context, viewModel))
        {
          tool.Context = viewModel;
        }

        tool.Title = definition.Title;
      }
      else
      {
        AddToPlace(definition.Id);
        Raise(definition.Id);
      }
    }
  }

  private void Raise(string id) =>
      VisibilityChanged?.Invoke(this, new PanelVisibilityChangedEventArgs(id, IsVisible(id)));

  private void RaiseIfPanel(IDockable? dockable)
  {
    if (!_loading && dockable is PanelTool tool)
    {
      Raise(tool.Id);
    }
  }

  // A closed tab is no longer visible, and the tab that took its place in the group just came to
  // the front.
  private void OnTabHidden(IDockable? dockable)
  {
    if (_loading || dockable is not PanelTool tool)
    {
      return;
    }

    VisibilityChanged?.Invoke(this, new PanelVisibilityChangedEventArgs(tool.Id, false));
    RaiseForGroup((tool.Owner ?? tool.OriginalOwner) as IDock);
  }

  // Switching tabs changes which panel of the group is in front, so every tab of the group is
  // reported again.
  private void RaiseForGroupOf(IDockable? dockable)
  {
    if (!_loading && dockable is PanelTool tool)
    {
      RaiseForGroup(tool.Owner as IDock);
    }
  }

  private void RaiseForGroup(IDock? group)
  {
    if (_loading || group?.VisibleDockables is not { } tabs)
    {
      return;
    }

    foreach (var tab in tabs)
    {
      if (tab is PanelTool sibling)
      {
        VisibilityChanged?.Invoke(
            this, new PanelVisibilityChangedEventArgs(sibling.Id, IsFront(sibling)));
      }
    }
  }
}
