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

using System.Diagnostics;
using System.Net.WebSockets;
using FlixelDebugger.Core.Completion;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// One connected game: the handshake, the latest data of every panel, and the typed actions you can
/// send back.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DebugServer"/> creates one session per WebSocket connection, so several games can
/// be connected at once. When a game disconnects and reconnects, the new connection is a new
/// session and starts empty, exactly as the protocol says: the handshake starts over and the game
/// re-sends everything.
/// </para>
/// <para>
/// <b>Threads.</b> The receive loop runs on a background task. Every event is raised on that task,
/// and the properties holding the latest data can be read from any thread. A UI should handle an
/// event by copying what it needs and moving to its own thread. Core never touches a UI thread.
/// </para>
/// <para>
/// <b>Panels.</b> The game numbers its panels, but the numbers change whenever panels are
/// registered or removed. The session therefore tracks panels by <i>id</i>: a
/// <see cref="PanelState"/> survives a new panel list, and only its <see cref="PanelState.Index"/>
/// changes.
/// </para>
/// <para>
/// <b>Sending.</b> Sends are serialized: only one send runs at a time. Every action helper returns
/// <see langword="false"/> instead of throwing when the panel does not exist (yet) or the
/// connection is closed, because both are normal races in a live debugging session.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// server.SessionConnected += (_, e) =>
/// {
///   e.Session.PanelUpdated += (_, p) => Dispatch(() => Refresh(p.Panel.Id));
///   _ = e.Session.SetPausedAsync(true);
/// };
/// </code>
/// </example>
public sealed class GameSession : ICommandRunner, IAsyncDisposable
{
  /// <summary>
  /// The largest message the session accepts, in bytes (16 MB, as the protocol specifies).
  /// </summary>
  public const int MaxMessageBytes = 16 * 1024 * 1024;

  private const int InitialBufferBytes = 16 * 1024;
  private const int ShrinkAboveBytes = 1024 * 1024;
  private const int PendingPings = 8;

  private static readonly TimeSpan _closeGrace = TimeSpan.FromSeconds(1);

  private readonly WebSocket _socket;
  private readonly DebugServerOptions _options;
  private readonly SemaphoreSlim _sendLock = new(1, 1);
  private readonly MessageWriter _writer = new();
  private readonly MessageDecoder _decoder = new();
  private readonly CancellationTokenSource _abort = new();
  private readonly Dictionary<ushort, PanelState> _byIndex = [];
  private readonly long[] _pingNonces = new long[PendingPings];
  private readonly long[] _pingStamps = new long[PendingPings];
  private readonly Lock _pingGate = new();

  private volatile PanelState[] _panels = [];
  private volatile Dictionary<string, PanelState> _byId = [];
  private volatile Hello? _hello;
  private volatile string? _closeReason;
  private volatile WatchesPayload? _watches;
  private volatile TrackersPayload? _trackers;
  private volatile ControlsPayload? _controls;
  private volatile ConsoleCommandsPayload? _consoleCommands;
  private volatile ConsoleResultPayload? _consoleResult;
  private volatile SceneTree _scene = SceneTree.Empty;
  private volatile InspectorStatePayload? _inspector;
  private volatile TexturesListPayload? _textures;
  private volatile SavePayload? _save;
  private volatile ViewportPayload? _viewport;
  private volatile CaptureStatePayload? _captureState;
  private volatile CaptureImagePayload? _captureImage;
  private volatile InputStatePayload? _inputState;
  private double _pingMs = double.NaN;
  private Task? _pingTask;
  private long _protocolErrors;
  private int _state;
  private int _pingSlot;

  internal GameSession(WebSocket socket, string remoteAddress, DebugServerOptions options)
  {
    _socket = socket;
    _options = options;
    RemoteAddress = remoteAddress;
    ConnectedAt = DateTimeOffset.UtcNow;
  }

  /// <summary>
  /// Raised when <see cref="State"/> changes. It is raised on a background thread.
  /// </summary>
  public event EventHandler? StateChanged;

  /// <summary>
  /// Raised when the game sent a new panel list. It is raised on a background thread.
  /// </summary>
  public event EventHandler? PanelListChanged;

  /// <summary>
  /// Raised after a panel payload was stored. It is raised on a background thread.
  /// </summary>
  public event EventHandler<PanelUpdatedEventArgs>? PanelUpdated;

  /// <summary>
  /// Raised when a new <see cref="PingMs"/> value was measured. It is raised on a background
  /// thread.
  /// </summary>
  public event EventHandler? PingUpdated;

