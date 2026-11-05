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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One node of the scene tree.
/// </summary>
public sealed partial class SceneNodeViewModel : ObservableObject
{
  [ObservableProperty]
  private string _type = string.Empty;

  [ObservableProperty]
  private string _label = string.Empty;

  [ObservableProperty]
  private bool _isTrackedRoot;

  [ObservableProperty]
  private bool _isExpanded;

  /// <summary>
  /// Creates a node.
  /// </summary>
  /// <param name="id">The object id.</param>
  public SceneNodeViewModel(int id)
  {
    Id = id;
  }

  /// <summary>Gets the object id.</summary>
  public int Id { get; }

  /// <summary>Gets the children in the order the game sent them.</summary>
  public RangeObservableCollection<SceneNodeViewModel> Children { get; } = [];

  /// <summary>Gets the node's text: its type name and its label.</summary>
  public string Text => Label.Length > 0 ? $"{Type} {Label}" : Type;

  partial void OnTypeChanged(string value) => OnPropertyChanged(nameof(Text));

  partial void OnLabelChanged(string value) => OnPropertyChanged(nameof(Text));
}

/// <summary>
/// The ViewModel of the scene panel: a tree of the game's objects that selects the clicked one in
/// the inspector.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stable tree.</b> A new snapshot does not rebuild the tree. Node objects are kept by id and
/// only the nodes whose children changed are touched, so the tree keeps its scroll position and
/// every expanded node stays expanded. Expansion is remembered by id, even for a node that is gone
/// for a moment.
/// </para>
/// <para>
/// <b>Selection.</b> Clicking a node sends <c>SelectAsync(id)</c>. The other way round, when the
/// game selects an object (an Alt+click in the game window), the matching node is revealed: its
/// ancestors are expanded and the node is selected without sending anything back.
/// </para>
/// </remarks>
public sealed partial class ScenePanelViewModel : LivePanelViewModel
{
  // Marks that no object waits to be revealed. Ids are never negative, and -1 means "nothing
  // selected".
  private const int NoReveal = -2;

  private readonly SelectionOrigin _origin;
  private readonly Dictionary<int, SceneNodeViewModel> _nodes = [];
  private readonly HashSet<int> _expanded = [];
  private readonly HashSet<int> _seenRoots = [];
  private SceneTree? _lastTree;
  private int _lastInspectorId = -1;
  private int _pendingReveal = NoReveal;
  private bool _syncing;
  private bool _built;

  [ObservableProperty]
  private SceneNodeViewModel? _selectedNode;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  /// <param name="origin">Marks selections that the person made here.</param>
  public ScenePanelViewModel(IActiveSession active, IUiScheduler scheduler, SelectionOrigin origin)
      : base(BuiltInPanels.Scene, "Scene", active, scheduler)
  {
    ArgumentNullException.ThrowIfNull(origin);
    _origin = origin;
    _lastInspectorId = active.Current?.Inspector?.SelectedId ?? -1;
    _pendingReveal = _lastInspectorId >= 0 ? _lastInspectorId : NoReveal;
    Refresh();
  }

  /// <summary>Gets the nodes without a parent.</summary>
  public RangeObservableCollection<SceneNodeViewModel> Roots { get; } = [];

  /// <summary>Gets whether the tree has no nodes.</summary>
  public bool IsEmpty => Roots.Count == 0;

  /// <inheritdoc/>
  public override void Refresh()
  {
    var session = Session;
    var tree = session?.Scene ?? SceneTree.Empty;
    if (!_built || !ReferenceEquals(tree, _lastTree))
    {
      _built = true;
      _lastTree = tree;
      Apply(tree);
    }

    var inspectorId = session?.Inspector?.SelectedId ?? -1;
    if (inspectorId != _lastInspectorId)
    {
      _lastInspectorId = inspectorId;
      _pendingReveal = inspectorId;
    }

    if (_pendingReveal != NoReveal)
    {
      Reveal(_pendingReveal);
    }
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    _built = false;
    _lastInspectorId = newSession?.Inspector?.SelectedId ?? -1;
    _pendingReveal = _lastInspectorId >= 0 ? _lastInspectorId : NoReveal;
    _nodes.Clear();
    _expanded.Clear();
    _seenRoots.Clear();
    Roots.ReplaceAll([]);
    SetSelection(null);
  }

