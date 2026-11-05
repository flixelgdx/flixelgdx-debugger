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

using System.Collections.ObjectModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Serializer.SystemTextJson;

namespace FlixelDebugger.App.Panels.Docking;

/// <summary>
/// The <see cref="IPanelLayout"/> of the real app, built on the Dock.Avalonia model.
/// </summary>
/// <remarks>
/// <para>
/// This class owns the dock layout and everything that touches Dock types: showing and hiding tabs, rebuilding
/// the default arrangement, and saving and loading the layout as JSON. The rest of the app only sees
/// <see cref="IPanelLayout"/>.
/// </para>
/// <para>
/// Panels come from the <see cref="IPanelRegistry"/>. A panel that games add later (a custom panel) is placed in
/// the middle tab group as soon as it is registered.
/// </para>
/// </remarks>
public sealed class DockPanelLayout : IPanelLayout
{
    private readonly IPanelRegistry _registry;
    private readonly DebuggerDockFactory _factory;
    private readonly DockSerializer _serializer = new(typeof(ObservableCollection<>));
    private IRootDock? _root;

    /// <summary>
    /// Creates the layout service. The layout itself is built on first use.
    /// </summary>
    /// <param name="registry">Tells which panels exist.</param>
    public DockPanelLayout(IPanelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _factory = new DebuggerDockFactory(registry);
        _factory.DockableHidden += (_, e) => RaiseIfPanel(e.Dockable, false);
        _factory.DockableRestored += (_, e) => RaiseIfPanel(e.Dockable, true);
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
    public bool IsVisible(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return FindTool(id) is { } tool && !IsHidden(tool);
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

            tool = AddToMiddle(id);
            VisibilityChanged?.Invoke(this, new PanelVisibilityChangedEventArgs(id, true));
            return true;
        }

        var hidden = IsHidden(tool);
        if (visible && hidden)
        {
            _factory.RestoreDockable(tool);
            if (!IsAttached(tool))
            {
                // The dock the tab came from is gone, so put it in the middle group instead.
                EnsureRoot().HiddenDockables?.Remove(tool);
                (FindMiddle() ?? throw new InvalidOperationException("The layout has no tab group.")).VisibleDockables?.Add(tool);
                tool.Owner = FindMiddle();
            }

            _factory.SetActiveDockable(tool);
        }
        else if (!visible && !hidden)
        {
            _factory.HideDockable(tool);
        }

        VisibilityChanged?.Invoke(this, new PanelVisibilityChangedEventArgs(id, visible));
        return true;
    }

    /// <inheritdoc/>
    public bool Reveal(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (FindTool(id) is not { } tool || IsHidden(tool))
        {
            return SetVisible(id, true);
        }

        _factory.SetActiveDockable(tool);
        return true;
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
        try
        {
            return _serializer.Serialize<IDock?>(EnsureRoot());
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public bool Restore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        IRootDock? layout;
        try
        {
            layout = _serializer.Deserialize<IDock?>(text) as IRootDock;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.Text.Json.JsonException)
        {
            return false;
        }

        if (layout is null || !AttachContexts(layout))
        {
            return false;
        }

        _factory.InitLayout(layout);
        _root = layout;
        AddMissingPanels();
        RootChanged?.Invoke(this, EventArgs.Empty);
        return true;
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
            foreach (var list in new[] { root.HiddenDockables, root.LeftPinnedDockables, root.RightPinnedDockables, root.TopPinnedDockables, root.BottomPinnedDockables })
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
        var root = _factory.CreateLayout();
        _factory.InitLayout(root);
        _root = root;
        AddMissingPanels();
        return root;
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

    private PanelTool AddToMiddle(string id)
    {
        var tool = _factory.CreateTool(id);
        var middle = FindMiddle() ?? throw new InvalidOperationException("The layout has no tab group.");
        _factory.AddDockable(middle, tool);
        _factory.SetActiveDockable(tool);
        return tool;
    }

    // Adds a tab for every registered panel the layout does not have, such as a panel a game announced.
    private void AddMissingPanels()
    {
        foreach (var definition in _registry.Definitions)
        {
            if (FindTool(definition.Id) is null)
            {
                AddToMiddle(definition.Id);
            }
        }
    }

    private void SyncWithRegistry()
    {
        if (_root is null)
        {
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
            }
            else
            {
                AddToMiddle(definition.Id);
                VisibilityChanged?.Invoke(this, new PanelVisibilityChangedEventArgs(definition.Id, true));
            }
        }
    }

    // Gives every saved tab its ViewModel. Tabs of panels this build does not know are dropped.
    private bool AttachContexts(IRootDock layout)
    {
        var unknown = new List<PanelTool>();
        var known = 0;
        foreach (var item in Walk(layout))
        {
            if (item is not PanelTool tool)
            {
                continue;
            }

            if (!_registry.TryGet(tool.Id, out _) &&
                !(tool.Id.StartsWith(PanelDefinition.CustomPrefix, StringComparison.Ordinal) && _registry.RegisterCustom(tool.Id, tool.Title)))
            {
                unknown.Add(tool);
                continue;
            }

            tool.Context = _registry.GetViewModel(tool.Id);
            known++;
        }

        foreach (var tool in unknown)
        {
            (tool.Owner as IDock)?.VisibleDockables?.Remove(tool);
            layout.HiddenDockables?.Remove(tool);
        }

        layout.HiddenDockables ??= _factory.CreateList<IDockable>();
        return known > 0;
    }

    private void RaiseIfPanel(IDockable? dockable, bool visible)
    {
        if (dockable is PanelTool tool)
        {
            VisibilityChanged?.Invoke(this, new PanelVisibilityChangedEventArgs(tool.Id, visible));
        }
    }
}