  /// <summary>Gets a unique id for this connection.</summary>
  public Guid Id { get; } = Guid.NewGuid();

  /// <summary>Gets the address the game connected from.</summary>
  public string RemoteAddress { get; }

  /// <summary>Gets when the WebSocket connection was accepted.</summary>
  public DateTimeOffset ConnectedAt { get; }

  /// <summary>Gets where the connection is in its life.</summary>
  public SessionState State => (SessionState)Volatile.Read(ref _state);

  /// <summary>
  /// Gets the fields of the game's HELLO, or <see langword="null"/> before it arrives.
  /// </summary>
  public Hello? Hello => _hello;

  /// <summary>
  /// Gets a short name for lists: the game title with the platform, or the address before HELLO
  /// arrives.
  /// </summary>
  public string DisplayName => _hello is { } hello
      ? (hello.GameTitle.Length > 0
          ? $"{hello.GameTitle} ({hello.Platform})"
          : $"Game ({hello.Platform})")
      : RemoteAddress;

  /// <summary>
  /// Gets why the connection ended, in plain words, or <see langword="null"/> while it is open.
  /// </summary>
  public string? CloseReason => _closeReason;

  /// <summary>
  /// Gets the last measured round trip time of a PING in milliseconds, or <see langword="null"/> if
  /// none was measured yet.
  /// </summary>
  public double? PingMs
  {
    get
    {
      var value = Volatile.Read(ref _pingMs);
      return double.IsNaN(value) ? null : value;
    }
  }

  /// <summary>
  /// Gets how many received messages were dropped because they could not be decoded.
  /// </summary>
  public long ProtocolErrors => Interlocked.Read(ref _protocolErrors);

  /// <summary>Gets the panels the game currently has, ordered by index.</summary>
  public IReadOnlyList<PanelState> Panels => _panels;

  /// <summary>Gets the stats history (120 samples per field).</summary>
  public StatsHistory Stats { get; } = new();

  /// <summary>Gets the ping times measured for this game, for the network graph.</summary>
  public PingHistory Pings { get; } = new();

  /// <summary>Gets the log lines (the newest 500) with their filters.</summary>
  public LogStore Logs { get; } = new();

  /// <summary>Gets the thumbnails of textures, by texture id.</summary>
  public ThumbnailStore TextureThumbnails { get; } = new();

  /// <summary>Gets the thumbnails of inspected objects, by object id.</summary>
  public ThumbnailStore InspectorThumbnails { get; } = new();

  /// <summary>Gets the latest watches, or <see langword="null"/> before the first one.</summary>
  public WatchesPayload? Watches => _watches;

  /// <summary>Gets the latest trackers, or <see langword="null"/> before the first one.</summary>
  public TrackersPayload? Trackers => _trackers;

  /// <summary>
  /// Gets the latest controls state, or <see langword="null"/> before the first one.
  /// </summary>
  public ControlsPayload? Controls => _controls;

  /// <summary>
  /// Gets the latest console command list, or <see langword="null"/> before the first one.
  /// </summary>
  public ConsoleCommandsPayload? ConsoleCommands => _consoleCommands;

  /// <summary>
  /// Gets the result of the last console command, or <see langword="null"/> if none ran.
  /// </summary>
  public ConsoleResultPayload? ConsoleResult => _consoleResult;

  /// <summary>
  /// Gets the latest scene tree snapshot. It is an empty tree before the first one.
  /// </summary>
  public SceneTree Scene => _scene;

  /// <summary>
  /// Gets the latest inspector state, or <see langword="null"/> before the first one.
  /// </summary>
  public InspectorStatePayload? Inspector => _inspector;

  /// <summary>
  /// Gets the latest textures list, or <see langword="null"/> before the first one.
  /// </summary>
  public TexturesListPayload? Textures => _textures;

  /// <summary>Gets the latest save data, or <see langword="null"/> before the first one.</summary>
  public SavePayload? Save => _save;

  /// <summary>
  /// Gets the latest viewport map, or <see langword="null"/> before the first one.
  /// </summary>
  public ViewportPayload? Viewport => _viewport;

  /// <summary>
  /// Gets whether the game can take pictures of its screen, or <see langword="null"/> when it
  /// has not said so (a game older than protocol 1.1 never does).
  /// </summary>
  public bool? CaptureSupported => _captureState?.Supported;

  /// <summary>
  /// Gets the latest captured picture, or <see langword="null"/> before the first one.
  /// </summary>
  public CaptureImagePayload? CaptureImage => _captureImage;

  /// <summary>
  /// Gets which kinds of input the game accepts, or <see langword="null"/> when it has not said
  /// so (a game older than protocol 1.1 never does).
  /// </summary>
  public InputSupport? InputFlags => _inputState?.Flags;

