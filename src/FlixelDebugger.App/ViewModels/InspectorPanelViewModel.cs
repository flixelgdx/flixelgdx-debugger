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
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One section of the inspector: a component of the selected object with its properties.
/// </summary>
public sealed partial class InspectorSectionViewModel : ObservableObject
{
  [ObservableProperty]
  private bool _isExpanded = true;

  /// <summary>
  /// Creates a section.
  /// </summary>
  /// <param name="index">The position of the section in the inspector state.</param>
  /// <param name="section">The section from the game.</param>
  /// <param name="grid">The property grid of the section.</param>
  public InspectorSectionViewModel(int index, InspectorSection section, PropertyGridViewModel grid)
  {
    Index = index;
    TypeName = section.TypeName;
    DisplayName = section.DisplayName.Length > 0 ? section.DisplayName : section.TypeName;
    Grid = grid;
  }

  /// <summary>Gets the position of the section in the inspector state.</summary>
  public int Index { get; }

  /// <summary>Gets the type name of the section, for example <c>FlixelSprite</c>.</summary>
  public string TypeName { get; }

  /// <summary>Gets the name shown in the section header.</summary>
  public string DisplayName { get; }

  /// <summary>Gets the property rows.</summary>
  public PropertyGridViewModel Grid { get; }
}

/// <summary>
/// The ViewModel of the inspector panel: the selected object's path, sections of editable
/// properties, and a texture preview.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refresh rate.</b> The inspector refreshes about 30 times a second, because it follows a
/// moving object.
/// </para>
/// <para>
/// <b>Auto-open.</b> When the game reports a different selected object (for example after an
/// Alt+click in the game window), the panel brings its own tab into view. A selection the person
/// made in the scene panel is recognized through <see cref="SelectionOrigin"/> and does not steal
/// the tab.
/// </para>
/// <para>
/// <b>Editing.</b> Edits go through <see cref="PropertyRowViewModel"/>, which protects the row
/// being typed in. Each edit names the selected object, the section, and the row, exactly as the
/// protocol wants.
/// </para>
/// <para>
/// <b>Preview.</b> If the object has a texture and the Preview section is open, the panel asks the
/// game for a thumbnail once per selected object.
/// </para>
/// </remarks>
public sealed partial class InspectorPanelViewModel : LivePanelViewModel
{
  /// <summary>How often the inspector refreshes: about 30 times a second.</summary>
  public static readonly TimeSpan InspectorInterval = TimeSpan.FromMilliseconds(33);

  /// <summary>The hint shown while nothing is selected.</summary>
  public const string EmptyHint =
      "Nothing is selected. Pick an object in the Scene panel, or Alt+click it in the game.";

  private const int PreviewTimeoutTicks = 90;

  private readonly IPanelLayout _layout;
  private readonly SelectionOrigin _origin;
  private readonly AgentActivityMonitor _agent;
  private readonly Dictionary<string, bool> _expanded = new(StringComparer.Ordinal);
  private InspectorStatePayload? _last;
  private int _previewRequestedFor = -1;
  private int _lastSelectedId = -1;
  private bool _built;

  [ObservableProperty]
  private bool _hasSelection;

  [ObservableProperty]
  private bool _isAgentSelection;

  [ObservableProperty]
  private bool _hasTexture;

  [ObservableProperty]
  private string _path = string.Empty;

  [ObservableProperty]
  private string _pathEntry = string.Empty;

  [ObservableProperty]
  private bool _previewExpanded;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  /// <param name="layout">Brings this panel into view when the game selects an object.</param>
  /// <param name="origin">Tells selections made here from selections made in the game.</param>
  /// <param name="agent">Tells which object an AI agent touched last, to mark it.</param>
  public InspectorPanelViewModel(
      IActiveSession active,
      IUiScheduler scheduler,
      IPanelLayout layout,
      SelectionOrigin origin,
      AgentActivityMonitor agent)
      : base(BuiltInPanels.Inspector, "Inspector", active, scheduler, InspectorInterval)
  {
    ArgumentNullException.ThrowIfNull(layout);
    ArgumentNullException.ThrowIfNull(origin);
    ArgumentNullException.ThrowIfNull(agent);
    _layout = layout;
    _origin = origin;
    _agent = agent;
    Preview = new ThumbnailPreviewViewModel(PreviewTimeoutTicks);
    _lastSelectedId = active.Current?.Inspector?.SelectedId ?? -1;
    Refresh();
  }

  /// <summary>Gets the sections, most derived type first, as the game sent them.</summary>
  public RangeObservableCollection<InspectorSectionViewModel> Sections { get; } = [];

  /// <summary>Gets the texture preview.</summary>
  public ThumbnailPreviewViewModel Preview { get; }

  /// <summary>Gets the id of the selected object, or -1 when nothing is selected.</summary>
  public int SelectedId => _last?.SelectedId ?? -1;