  partial void OnSelectedNodeChanged(SceneNodeViewModel? value)
  {
    if (!_syncing && value is not null && Session is { } session)
    {
      _origin.MarkLocal(value.Id);
      _ = session.SelectAsync(value.Id);
    }
  }

  [RelayCommand]
  private async Task RefreshTreeAsync()
  {
    if (Session is { } session)
    {
      await session.RequestSceneTreeAsync();
    }
  }

  private void Apply(SceneTree tree)
  {
    var alive = new HashSet<int>(tree.Nodes.Count);
    foreach (var node in tree.Nodes)
    {
      alive.Add(node.Id);
      var view = NodeFor(node);
      view.Type = node.Type;
      view.Label = node.Label;
      view.IsTrackedRoot = node.TrackedRoot;
      SetChildren(view.Children, tree.GetChildren(node.Id));
    }

    // Forget nodes that left the tree, but not their expanded state: the same id may come back.
    foreach (var id in _nodes.Keys.Where(id => !alive.Contains(id)).ToList())
    {
      _nodes.Remove(id);
    }

    SetChildren(Roots, tree.Roots);
    foreach (var root in tree.Roots)
    {
      // A root opens the first time it is seen, so a new game shows its first level.
      if (_seenRoots.Add(root.Id) && !_expanded.Contains(root.Id))
      {
        _nodes[root.Id].IsExpanded = true;
      }
    }

    if (SelectedNode is { } selected && !_nodes.ContainsKey(selected.Id))
    {
      SetSelection(null);
    }

    OnPropertyChanged(nameof(IsEmpty));
  }

  private SceneNodeViewModel NodeFor(SceneNode node)
  {
    if (!_nodes.TryGetValue(node.Id, out var view))
    {
      view = new SceneNodeViewModel(node.Id) { IsExpanded = _expanded.Contains(node.Id) };
      view.PropertyChanged += OnNodeChanged;
      _nodes[node.Id] = view;
    }

    return view;
  }

  private void OnNodeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (sender is SceneNodeViewModel node && e.PropertyName == nameof(SceneNodeViewModel.IsExpanded))
    {
      if (node.IsExpanded)
      {
        _expanded.Add(node.Id);
      }
      else
      {
        _expanded.Remove(node.Id);
      }
    }
  }

  // Replaces a child list only when its ids differ, so untouched branches do not flicker.
  private void SetChildren(RangeObservableCollection<SceneNodeViewModel> target, IReadOnlyList<SceneNode> source)
  {
    var same = target.Count == source.Count;
    for (var i = 0; same && i < source.Count; i++)
    {
      same = target[i].Id == source[i].Id;
    }

    if (same)
    {
      return;
    }

    var list = new List<SceneNodeViewModel>(source.Count);
    foreach (var child in source)
    {
      list.Add(NodeFor(child));
    }

    target.ReplaceAll(list);
  }

  // Selects the node for an object id the game selected, opening its ancestors. An unknown id waits
  // for the next snapshot.
  private void Reveal(int id)
  {
    if (id < 0)
    {
      _pendingReveal = NoReveal;
      if (SelectedNode is not null)
      {
        SetSelection(null);
      }

      return;
    }

    if (!_nodes.TryGetValue(id, out var node) || _lastTree is null)
    {
      return;
    }

    var tree = _lastTree;
    var current = id;
    while (tree.TryGetParent(current, out var parent))
    {
      if (_nodes.TryGetValue(parent.Id, out var ancestor))
      {
        ancestor.IsExpanded = true;
      }

      current = parent.Id;
    }

    _pendingReveal = NoReveal;
    SetSelection(node);
  }

  private void SetSelection(SceneNodeViewModel? node)
  {
    _syncing = true;
    SelectedNode = node;
    _syncing = false;
  }
}