  /// <summary>
  /// Looks up a panel by id.
  /// </summary>
  /// <param name="id">
  /// The panel id, for example <see cref="BuiltInPanels.Stats"/> or <c>custom.player</c>.
  /// </param>
  /// <param name="panel">The panel, when it exists.</param>
  /// <returns><see langword="true"/> if the game currently has a panel with that id.</returns>
  public bool TryGetPanel(string id, out PanelState panel)
  {
    ArgumentNullException.ThrowIfNull(id);
    return _byId.TryGetValue(id, out panel!);
  }

  /// <summary>
  /// Sends one protocol message. Sends are serialized: if another send is running, this one waits.
  /// </summary>
  /// <param name="message">The message to send.</param>
  /// <param name="cancellationToken">A token that cancels the wait and the send.</param>
  /// <returns>
  /// <see langword="true"/> if the message was sent; <see langword="false"/> if the connection is
  /// closed.
  /// </returns>
  public async Task<bool> SendAsync(
      ProtocolMessage message, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    var locked = false;
    try
    {
      await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
      locked = true;
      if (_socket.State != WebSocketState.Open)
      {
        return false;
      }

      // The writer is reused for every message, which is safe because only one send holds the lock.
      message.Write(_writer);
      await _socket.SendAsync(
          _writer.Finish(),
          WebSocketMessageType.Binary,
          true,
          cancellationToken).ConfigureAwait(false);
      return true;
    }
    catch (Exception e)
        when (e is WebSocketException or ObjectDisposedException or InvalidOperationException)
    {
      return false;
    }
    finally
    {
      if (locked)
      {
        _sendLock.Release();
      }
    }
  }

  /// <summary>
  /// Sends an action to the panel with the given id.
  /// </summary>
  /// <param name="panelId">The panel id.</param>
  /// <param name="action">The action to send.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>
  /// <see langword="true"/> if it was sent; <see langword="false"/> if the game has no such panel
  /// or the connection is closed.
  /// </returns>
  public Task<bool> SendActionAsync(
      string panelId, ActionPayload action, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(panelId);
    ArgumentNullException.ThrowIfNull(action);
    if (!_byId.TryGetValue(panelId, out var panel) || panel.Index < 0)
    {
      return Task.FromResult(false);
    }

    return SendAsync(new PanelAction((ushort)panel.Index, action), cancellationToken);
  }

  /// <summary>Pauses or resumes the game.</summary>
  /// <param name="paused"><see langword="true"/> to pause.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Controls, new ControlsSetPausedAction(paused), cancellationToken);

  /// <summary>Advances the paused game by one frame.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> StepAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Controls, new ControlsStepAction(), cancellationToken);