  /// <inheritdoc/>
  public override void Refresh()
  {
    var payload = Session?.Inspector;
    IsAgentSelection = _agent.IsDriving(Session?.Id) && payload is { SelectedId: >= 0 } selected &&
        selected.SelectedId == _agent.ObjectId;
    if (!_built || !ReferenceEquals(payload, _last))
    {
      _built = true;
      Apply(payload);
    }

    SyncPreview();
    Preview.Tick();
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    _built = false;
    _last = null;
    _lastSelectedId = newSession?.Inspector?.SelectedId ?? -1;
    _previewRequestedFor = -1;
    Preview.Clear();
    Sections.ReplaceAll([]);
  }

  partial void OnPreviewExpandedChanged(bool value) => SyncPreview();

  [RelayCommand]
  private async Task SelectParentAsync()
  {
    if (Session is { } session)
    {
      await session.SelectParentAsync();
    }
  }

  [RelayCommand]
  private async Task GoToAsync()
  {
    if (Session is { } session)
    {
      await session.GoToSelectionAsync();
    }
  }

  [RelayCommand]
  private async Task ClearAsync()
  {
    if (Session is { } session)
    {
      await session.ClearSelectionAsync();
    }
  }

  [RelayCommand]
  private async Task SelectPathAsync()
  {
    var text = PathEntry.Trim();
    if (text.Length == 0 || Session is not { } session)
    {
      return;
    }

    PathEntry = string.Empty;
    await session.SelectPathAsync(text);
  }

  [RelayCommand]
  private async Task RefreshPreviewAsync()
  {
    if (SelectedId >= 0 && Session is { } session)
    {
      Preview.BeginWaiting();
      await session.RequestInspectorThumbnailAsync(SelectedId);
    }
  }

  private void Apply(InspectorStatePayload? payload)
  {
    var previousId = _last?.SelectedId ?? -1;
    _last = payload;
    var id = payload?.SelectedId ?? -1;
    HasSelection = id >= 0;
    HasTexture = payload is { HasTexture: true } && id >= 0;
    Path = payload?.Path ?? string.Empty;

    if (payload is null || id < 0)
    {
      Sections.ReplaceAll([]);
    }
    else if (id == previousId && SameShape(payload))
    {
      for (var i = 0; i < payload.Sections.Count; i++)
      {
        Sections[i].Grid.Update(payload.Sections[i].Properties);
      }
    }
    else
    {
      Rebuild(payload);
    }

    if (id != _lastSelectedId)
    {
      var gameSelected = id >= 0 && !_origin.TryConsume(id);
      _lastSelectedId = id;
      if (gameSelected)
      {
        _layout.Reveal(BuiltInPanels.Inspector);
      }
    }
  }

  private bool SameShape(InspectorStatePayload payload)
  {
    if (payload.Sections.Count != Sections.Count)
    {
      return false;
    }

    for (var i = 0; i < Sections.Count; i++)
    {
      if (Sections[i].TypeName != payload.Sections[i].TypeName)
      {
        return false;
      }
    }

    return true;
  }

  // Builds the sections again, keeping each type's open or closed state across selections.
  private void Rebuild(InspectorStatePayload payload)
  {
    var sections = new List<InspectorSectionViewModel>(payload.Sections.Count);
    for (var i = 0; i < payload.Sections.Count; i++)
    {
      var section = payload.Sections[i];
      var index = (ushort)i;
      var grid = new PropertyGridViewModel(
          (property, value) => SetPropertyAsync(index, (ushort)property, value));
      grid.Update(section.Properties);
      var view = new InspectorSectionViewModel(i, section, grid);
      if (_expanded.TryGetValue(section.TypeName, out var expanded))
      {
        view.IsExpanded = expanded;
      }

      view.PropertyChanged += OnSectionChanged;
      sections.Add(view);
    }

    Sections.ReplaceAll(sections);
  }

  private void OnSectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (sender is InspectorSectionViewModel section &&
        e.PropertyName == nameof(InspectorSectionViewModel.IsExpanded))
    {
      _expanded[section.TypeName] = section.IsExpanded;
    }
  }

  private Task SetPropertyAsync(ushort section, ushort property, PropertyValue value) =>
      Session is { } session && SelectedId >= 0
          ? session.SetPropertyAsync(SelectedId, section, property, value)
          : Task.CompletedTask;

  // Asks for a thumbnail once per selected object while the preview is open, and shows what
  // arrived.
  private void SyncPreview()
  {
    var wanted = HasTexture && PreviewExpanded ? SelectedId : -1;
    if (wanted != _previewRequestedFor)
    {
      _previewRequestedFor = wanted;
      Preview.Clear();
      if (wanted >= 0 && Session is { } session)
      {
        Preview.BeginWaiting();
        _ = session.RequestInspectorThumbnailAsync(wanted);
      }
    }

    if (wanted >= 0 && Session is { } active &&
        active.InspectorThumbnails.TryGet(wanted, out var thumbnail))
    {
      Preview.Show(thumbnail);
    }
  }
}
