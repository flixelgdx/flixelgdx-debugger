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
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.Widgets;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The ViewModel of one panel: a live copy of the widgets the active game built for it.
/// </summary>
/// <remarks>
/// <para>
/// Every panel in the app uses this one class, whether the game ships it or a game developer wrote
/// it. The game sends the panel's widget tree once and then small patches; this class turns them
/// into <see cref="UiWidget"/> objects that the view draws, and sends what the person does back to
/// the game. It never decides anything on its own.
/// </para>
/// <para>
/// Patches are not applied one by one. Core already keeps the current value of every property in
/// a <see cref="UiPanelState"/>, so when a patch arrives this class only notes which properties
/// changed and, on the UI thread, reads their current values from that state. Reading the same
/// value twice changes nothing, so a late or repeated notice is harmless. Widgets keep their
/// identity across new trees when their id and type stay the same, so a table keeps its scroll
/// position and a tree keeps what the person expanded.
/// </para>
/// <para>Use this class from the UI thread only, apart from the session events it handles.</para>
/// </remarks>
public sealed partial class GamePanelViewModel : ObservableObject
{
  /// <summary>The owner name the app uses when it holds a panel open.</summary>
  public const string Owner = "ui";

  private readonly IUiScheduler _ui;
  private readonly Dictionary<int, Func<Task>> _flushers = [];
  private readonly Dictionary<int, UiWidget> _widgets = [];
  private readonly HashSet<(int Widget, WidgetProp Key)> _pending = [];
  private readonly Lock _pendingGate = new();
  private IGameSession? _session;
  private bool _flushPosted;
  private bool _rebuildPosted;

  [ObservableProperty]
  private string _title;

  [ObservableProperty]
  private UiWidget? _root;

  [ObservableProperty]
  private string _statusText = string.Empty;

  /// <summary>
  /// Creates the ViewModel of a panel and starts following the active game.
  /// </summary>
  /// <param name="definition">The panel's definition.</param>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="ui">Moves work to the UI thread.</param>
  /// <param name="dialogs">Asks the person before a button with a confirmation runs.</param>
  public GamePanelViewModel(
      PanelDefinition definition, IActiveSession active, IUiScheduler ui, IDialogService dialogs)
  {
    ArgumentNullException.ThrowIfNull(definition);
    ArgumentNullException.ThrowIfNull(active);
    ArgumentNullException.ThrowIfNull(ui);
    ArgumentNullException.ThrowIfNull(dialogs);
    Id = definition.Id;
    Dialogs = dialogs;
    _title = definition.Title;
    _ui = ui;
    active.Changed += (_, e) => Follow(e.NewSession);
    Follow(active.Current);
  }

  /// <summary>Gets the stable panel id.</summary>
  public string Id { get; }

  /// <summary>Gets the dialogs the panel's widgets may open.</summary>
  public IDialogService Dialogs { get; }

  /// <summary>Gets whether a widget tree is shown.</summary>
  public bool HasRoot => Root is not null;

  /// <summary>
  /// Looks up a widget of the current tree by its id.
  /// </summary>
  /// <param name="id">The widget id.</param>
  /// <param name="widget">The widget, when found.</param>
  /// <returns><see langword="true"/> if the tree has a widget with that id.</returns>
  public bool TryGetWidget(int id, out UiWidget widget) => _widgets.TryGetValue(id, out widget!);

  /// <summary>
  /// Finds the first widget of the current tree with the given name.
  /// </summary>
  /// <param name="name">The name the game gave the widget.</param>
  /// <returns>The widget, or <see langword="null"/>.</returns>
  public UiWidget? FindByName(string name)
  {
    ArgumentNullException.ThrowIfNull(name);
    foreach (var widget in _widgets.Values)
    {
      if (string.Equals(widget.Name, name, StringComparison.Ordinal))
      {
        return widget;
      }
    }

    return null;
  }

  /// <summary>
  /// Sends an event of one widget to the active game.
  /// </summary>
  /// <param name="widget">The widget id.</param>
  /// <param name="kind">What happened.</param>
  /// <param name="args">The event's arguments.</param>
  /// <returns>Whether the event was sent.</returns>
  public async Task<bool> SendAsync(int widget, WidgetEvent kind, IReadOnlyList<UiValue> args)
  {
    ArgumentNullException.ThrowIfNull(args);
    if (_session is not { } session)
    {
      return false;
    }

    // A text box may still hold typing it has not sent. The game must see it before this event,
    // for example the value of a field before the Set button next to it.
    foreach (var flush in _flushers.Values.ToArray())
    {
      await flush();
    }

    return await session.SendPanelEventAsync(Id, widget, kind, args);
  }

  /// <summary>
  /// Sends an event without flushing pending changes first. The flushers themselves use this.
  /// </summary>
  /// <param name="widget">The widget id.</param>
  /// <param name="kind">What happened.</param>
  /// <param name="args">The event's arguments.</param>
  /// <returns>Whether the event was sent.</returns>
  public Task<bool> SendRawAsync(int widget, WidgetEvent kind, IReadOnlyList<UiValue> args)
  {
    ArgumentNullException.ThrowIfNull(args);
    return _session is { } session
        ? session.SendPanelEventAsync(Id, widget, kind, args)
        : Task.FromResult(false);
  }