  /// <summary>Shows or hides hitboxes in the game.</summary>
  /// <param name="hitboxes"><see langword="true"/> to show them.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetHitboxesAsync(
      bool hitboxes, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Controls, new ControlsSetHitboxesAction(hitboxes), cancellationToken);

  /// <summary>Turns the game's free camera on or off.</summary>
  /// <param name="freeCam"><see langword="true"/> to turn it on.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Controls, new ControlsSetFreeCamAction(freeCam), cancellationToken);

  /// <summary>
  /// Sets how often the game samples stats, in Hz. The game clamps it to 1 to 30.
  /// </summary>
  /// <param name="rate">The sample rate in Hz.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetRateAsync(byte rate, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Controls, new ControlsSetRateAction(rate), cancellationToken);

  /// <summary>Overrides the game's log level.</summary>
  /// <param name="level">
  /// The level, or <see cref="LogLevel.Default"/> to use the game's own setting.
  /// </param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetLogLevelAsync(
      LogLevel level, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Controls, new ControlsSetLogLevelAction(level), cancellationToken);

  /// <summary>Clears the game's log and the lines stored here.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public async Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default)
  {
    var sent = await SendActionAsync(
        BuiltInPanels.Logs, new LogsClearAction(), cancellationToken).ConfigureAwait(false);
    Logs.Clear();
    return sent;
  }

  /// <summary>Runs a console line in the game.</summary>
  /// <param name="line">The line to run. The game rejects lines over 500 characters.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  /// <exception cref="ArgumentException">
  /// The line is longer than <see cref="ProtocolConstants.MaxCommandChars"/>.
  /// </exception>
  public Task<bool> RunCommandAsync(string line, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(line);
    if (line.Length > ProtocolConstants.MaxCommandChars)
    {
      throw new ArgumentException(
          $"A console line may have at most {ProtocolConstants.MaxCommandChars} characters.",
          nameof(line));
    }

    return SendActionAsync(BuiltInPanels.Console, new ConsoleRunAction(line), cancellationToken);
  }

  /// <summary>Asks the game for a fresh scene tree snapshot, even if nothing changed.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestSceneTreeAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Scene, new SceneRequestTreeAction(), cancellationToken);

  /// <summary>Selects an object in the inspector by its id.</summary>
  /// <param name="id">The object id, for example from the scene tree.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SelectAsync(int id, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Inspector, new InspectorSelectAction(id), cancellationToken);

  /// <summary>
  /// Selects an object in the inspector by its scene path, for example <c>state.3.0</c>.
  /// </summary>
  /// <param name="path">The scene path.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SelectPathAsync(string path, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(path);
    return SendActionAsync(
        BuiltInPanels.Inspector, new InspectorSelectPathAction(path), cancellationToken);
  }

  /// <summary>Selects the parent of the inspector's selection.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SelectParentAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Inspector, new InspectorSelectParentAction(), cancellationToken);

  /// <summary>Clears the inspector's selection.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ClearSelectionAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Inspector, new InspectorClearAction(), cancellationToken);

  /// <summary>Moves the game camera to the inspector's selection.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> GoToSelectionAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Inspector, new InspectorGoToAction(), cancellationToken);

  /// <summary>Edits one property of the inspector's selection.</summary>
  /// <param name="id">
  /// The id of the selected object. The game ignores the edit if it is no longer selected.
  /// </param>
  /// <param name="sectionIndex">The position of the section in the latest inspector state.</param>
  /// <param name="propertyIndex">The position of the row inside the section.</param>
  /// <param name="value">The new value.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetPropertyAsync(
      int id,
      ushort sectionIndex,
      ushort propertyIndex,
      PropertyValue value,
      CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Inspector,
          new InspectorSetPropertyAction(id, sectionIndex, propertyIndex, value),
          cancellationToken);

  /// <summary>
  /// Asks the game for a thumbnail of an inspected object. The game may never answer.
  /// </summary>
  /// <param name="id">The object id.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestInspectorThumbnailAsync(
      int id, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Inspector, new InspectorRequestThumbnailAction(id), cancellationToken);

  /// <summary>Asks the game for a thumbnail of a texture. The game may never answer.</summary>
  /// <param name="id">The texture id from the textures list.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestThumbnailAsync(int id, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Textures, new TexturesRequestThumbnailAction(id), cancellationToken);

  /// <summary>Changes one save value from text, keeping its type.</summary>
  /// <param name="path">
  /// The dotted path of the value. The game accepts at most 200 characters.
  /// </param>
  /// <param name="text">The new value as text. The game accepts at most 200 characters.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveSetAsync(
      string path, string text, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(path);
    ArgumentNullException.ThrowIfNull(text);
    return SendActionAsync(BuiltInPanels.Save, new SaveSetAction(path, text), cancellationToken);
  }

  /// <summary>Resets one save key (or a whole branch) to the game's default.</summary>
  /// <param name="path">The dotted path.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveResetKeyAsync(string path, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(path);
    return SendActionAsync(BuiltInPanels.Save, new SaveResetKeyAction(path), cancellationToken);
  }

  /// <summary>Writes the save data to storage now.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveNowAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Save, new SaveSaveNowAction(), cancellationToken);

  /// <summary>
  /// Erases all save data and the stored save. Ask the person to confirm before calling this.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveResetAllAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Save, new SaveResetAllAction(), cancellationToken);

  /// <summary>Switches the save panel to another save of the game's save list.</summary>
  /// <param name="index">
  /// The position of the save in <see cref="SavePayload.Saves"/>. The game ignores an index that
  /// is out of range.
  /// </param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveSelectAsync(ushort index, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Save, new SaveSelectAction(index), cancellationToken);

  /// <summary>Chooses the camera the viewport map shows and camera actions act on.</summary>
  /// <param name="index">A position in the game's camera list.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportSetCameraAsync(
      byte index, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Viewport, new ViewportSetCameraAction(index), cancellationToken);

  /// <summary>
  /// Selects the topmost object drawn at a point of the viewport, like Alt + Click in the game.
  /// </summary>
  /// <param name="viewX">The x coordinate in view units.</param>
  /// <param name="viewY">The y coordinate in view units.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportPickAsync(
      float viewX, float viewY, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Viewport, new ViewportPickAction(viewX, viewY), cancellationToken);

  /// <summary>Moves an object by a distance in world units.</summary>
  /// <param name="id">The object id.</param>
  /// <param name="dx">The horizontal distance in world units.</param>
  /// <param name="dy">The vertical distance in world units.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportMoveAsync(
      int id, float dx, float dy, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Viewport, new ViewportMoveAction(id, dx, dy), cancellationToken);

  /// <summary>Moves the viewport's camera by a distance in view units.</summary>
  /// <param name="dx">The horizontal distance; positive moves the camera right.</param>
  /// <param name="dy">The vertical distance; positive moves the camera down.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportPanAsync(
      float dx, float dy, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Viewport, new ViewportPanAction(dx, dy), cancellationToken);

  /// <summary>Zooms the viewport's camera and keeps one point of the view still.</summary>
  /// <param name="factor">The zoom multiplier, for example 1.25 for one wheel notch in.</param>
  /// <param name="anchorViewX">The x coordinate to keep still, in view units.</param>
  /// <param name="anchorViewY">The y coordinate to keep still, in view units.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportZoomAsync(
      float factor,
      float anchorViewX,
      float anchorViewY,
      CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Viewport,
          new ViewportZoomAction(factor, anchorViewX, anchorViewY),
          cancellationToken);

  /// <summary>Turns the free camera off, which restores the camera exactly.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportResetCameraAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Viewport, new ViewportResetCameraAction(), cancellationToken);

  /// <summary>Asks the game for a picture of its next finished frame.</summary>
  /// <param name="requestId">A number that comes back with the picture.</param>
  /// <param name="maxSide">The most pixels the longest side may have (the game clamps it).</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestCaptureAsync(
      int requestId, ushort maxSide, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Capture, new CaptureRequestAction(requestId, maxSide), cancellationToken);

  /// <summary>Presses or releases a key in the game.</summary>
  /// <param name="down"><see langword="true"/> to press.</param>
  /// <param name="key">The framework key code from 0 to 255.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputKeyAsync(
      bool down, int key, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Input, new InputKeyAction(down, key), cancellationToken);

  /// <summary>Moves, presses, or releases the pointer in the game.</summary>
  /// <param name="phase">0 moves, 1 presses, 2 releases.</param>
  /// <param name="button">0 left, 1 right, 2 middle.</param>
  /// <param name="viewX">The x position in view units of the viewport's camera.</param>
  /// <param name="viewY">The y position in view units of the viewport's camera.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputPointerAsync(
      byte phase,
      byte button,
      float viewX,
      float viewY,
      CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Input,
          new InputPointerAction(phase, button, viewX, viewY),
          cancellationToken);

  /// <summary>Turns the mouse wheel in the game.</summary>
  /// <param name="amount">The amount. Positive scrolls down.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputScrollAsync(
      float amount, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Input, new InputScrollAction(amount), cancellationToken);

  /// <summary>Types text in the game.</summary>
  /// <param name="text">The text. The game cuts it at 1000 characters.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputTextAsync(string text, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInPanels.Input, new InputTextAction(text), cancellationToken);

  /// <summary>Clicks the center of an object's viewport box.</summary>
  /// <param name="id">The object id.</param>
  /// <param name="button">0 left, 1 right, 2 middle.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputClickObjectAsync(
      int id, byte button = 0, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInPanels.Input, new InputClickObjectAction(id, button), cancellationToken);

  /// <summary>Presses a button of a custom panel.</summary>
  /// <param name="panelId">The id of the custom panel.</param>
  /// <param name="actionIndex">The button's action index from the panel payload.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> CustomPressAsync(
      string panelId, ushort actionIndex, CancellationToken cancellationToken = default) =>
      SendActionAsync(panelId, new CustomPressAction(actionIndex), cancellationToken);

  /// <summary>Edits one property row of a custom panel.</summary>
  /// <param name="panelId">The id of the custom panel.</param>
  /// <param name="propertyIndex">The position of the row in the panel payload.</param>
  /// <param name="value">The new value.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> CustomSetPropertyAsync(
      string panelId,
      ushort propertyIndex,
      PropertyValue value,
      CancellationToken cancellationToken = default) =>
      SendActionAsync(
          panelId, new CustomSetPropertyAction(propertyIndex, value), cancellationToken);

  /// <summary>
  /// Closes the connection politely.
  /// </summary>
  /// <param name="reason">A short reason the game can show. It is cut to fit a close frame.</param>
  /// <param name="status">The WebSocket close status to send.</param>
  /// <returns>
  /// A task that completes when the close frame was sent. The session ends once the game answers,
  /// or after a second.
  /// </returns>
  public async Task CloseAsync(
      string reason, WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure)
  {
    ArgumentNullException.ThrowIfNull(reason);
    _closeReason ??= reason;
    await CloseSocketAsync(status, reason, loopRunning: true).ConfigureAwait(false);
    try
    {
      // If the game never answers with its own close frame, stop waiting for it.
      _abort.CancelAfter(_closeGrace);
    }
    catch (ObjectDisposedException)
    {
      // The session is already disposed.
    }
  }

  /// <summary>
  /// Closes the connection and releases its resources.
  /// </summary>
  /// <returns>A task that completes when the session is disposed.</returns>
  public ValueTask DisposeAsync()
  {
    _abort.Cancel();
    _socket.Dispose();
    _abort.Dispose();

    // The send lock is not disposed: a send that is still finishing on another thread must be able
    // to release it.
    return ValueTask.CompletedTask;
  }

  // Runs the receive loop until the connection ends. Called by the server on the request's task.
  internal async Task RunAsync(CancellationToken stopping)
  {
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, _abort.Token);
    using var handshake = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
    handshake.CancelAfter(_options.HandshakeTimeout);
    using var pings = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
    try
    {
      await ReceiveLoopAsync(linked.Token, handshake.Token, pings.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested)
    {
      _closeReason ??= "The debugger is shutting down.";
      await CloseSocketAsync(
          WebSocketCloseStatus.EndpointUnavailable,
          "The debugger is shutting down.").ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      // Either the handshake timed out or the session was aborted after a close request.
      if (State == SessionState.Handshaking)
      {
        _closeReason ??= "The game did not send HELLO in time.";
        await CloseSocketAsync(
            WebSocketCloseStatus.PolicyViolation,
            "No HELLO was received in time.").ConfigureAwait(false);
      }
    }
    catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException)
    {
      _closeReason ??= "The connection was lost: " + e.Message;
    }
    finally
    {
      await pings.CancelAsync().ConfigureAwait(false);
      if (_pingTask is { } pingTask)
      {
        await pingTask.ConfigureAwait(false);
      }

      _closeReason ??= PeerClosedReason();
      SetState(SessionState.Closed);
    }
  }

  private void Raise(EventHandler? handler)
  {
    try
    {
      handler?.Invoke(this, EventArgs.Empty);
    }
    catch (Exception e)
    {
      // A faulty subscriber must not break the connection. Leave a trace for the developer instead.
      Debug.WriteLine($"A GameSession event handler threw: {e}");
    }
  }

  private void Raise<T>(EventHandler<T>? handler, T args)
  {
    try
    {
      handler?.Invoke(this, args);
    }
    catch (Exception e)
    {
      // A faulty subscriber must not break the connection. Leave a trace for the developer instead.
      Debug.WriteLine($"A GameSession event handler threw: {e}");
    }
  }

  // A close frame may carry at most 123 bytes of reason text.
  private static string Truncate(string reason)
  {
    const int MaxChars = 120;
    return reason.Length <= MaxChars ? reason : reason[..MaxChars];
  }

  private string PeerClosedReason() => _socket.CloseStatusDescription is { Length: > 0 } text
      ? $"The game closed the connection: {text}"
      : "The game closed the connection.";

  private async Task ReceiveLoopAsync(
      CancellationToken token, CancellationToken handshakeToken, CancellationToken pingToken)
  {
    var buffer = new byte[InitialBufferBytes];
    while (!token.IsCancellationRequested)
    {
      var count = 0;
      var text = false;
      while (true)
      {
        if (count == buffer.Length)
        {
          if (buffer.Length >= MaxMessageBytes)
          {
            _closeReason ??= "The game sent a message over 16 MB.";
            await CloseSocketAsync(
                WebSocketCloseStatus.MessageTooBig, "Message exceeds 16 MB.").ConfigureAwait(false);
            return;
          }

          Array.Resize(ref buffer, Math.Min(buffer.Length * 2, MaxMessageBytes));
        }

        var receiveToken = State == SessionState.Handshaking ? handshakeToken : token;
        var result = await _socket.ReceiveAsync(
            buffer.AsMemory(count), receiveToken).ConfigureAwait(false);
        if (result.MessageType == WebSocketMessageType.Close)
        {
          _closeReason ??= PeerClosedReason();
          await AnswerCloseAsync().ConfigureAwait(false);
          return;
        }

        // Text messages are ignored, so their bytes are never kept: reuse the start of the buffer.
        text = result.MessageType == WebSocketMessageType.Text;
        count = text ? 0 : count + result.Count;
        if (result.EndOfMessage)
        {
          break;
        }
      }

      if (text)
      {
        continue;
      }

      if (!Decode(buffer.AsSpan(0, count), out var message) && State == SessionState.Handshaking)
      {
        await RejectAsync(
            WebSocketCloseStatus.ProtocolError,
            "The first message must be a valid HELLO.").ConfigureAwait(false);
        return;
      }

      if (message is not null && !await HandleAsync(message, pingToken).ConfigureAwait(false))
      {
        return;
      }

      if (buffer.Length > ShrinkAboveBytes)
      {
        buffer = new byte[InitialBufferBytes];
      }
    }
  }

  // Decodes one message. A message that cannot be decoded is counted and dropped.
  private bool Decode(ReadOnlySpan<byte> bytes, out ProtocolMessage? message)
  {
    try
    {
      message = _decoder.Decode(bytes);
      return true;
    }
    catch (ProtocolException)
    {
      Interlocked.Increment(ref _protocolErrors);
      message = null;
      return false;
    }
  }

  // Handles one message. Returns false when the loop should stop.
  private async Task<bool> HandleAsync(ProtocolMessage message, CancellationToken pingToken)
  {
    if (State == SessionState.Handshaking)
    {
      if (message is not Hello hello)
      {
        await RejectAsync(
            WebSocketCloseStatus.ProtocolError,
            "The first message must be HELLO.").ConfigureAwait(false);
        return false;
      }

      return await AcceptHelloAsync(hello, pingToken).ConfigureAwait(false);
    }

    switch (message)
    {
      case PanelList list:
        ApplyPanelList(list);
        break;
      case PanelData data:
        ApplyPanelData(data);
        break;
      case Pong pong:
        ApplyPong(pong);
        break;
    }

    return true;
  }

  private async Task<bool> AcceptHelloAsync(Hello hello, CancellationToken pingToken)
  {
    _hello = hello;

    // Always answer, even on a mismatch: the game logs a clear warning when it sees our version.
    var ack = new HelloAck(ProtocolConstants.VersionMajor, ProtocolConstants.VersionMinor);
    if (!await SendAsync(ack, _abort.Token).ConfigureAwait(false))
    {
      _closeReason ??= "The HELLO_ACK could not be sent.";
      return false;
    }

    if (hello.ProtocolMajor != ProtocolConstants.VersionMajor)
    {
      await RejectAsync(
          WebSocketCloseStatus.PolicyViolation,
          $"Unsupported protocol version {hello.ProtocolMajor}.{hello.ProtocolMinor}; " +
          $"this debugger speaks {ProtocolConstants.VersionMajor}.x.")
          .ConfigureAwait(false);
      return false;
    }

    SetState(SessionState.Live);
    _pingTask = Task.Run(() => PingLoopAsync(pingToken), CancellationToken.None);
    return true;
  }

  private async Task RejectAsync(WebSocketCloseStatus status, string reason)
  {
    _closeReason ??= reason;
    await CloseSocketAsync(status, reason).ConfigureAwait(false);
  }

  private async Task AnswerCloseAsync()
  {
    if (_socket.State != WebSocketState.CloseReceived)
    {
      return;
    }

    var locked = false;
    try
    {
      using var timeout = new CancellationTokenSource(_closeGrace);
      await _sendLock.WaitAsync(timeout.Token).ConfigureAwait(false);
      locked = true;
      await _socket.CloseOutputAsync(
          WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token).ConfigureAwait(false);
    }
    catch (Exception e)
        when (e is WebSocketException or ObjectDisposedException or OperationCanceledException)
    {
      // The game is gone already, so there is nobody left to answer.
    }
    finally
    {
      if (locked)
      {
        _sendLock.Release();
      }
    }
  }

  // Sends a close frame and, when the receive loop is not running, waits briefly for the game's
  // close frame.
  private async Task CloseSocketAsync(
      WebSocketCloseStatus status, string reason, bool loopRunning = false)
  {
    if (_socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
    {
      return;
    }

    var locked = false;
    try
    {
      using var timeout = new CancellationTokenSource(_closeGrace);
      await _sendLock.WaitAsync(timeout.Token).ConfigureAwait(false);
      locked = true;
      if (_socket.State == WebSocketState.Open && !loopRunning)
      {
        await _socket.CloseAsync(status, Truncate(reason), timeout.Token).ConfigureAwait(false);
      }
      else
      {
        // While the loop reads, it receives the game's close frame itself. CloseOutputAsync only
        // sends ours.
        await _socket.CloseOutputAsync(
            status, Truncate(reason), timeout.Token).ConfigureAwait(false);
      }
    }
    catch (Exception e)
        when (e is WebSocketException or ObjectDisposedException or OperationCanceledException)
    {
      _socket.Abort();
    }
    finally
    {
      if (locked)
      {
        _sendLock.Release();
      }
    }
  }

  private void SetState(SessionState state)
  {
    if (Interlocked.Exchange(ref _state, (int)state) != (int)state)
    {
      Raise(StateChanged);
    }
  }

  private async Task PingLoopAsync(CancellationToken token)
  {
    using var timer = new PeriodicTimer(_options.PingInterval);
    try
    {
      while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
      {
        var nonce = Random.Shared.NextInt64();
        lock (_pingGate)
        {
          _pingNonces[_pingSlot] = nonce;
          _pingStamps[_pingSlot] = Stopwatch.GetTimestamp();
          _pingSlot = (_pingSlot + 1) % PendingPings;
        }

        if (!await SendAsync(new Ping(nonce), token).ConfigureAwait(false))
        {
          break;
        }
      }
    }
    catch (OperationCanceledException)
    {
      // The session ended.
    }
  }

  private void ApplyPong(Pong pong)
  {
    long stamp = 0;
    lock (_pingGate)
    {
      for (var i = 0; i < PendingPings; i++)
      {
        if (_pingStamps[i] != 0 && _pingNonces[i] == pong.Nonce)
        {
          stamp = _pingStamps[i];
          _pingStamps[i] = 0;
          break;
        }
      }
    }

    if (stamp == 0)
    {
      return;
    }

    var pingMs = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
    Volatile.Write(ref _pingMs, pingMs);
    Pings.Add(pingMs);
    Raise(PingUpdated);
  }

  private void ApplyPanelList(PanelList list)
  {
    var previous = _byId;
    var byId = new Dictionary<string, PanelState>(list.Panels.Count);
    var ordered = new List<PanelState>(list.Panels.Count);
    _byIndex.Clear();
    foreach (var info in list.Panels)
    {
      if (byId.ContainsKey(info.Id))
      {
        continue;
      }

      // Match by id: the same panel keeps its state even when its index changed.
      if (previous.TryGetValue(info.Id, out var state) && state.Kind == info.Kind)
      {
        state.Update(info);
      }
      else
      {
        state = new PanelState(info);
      }

      byId[info.Id] = state;
      _byIndex[info.Index] = state;
      ordered.Add(state);
    }

    ordered.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    _byId = byId;
    _panels = [.. ordered];
    Raise(PanelListChanged);
  }

  private void ApplyPanelData(PanelData data)
  {
    if (!_byIndex.TryGetValue(data.Panel, out var panel) || data.Payload is UnknownPayload)
    {
      // Data for a panel we have no entry for, or a payload kind from a newer minor version: ignore
      // it.
      return;
    }

    var payload = data.Payload;
    switch (payload)
    {
      case StatsPayload stats:
        Stats.Add(stats);
        break;
      case LogsPayload logs:
        Logs.Apply(logs);
        break;
      case WatchesPayload watches:
        _watches = watches;
        break;
      case TrackersPayload trackers:
        _trackers = trackers;
        break;
      case ControlsPayload controls:
        _controls = controls;
        break;
      case ConsoleCommandsPayload commands:
        _consoleCommands = commands;
        break;
      case ConsoleResultPayload result:
        _consoleResult = result;
        break;
      case ScenePayload scene:
        _scene = new SceneTree(scene.Nodes);
        break;
      case InspectorStatePayload inspector:
        _inspector = inspector;
        break;
      case InspectorThumbnailPayload thumbnail:
        InspectorThumbnails.Set(new Thumbnail(
            thumbnail.Id, thumbnail.Width, thumbnail.Height, thumbnail.Png));
        break;
      case TexturesListPayload textures:
        _textures = textures;
        TextureThumbnails.Retain(textures.Entries.Select(static e => e.Id).ToHashSet());
        break;
      case TexturesThumbnailPayload thumbnail:
        TextureThumbnails.Set(new Thumbnail(
            thumbnail.Id, thumbnail.Width, thumbnail.Height, thumbnail.Png));
        break;
      case SavePayload save:
        _save = save;
        break;
      case ViewportPayload viewport:
        _viewport = viewport;
        break;
      case CaptureStatePayload captureState:
        _captureState = captureState;
        break;
      case CaptureImagePayload captureImage:
        _captureImage = captureImage;
        break;
      case InputStatePayload inputState:
        _inputState = inputState;
        break;
    }

    panel.SetPayload(payload);
    Raise(PanelUpdated, new PanelUpdatedEventArgs(this, panel, payload));
  }
}
