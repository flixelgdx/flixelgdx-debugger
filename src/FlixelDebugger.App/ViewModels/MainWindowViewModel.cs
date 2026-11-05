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
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Adb;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The ViewModel of the main window: the server, the list of games, the menus, the status bar, and
/// the layout.
/// </summary>
/// <remarks>
/// <para>
/// This class is the conductor. It starts the server, keeps the list of connected games, tells the
/// panels which game is active (through <see cref="IActiveSession"/>), and turns menu clicks into
/// Core actions. It holds no logic of its own that Core already has, and it never touches a
/// control: everything goes through small interfaces so the tests can run it without a window or a
/// socket.
/// </para>
/// <para>
/// <b>Active game.</b> The first game that connects is selected automatically. When the active game
/// disconnects, it stays selected with its last data, and the window says "Disconnected". A new
/// connection with the same title and device is treated as the same game coming back and is
/// selected automatically. A game that is not selected and has disconnected is dropped from the
/// list on the next status tick, so choosing another game never changes the list while the dropdown
/// is still handling the choice.
/// </para>
/// <para>
/// <b>Threads.</b> The host raises its events on background threads, so every handler moves to the
/// UI thread with <see cref="IUiScheduler.Post"/> first.
/// </para>
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
  /// <summary>How often the status bar and the game list are refreshed.</summary>
  public static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);

  private readonly IDebugHost _host;
  private readonly IAdbClient _adb;
  private readonly ISettingsStore _settings;
  private readonly IPanelRegistry _registry;
  private readonly IPanelLayout _layout;
  private readonly IActiveSession _active;
  private readonly IUiScheduler _ui;
  private readonly INotificationService _notifier;
  private readonly IDialogService _dialogs;
  private bool _syncingControls;

  private SessionItemViewModel? _selectedSession;

  [ObservableProperty]
  private string? _serverError;

  [ObservableProperty]
  private bool _hasServerError;

  [ObservableProperty]
  private bool _hasActiveSession;

  [ObservableProperty]
  private bool _isActiveDisconnected;

  [ObservableProperty]
  private string _statusText = "- FPS | - MB | Running";

  [ObservableProperty]
  private string _gameText = "No game connected";

  [ObservableProperty]
  private string _pingText = "- ms";

  [ObservableProperty]
  private string _listenText = string.Empty;

  [ObservableProperty]
  private bool _showHitboxes;

  [ObservableProperty]
  private bool _isPaused;

  [ObservableProperty]
  private bool _freeCamera;

  /// <summary>
  /// Creates the main ViewModel and starts following the host, the layout, and the registry.
  /// </summary>
  /// <param name="host">The server the games connect to.</param>
  /// <param name="adb">Forwards the port to Android devices.</param>
  /// <param name="settings">The saved settings.</param>
  /// <param name="registry">The list of panels.</param>
  /// <param name="layout">The arrangement of the panels.</param>
  /// <param name="active">Receives the game every panel should show.</param>
  /// <param name="ui">Moves work to the UI thread and runs the status timer.</param>
  /// <param name="notifier">Shows messages that do not block the window.</param>
  /// <param name="dialogs">Opens the settings and device dialogs.</param>
  public MainWindowViewModel(
      IDebugHost host,
      IAdbClient adb,
      ISettingsStore settings,
      IPanelRegistry registry,
      IPanelLayout layout,
      IActiveSession active,
      IUiScheduler ui,
      INotificationService notifier,
      IDialogService dialogs)
  {
    ArgumentNullException.ThrowIfNull(host);
    ArgumentNullException.ThrowIfNull(adb);
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(registry);
    ArgumentNullException.ThrowIfNull(layout);
    ArgumentNullException.ThrowIfNull(active);
    ArgumentNullException.ThrowIfNull(ui);
    ArgumentNullException.ThrowIfNull(notifier);
    ArgumentNullException.ThrowIfNull(dialogs);
    _host = host;
    _adb = adb;
    _settings = settings;
    _registry = registry;
    _layout = layout;
    _active = active;
    _ui = ui;
    _notifier = notifier;
    _dialogs = dialogs;

    host.SessionConnected += (_, e) => ui.Post(() => AddSession(e.Session));
    host.SessionDisconnected += (_, _) => ui.Post(RefreshStatus);
    layout.RootChanged += (_, _) =>
    {
      OnPropertyChanged(nameof(Layout));
      SyncPanelItems();
    };
    layout.VisibilityChanged += (_, e) => PanelItemFor(e.PanelId)?.SyncFromLayout(e.Visible);
    registry.Changed += (_, _) => SyncPanelItems();

    _ = layout.Root;
    SyncPanelItems();
    foreach (var session in host.Sessions)
    {
      AddSession(session);
    }

    ui.Every(StatusInterval, OnStatusTick);
    RefreshStatus();
  }

  /// <summary>Gets the connected games, including the active one if it disconnected.</summary>
  public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

  /// <summary>
  /// Gets or sets the game the dropdown shows, which is also the game every panel follows.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Setting a different entry makes it the active game. Setting <see langword="null"/> is ignored
  /// while the current entry is still in <see cref="Sessions"/>: the ViewModel is the only one that
  /// knows the list is not empty, and a ComboBox reports <see langword="null"/> whenever it is in
  /// the middle of rebuilding its items. Believing it would reset every panel to the empty view for
  /// a moment, or for good. After an ignored <see langword="null"/>, the property change is raised
  /// again so the control shows the real selection.
  /// </para>
  /// </remarks>
  public SessionItemViewModel? SelectedSession
  {
    get => _selectedSession;
    set
    {
      if (value is null && _selectedSession is not null && Sessions.Contains(_selectedSession))
      {
        _ui.Post(() => OnPropertyChanged(nameof(SelectedSession)));
        return;
      }

      if (SetProperty(ref _selectedSession, value))
      {
        ApplySelection(value);
      }
    }
  }

  /// <summary>Gets the check items of the Debug menu, one per panel.</summary>
  public ObservableCollection<PanelMenuItemViewModel> PanelItems { get; } = [];

  /// <summary>Gets the root of the dock layout for the window's dock control.</summary>
  public IRootDock? Layout => _layout.Root;

  /// <summary>
  /// Gets whether no game is in the list, which is when the how-to-connect hint is shown.
  /// </summary>
  public bool HasNoSessions => Sessions.Count == 0;

  /// <summary>
  /// Restores the saved layout, then starts the server.
  /// </summary>
  /// <returns>A task that completes when the server was started or failed to start.</returns>
  public async Task InitializeAsync()
  {
    RestoreLayout();
    _host.Configure(_settings.Current.Port, _settings.Current.ListenOnAllInterfaces);
    await StartServerAsync();
  }

  /// <summary>
  /// Saves the layout and the visibility of every panel to the settings file.
  /// </summary>
  public void SaveLayout()
  {
    var settings = _settings.Current;
    settings.WindowLayout = _layout.Save();
    settings.PanelVisibility.Clear();
    foreach (var definition in _registry.Definitions)
    {
      settings.PanelVisibility[definition.Id] = _layout.IsOpen(definition.Id);
    }

    _settings.Save();
  }

  /// <summary>
  /// Replaces the layout with the saved one, then applies the saved visibility.
  /// </summary>
  /// <remarks>
  /// If the saved layout is missing or unreadable, the default arrangement stays. The visibility
  /// list is applied in both cases, so hiding a panel survives even when the layout text could not
  /// be used.
  /// </remarks>
  public void RestoreLayout()
  {
    _layout.Restore(_settings.Current.WindowLayout);
    foreach (var (id, visible) in _settings.Current.PanelVisibility)
    {
      // The saved flag says whether the tab was open, not whether it was in front, so a tab behind
      // another one is left where the restored layout put it.
      if (_layout.IsOpen(id) != visible)
      {
        _layout.SetVisible(id, visible);
      }
    }

    SyncPanelItems();
  }

  /// <summary>
  /// Saves the layout and stops the server.
  /// </summary>
  /// <returns>A task that completes when the server is stopped.</returns>
  public async Task ShutdownAsync()
  {
    SaveLayout();
    await _host.StopAsync();
  }

  /// <summary>
  /// Applies new server settings: saves them and restarts the server if anything changed.
  /// </summary>
  /// <param name="port">The new port, from 1 to 65535.</param>
  /// <param name="listenOnAllInterfaces">Whether to accept connections from the network.</param>
  /// <returns>A task that completes when the server runs again or failed to start.</returns>
  public async Task ApplySettingsAsync(int port, bool listenOnAllInterfaces)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
    var settings = _settings.Current;
    var changed = settings.Port != port || settings.ListenOnAllInterfaces != listenOnAllInterfaces;
    settings.Port = port;
    settings.ListenOnAllInterfaces = listenOnAllInterfaces;
    if (!_settings.Save())
    {
      _notifier.Show(NotificationLevel.Warning, "The settings could not be saved to disk. They apply until you close the app.");
    }

    if (!changed && _host.IsRunning)
    {
      return;
    }

    await _host.StopAsync();
    _host.Configure(port, listenOnAllInterfaces);
    await StartServerAsync();
  }

  [RelayCommand]
  private async Task StartServerAsync()
  {
    if (_host.IsRunning)
    {
      ServerError = null;
      return;
    }

    try
    {
      await _host.StartAsync();
      ServerError = null;
    }
    catch (PortInUseException)
    {
      ServerError = $"Port {_host.Port} is already in use. Close the other debugger or change the port in Settings.";
    }
    catch (Exception e) when (e is InvalidOperationException or System.Net.Sockets.SocketException or IOException)
    {
      ServerError = $"The server could not start: {e.Message}";
    }

    ListenText = _host.IsRunning ? _host.ListenAddress : "Server stopped";
  }

  [RelayCommand]
  private async Task ConnectAndroidAsync()
  {
    var port = _host.Port;
    var result = await _adb.ReverseAsync(port);
    if (result.Status == AdbStatus.ManyDevices)
    {
      var ready = result.Devices.Where(d => d.State == AdbDeviceState.Ready).ToList();
      var serial = await _dialogs.PickDeviceAsync(ready);
      if (serial is null)
      {
        return;
      }

      result = await _adb.ReverseAsync(port, serial);
    }

    _notifier.Show(result.Success ? NotificationLevel.Success : NotificationLevel.Warning, result.Message);
  }

  [RelayCommand]
  private async Task OpenSettingsAsync()
  {
    var dialog = new SettingsViewModel(_settings.Current.Port, _settings.Current.ListenOnAllInterfaces);
    if (await _dialogs.ShowSettingsAsync(dialog))
    {
      await ApplySettingsAsync(dialog.Port, dialog.ListenOnAllInterfaces);
    }
  }

  [RelayCommand]
  private void ResetLayout() => _layout.Reset();

  [RelayCommand]
  private void ToggleHitboxes() => ShowHitboxes = !ShowHitboxes;

  [RelayCommand]
  private void TogglePause() => IsPaused = !IsPaused;

  [RelayCommand]
  private void ToggleFreeCamera() => FreeCamera = !FreeCamera;

  [RelayCommand]
  private async Task StepFrameAsync()
  {
    if (_active.Current is { } session)
    {
      await session.StepAsync();
    }
  }

  [RelayCommand]
  private async Task ClearLogAsync()
  {
    if (_active.Current is { } session)
    {
      await session.ClearLogsAsync();
    }
  }

  // Closed entries are not dropped here. This runs inside the dropdown's own selection change, and
  // removing an item from its list at that moment makes the control report "no selection", which
  // would clear the panels.
  private void ApplySelection(SessionItemViewModel? value)
  {
    _active.Current = value?.Session;
    if (value is not null)
    {
      RegisterCustomPanels(value.Session);
    }

    RefreshStatus();
  }

  private void OnStatusTick()
  {
    PruneClosed();
    RefreshStatus();
  }

  partial void OnShowHitboxesChanged(bool value)
  {
    if (!_syncingControls && _active.Current is { } session)
    {
      _ = session.SetHitboxesAsync(value);
    }
  }

  partial void OnIsPausedChanged(bool value)
  {
    if (!_syncingControls && _active.Current is { } session)
    {
      _ = session.SetPausedAsync(value);
    }
  }

  partial void OnFreeCameraChanged(bool value)
  {
    if (!_syncingControls && _active.Current is { } session)
    {
      _ = session.SetFreeCamAsync(value);
    }
  }

  private void AddSession(IGameSession session)
  {
    if (Sessions.Any(i => ReferenceEquals(i.Session, session)))
    {
      return;
    }

    session.StateChanged += (_, _) => _ui.Post(RefreshStatus);
    session.PanelListChanged += (_, _) => _ui.Post(() => RegisterCustomPanels(session));
    session.PanelUpdated += (_, e) =>
    {
      if (e.Kind == PanelKind.Controls)
      {
        _ui.Post(RefreshStatus);
      }
    };

    var item = new SessionItemViewModel(session);
    var replacesSelected = SelectedSession is { Session.State: SessionState.Closed } selected && selected.IsSameGameAs(session);
    Sessions.Add(item);
    if (SelectedSession is null || replacesSelected)
    {
      SelectedSession = item;
    }

    // The same game reconnecting replaces its old, closed entry.
    for (var i = Sessions.Count - 1; i >= 0; i--)
    {
      var other = Sessions[i];
      if (!ReferenceEquals(other, item) && other.Session.State == SessionState.Closed && other.IsSameGameAs(session))
      {
        Sessions.RemoveAt(i);
      }
    }

    RegisterCustomPanels(session);
    OnPropertyChanged(nameof(HasNoSessions));
    RefreshStatus();
  }

  // Closed games stay in the list only while they are selected, so the list does not fill up with
  // old entries. This runs from the status timer, never from inside a selection change, so the
  // dropdown is not mid-update.
  private void PruneClosed()
  {
    for (var i = Sessions.Count - 1; i >= 0; i--)
    {
      var item = Sessions[i];
      if (!ReferenceEquals(item, SelectedSession) && item.Session.State == SessionState.Closed)
      {
        Sessions.RemoveAt(i);
      }
    }

    OnPropertyChanged(nameof(HasNoSessions));
  }

  private void RegisterCustomPanels(IGameSession session)
  {
    foreach (var panel in session.Panels)
    {
      if (panel.Kind == PanelKind.Custom)
      {
        _registry.RegisterCustom(panel.Id, panel.Name);
      }
    }
  }

  private PanelMenuItemViewModel? PanelItemFor(string id)
  {
    foreach (var item in PanelItems)
    {
      if (item.Id == id)
      {
        return item;
      }
    }

    return null;
  }

  private void SyncPanelItems()
  {
    foreach (var definition in _registry.Definitions)
    {
      var visible = _layout.IsVisible(definition.Id);
      if (PanelItemFor(definition.Id) is { } existing)
      {
        existing.SyncFromLayout(visible);
      }
      else
      {
        PanelItems.Add(new PanelMenuItemViewModel(definition.Id, definition.Title, visible, SetPanelVisible));
      }
    }
  }

  private void SetPanelVisible(string id, bool visible) => _layout.SetVisible(id, visible);

  private void RefreshStatus()
  {
    foreach (var item in Sessions)
    {
      item.Refresh();
    }

    var session = SelectedSession?.Session;
    HasActiveSession = session is not null;
    IsActiveDisconnected = session is { State: SessionState.Closed };
    ListenText = _host.IsRunning ? _host.ListenAddress : "Server stopped";
    HasServerError = ServerError is not null;
    if (session is null)
    {
      GameText = "No game connected";
      PingText = "- ms";
      StatusText = "- FPS | - MB | Running";
      SyncControls(null);
      return;
    }

    var stats = session.Stats.LatestPayload;
    var controls = session.Controls;
    var paused = controls?.Paused ?? stats?.Paused ?? false;
    var fps = stats is null ? "-" : stats.Fps.ToString("0", CultureInfo.InvariantCulture);
    var heap = stats is null ? "-" : stats.HeapMb.ToString("0.0", CultureInfo.InvariantCulture);
    GameText = IsActiveDisconnected
        ? $"{session.Title} ({session.Platform}) - Disconnected"
        : $"{session.Title} ({session.Platform})";
    PingText = IsActiveDisconnected || session.PingMs is null
        ? "- ms"
        : string.Create(CultureInfo.InvariantCulture, $"{session.PingMs:0} ms");
    StatusText = $"{fps} FPS | {heap} MB | {(paused ? "PAUSED" : "Running")}";
    SyncControls(controls);
  }

  // Copies the game's controls state into the menu check items without sending anything back.
  private void SyncControls(Protocol.Panels.ControlsPayload? controls)
  {
    _syncingControls = true;
    ShowHitboxes = controls?.Hitboxes ?? false;
    IsPaused = controls?.Paused ?? false;
    FreeCamera = controls?.FreeCam ?? false;
    _syncingControls = false;
  }

  partial void OnServerErrorChanged(string? value) => HasServerError = value is not null;
}