  /// <summary>
  /// Registers work that sends a widget's pending change, such as the last typing of a live text
  /// box. It runs before every event the panel sends.
  /// </summary>
  /// <param name="widget">
  /// The widget id. A newer registration for the same widget replaces it.
  /// </param>
  /// <param name="flush">The work. It must not send anything when nothing is pending.</param>
  public void SetFlusher(int widget, Func<Task> flush)
  {
    ArgumentNullException.ThrowIfNull(flush);
    _flushers[widget] = flush;
  }

  /// <summary>
  /// Brings the panel up to date with the active game right away. The ViewModel does this by
  /// itself whenever the game sends something; call it after opening the panel.
  /// </summary>
  public void Refresh()
  {
    var state = FindState();
    var snapshot = state?.Snapshot();
    if (snapshot is null)
    {
      _widgets.Clear();
      Root = null;
      StatusText = _session switch
      {
        null => "No game is connected.",
        { State: SessionState.Closed } => "The game disconnected.",
        _ when state is null => "The connected game has no panel with this id.",
        _ => "Waiting for the game to send this panel...",
      };
      return;
    }

    var old = new Dictionary<int, UiWidget>(_widgets);
    _widgets.Clear();
    Root = Build(snapshot, old);
    foreach (var id in _flushers.Keys.ToArray())
    {
      if (!_widgets.ContainsKey(id))
      {
        _flushers.Remove(id);
      }
    }

    StatusText = string.Empty;
  }

  partial void OnRootChanged(UiWidget? value) => OnPropertyChanged(nameof(HasRoot));

  private void Follow(IGameSession? session)
  {
    if (_session is { } old)
    {
      old.PanelTreeReceived -= OnTree;
      old.PanelPatched -= OnPatch;
      old.PanelListChanged -= OnList;
      old.StateChanged -= OnList;
    }

    _session = session;
    if (session is not null)
    {
      session.PanelTreeReceived += OnTree;
      session.PanelPatched += OnPatch;
      session.PanelListChanged += OnList;
      session.StateChanged += OnList;
    }

    lock (_pendingGate)
    {
      _pending.Clear();
    }

    Refresh();
  }

  private UiPanelState? FindState()
  {
    if (_session is not { } session)
    {
      return null;
    }

    foreach (var panel in session.Panels)
    {
      if (string.Equals(panel.Id, Id, StringComparison.Ordinal))
      {
        return panel;
      }
    }

    return null;
  }

  // Reuses the widget of the same id and type, so its control keeps its state.
  private UiWidget Build(WidgetNode node, Dictionary<int, UiWidget> old)
  {
    UiWidget widget;
    if (old.TryGetValue(node.Id, out var existing) && existing.Type == node.Type &&
        existing.Name == node.Name)
    {
      widget = existing;
      widget.SetAll(node.Props);
    }
    else
    {
      widget = new UiWidget(this, node);
    }

    _widgets[node.Id] = widget;
    var children = new UiWidget[node.Children.Count];
    for (var i = 0; i < children.Length; i++)
    {
      children[i] = Build(node.Children[i], old);
    }

    widget.SetChildren(children);
    return widget;
  }

  private void OnTree(object? sender, PanelTreeEventArgs e)
  {
    if (!string.Equals(e.Panel.Id, Id, StringComparison.Ordinal))
    {
      return;
    }

    PostRebuild(sender);
  }

  private void OnList(object? sender, EventArgs e) => PostRebuild(sender);

  private void PostRebuild(object? sender)
  {
    lock (_pendingGate)
    {
      if (_rebuildPosted)
      {
        return;
      }

      _rebuildPosted = true;
    }

    _ui.Post(() =>
    {
      lock (_pendingGate)
      {
        _rebuildPosted = false;
      }

      if (ReferenceEquals(sender, _session))
      {
        Refresh();
      }
    });
  }

  private void OnPatch(object? sender, PanelPatchEventArgs e)
  {
    if (!string.Equals(e.Panel.Id, Id, StringComparison.Ordinal))
    {
      return;
    }

    lock (_pendingGate)
    {
      foreach (var op in e.Ops)
      {
        _pending.Add((op.Widget, op.Key));
      }

      if (_flushPosted)
      {
        return;
      }

      _flushPosted = true;
    }

    _ui.Post(() => Flush(sender));
  }

  private void Flush(object? sender)
  {
    (int Widget, WidgetProp Key)[] changes;
    lock (_pendingGate)
    {
      _flushPosted = false;
      changes = [.. _pending];
      _pending.Clear();
    }

    if (!ReferenceEquals(sender, _session) || FindState() is not { } state)
    {
      return;
    }

    foreach (var (id, key) in changes)
    {
      if (_widgets.TryGetValue(id, out var widget))
      {
        widget.Set(key, state.Get(id, key) ?? UiValue.Null);
      }
    }
  }
}
