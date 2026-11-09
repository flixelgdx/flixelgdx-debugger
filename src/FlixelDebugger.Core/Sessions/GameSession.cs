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
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

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
/// <b>Panels and feeds.</b> A game talks over two channels. <i>Panels</i> are the windows the
/// person sees: the game builds every one of them, built-in or custom, as a tree of widgets, and
/// the session keeps a live copy of each in a <see cref="UiPanelState"/>. A panel only sends
/// while someone holds it open with <see cref="SetPanelOpenAsync"/>. <i>Feeds</i> are typed data
/// for tools, such as the scene tree or the save rows, kept in a <see cref="FeedState"/> and in
/// typed properties like <see cref="Scene"/>. A game sends a feed only while the session is
/// subscribed to it (see <see cref="SetFeedWantedAsync"/> and <see cref="FeedsWanted"/>).
/// </para>
/// <para>
/// The game numbers both lists, but the numbers change whenever a panel is registered or removed.
/// The session therefore tracks everything by <i>id</i>: a state object survives a new list, and
/// only its index changes.
/// </para>
/// <para>
/// <b>Sending.</b> Sends are serialized: only one send runs at a time. Every action helper returns
/// <see langword="false"/> instead of throwing when the feed or panel does not exist (yet) or the
/// connection is closed, because both are normal races in a live debugging session.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// server.SessionConnected += (_, e) =>
/// {
///   e.Session.PanelPatched += (_, p) => Dispatch(() => Refresh(p.Panel.Id));
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
  private readonly Dictionary<ushort, FeedState> _byIndex = [];
  private readonly Dictionary<ushort, UiPanelState> _panelByIndex = [];
  private readonly Lock _panelGate = new();
  private readonly Dictionary<string, HashSet<string>> _feedOwners = new(StringComparer.Ordinal);
  private readonly Lock _feedGate = new();
  private readonly long[] _pingNonces = new long[PendingPings];
  private readonly long[] _pingStamps = new long[PendingPings];
  private readonly Lock _pingGate = new();

  private volatile FeedState[] _feeds = [];
  private volatile Dictionary<string, FeedState> _byId = [];
  private volatile UiPanelState[] _panels = [];
  private volatile Dictionary<string, UiPanelState> _panelById = [];
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
  private volatile bool _feedsWanted;

  internal GameSession(WebSocket socket, string remoteAddress, DebugServerOptions options)
  {
    _socket = socket;
    _options = options;
    _feedsWanted = options.SubscribeFeeds;
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
  public event EventHandler? FeedListChanged;

  /// <summary>
  /// Raised after a panel payload was stored. It is raised on a background thread.
  /// </summary>
  public event EventHandler<FeedUpdatedEventArgs>? FeedUpdated;

  /// <summary>
  /// Raised after the game sent a new panel list. Read <see cref="Panels"/> for the new list.
  /// </summary>
  public event EventHandler? PanelListChanged;

  /// <summary>
  /// Raised after an open panel received a whole new widget tree. The game sends one every time
  /// the panel opens and whenever the panel's structure changes.
  /// </summary>
  public event EventHandler<PanelTreeEventArgs>? PanelTreeReceived;

  /// <summary>
  /// Raised after changes to the widgets of an open panel arrived and were applied to its state.
  /// </summary>
  public event EventHandler<PanelPatchEventArgs>? PanelPatched;

  /// <summary>
  /// Raised when the game asks the app to show or hide a panel, reset its layout, or show a short
  /// notice.
  /// </summary>
  public event EventHandler<AppRequestEventArgs>? AppRequested;

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

  /// <summary>Gets the feeds the game has, ordered by index.</summary>
  public IReadOnlyList<FeedState> Feeds => _feeds;

  /// <summary>Gets the panels the game currently has, ordered by index.</summary>
  public IReadOnlyList<UiPanelState> Panels => _panels;

  /// <summary>
  /// Gets whether this session subscribes to every feed of the game. Change it with
  /// <see cref="SetFeedsWantedAsync"/>.
  /// </summary>
  public bool FeedsWanted => _feedsWanted;

  /// <summary>
  /// Tells whether the session subscribes to one feed, because every feed is wanted or because
  /// someone asked for this one with <see cref="SetFeedWantedAsync"/>.
  /// </summary>
  /// <param name="feedId">The feed id.</param>
  /// <returns><see langword="true"/> if the game sends this feed's data.</returns>
  public bool IsFeedWanted(string feedId)
  {
    ArgumentNullException.ThrowIfNull(feedId);
    if (_feedsWanted)
    {
      return true;
    }

    lock (_feedGate)
    {
      return _feedOwners.TryGetValue(feedId, out var owners) && owners.Count > 0;
    }
  }

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
  /// has not said so (a game without the capture panel never does).
  /// </summary>
  public bool? CaptureSupported => _captureState?.Supported;

  /// <summary>
  /// Gets the latest captured picture, or <see langword="null"/> before the first one.
  /// </summary>
  public CaptureImagePayload? CaptureImage => _captureImage;

  /// <summary>
  /// Gets which kinds of input the game accepts, or <see langword="null"/> when it has not said
  /// so (a game without the input panel never does).
  /// </summary>
  public InputSupport? InputFlags => _inputState?.Flags;

  /// <summary>
  /// Looks up a feed by id.
  /// </summary>
  /// <param name="id">The feed id, for example <see cref="BuiltInFeeds.Stats"/>.</param>
  /// <param name="feed">The feed, when it exists.</param>
  /// <returns><see langword="true"/> if the game has a feed with that id.</returns>
  public bool TryGetFeed(string id, out FeedState feed)
  {
    ArgumentNullException.ThrowIfNull(id);
    return _byId.TryGetValue(id, out feed!);
  }

  /// <summary>
  /// Looks up a panel by id.
  /// </summary>
  /// <param name="id">The panel id, for example <c>stats</c> or <c>game.enemies</c>.</param>
  /// <param name="panel">The panel, when it exists.</param>
  /// <returns><see langword="true"/> if the game currently has a panel with that id.</returns>
  public bool TryGetPanel(string id, out UiPanelState panel)
  {
    ArgumentNullException.ThrowIfNull(id);
    return _panelById.TryGetValue(id, out panel!);
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
  /// Sends an action to the feed with the given id.
  /// </summary>
  /// <param name="feedId">The feed id.</param>
  /// <param name="action">The action to send.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>
  /// <see langword="true"/> if it was sent; <see langword="false"/> if the game has no such feed
  /// or the connection is closed.
  /// </returns>
  public Task<bool> SendActionAsync(
      string feedId, ActionPayload action, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(feedId);
    ArgumentNullException.ThrowIfNull(action);
    if (!_byId.TryGetValue(feedId, out var feed) || feed.Index < 0)
    {
      return Task.FromResult(false);
    }

    return SendAsync(new FeedAction((ushort)feed.Index, action), cancellationToken);
  }

  /// <summary>
  /// Subscribes to every feed of the game, or ends every subscription.
  /// </summary>
  /// <param name="wanted"><see langword="true"/> to receive feed data.</param>
  /// <param name="cancellationToken">A token that cancels the sends.</param>
  /// <returns>
  /// <see langword="true"/> if every message was sent or nothing had to change.
  /// </returns>
  public async Task<bool> SetFeedsWantedAsync(
      bool wanted, CancellationToken cancellationToken = default)
  {
    if (_feedsWanted == wanted)
    {
      return true;
    }

    var feeds = _feeds;
    var before = new bool[feeds.Length];
    for (var i = 0; i < feeds.Length; i++)
    {
      before[i] = IsFeedWanted(feeds[i].Id);
    }

    _feedsWanted = wanted;
    var sent = true;
    for (var i = 0; i < feeds.Length; i++)
    {
      if (IsFeedWanted(feeds[i].Id) != before[i])
      {
        sent &= await SendAsync(
            new FeedSubscribe((ushort)feeds[i].Index, !before[i]), cancellationToken)
            .ConfigureAwait(false);
      }
    }

    return sent;
  }

  /// <summary>
  /// Subscribes to one feed on behalf of one owner, or lets go of it.
  /// </summary>
  /// <remarks>
  /// Several owners can want the same feed, for example the app's status bar and a tool; the game
  /// keeps sending until the last one lets go. The wish is kept by id, so it may be made before the
  /// game listed its feeds and it survives new lists.
  /// </remarks>
  /// <param name="feedId">The feed id, for example <see cref="BuiltInFeeds.Stats"/>.</param>
  /// <param name="owner">Who wants the feed, for example <c>ui</c>.</param>
  /// <param name="wanted"><see langword="true"/> to subscribe.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>
  /// <see langword="true"/> if the message was sent or nothing had to be sent.
  /// </returns>
  public Task<bool> SetFeedWantedAsync(
      string feedId, string owner, bool wanted, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(feedId);
    ArgumentNullException.ThrowIfNull(owner);
    var before = IsFeedWanted(feedId);
    lock (_feedGate)
    {
      if (!_feedOwners.TryGetValue(feedId, out var owners))
      {
        owners = new HashSet<string>(StringComparer.Ordinal);
        _feedOwners[feedId] = owners;
      }

      if (wanted)
      {
        owners.Add(owner);
      }
      else
      {
        owners.Remove(owner);
      }
    }

    var after = IsFeedWanted(feedId);
    if (before == after || !_byId.TryGetValue(feedId, out var feed) || feed.Index < 0)
    {
      return Task.FromResult(true);
    }

    return SendAsync(new FeedSubscribe((ushort)feed.Index, after), cancellationToken);
  }

  /// <summary>
  /// Opens or closes a panel on behalf of one owner, such as a dock window or a tool.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The game only builds and sends a panel while it is open, so the app opens a panel when it
  /// becomes visible and closes it when it is hidden. Several owners can hold the same panel open;
  /// it closes only when the last one lets go. Think of it as a light in a shared room: it stays
  /// on until the last person leaves.
  /// </para>
  /// <para>
  /// The choice survives new panel lists, and the session opens the panel again by itself when
  /// the game sends one.
  /// </para>
  /// </remarks>
  /// <param name="panelId">The panel id.</param>
  /// <param name="owner">Who holds the panel open, for example <c>ui</c> or <c>mcp</c>.</param>
  /// <param name="open"><see langword="true"/> to hold the panel open.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>
  /// <see langword="true"/> if the game knows the panel and the message was sent or nothing had
  /// to be sent.
  /// </returns>
  public Task<bool> SetPanelOpenAsync(
      string panelId, string owner, bool open, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(panelId);
    ArgumentNullException.ThrowIfNull(owner);
    if (!_panelById.TryGetValue(panelId, out var panel) || panel.Index < 0)
    {
      return Task.FromResult(false);
    }

    if (!panel.SetOpen(owner, open))
    {
      return Task.FromResult(true);
    }

    if (!open)
    {
      panel.ClearTree();
    }

    return SendAsync(new PanelOpen((ushort)panel.Index, open), cancellationToken);
  }

  /// <summary>
  /// Tells the game that the person did something to a widget, such as clicking a button.
  /// </summary>
  /// <remarks>
  /// The game runs the panel's logic and answers with a patch, so the app never changes a value
  /// on its own. The arguments depend on the event; DEBUG_PROTOCOL.md lists them.
  /// </remarks>
  /// <param name="panelId">The panel id.</param>
  /// <param name="widget">The widget id from the panel's tree.</param>
  /// <param name="kind">What happened.</param>
  /// <param name="args">The event's arguments.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>
  /// <see langword="true"/> if it was sent; <see langword="false"/> if the game has no such panel
  /// or the connection is closed.
  /// </returns>
  public Task<bool> SendPanelEventAsync(
      string panelId,
      int widget,
      WidgetEvent kind,
      IReadOnlyList<UiValue> args,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(panelId);
    ArgumentNullException.ThrowIfNull(args);
    if (!_panelById.TryGetValue(panelId, out var panel) || panel.Index < 0)
    {
      return Task.FromResult(false);
    }

    return SendAsync(
        new PanelEvent((ushort)panel.Index, widget, kind, args), cancellationToken);
  }

  /// <summary>Pauses or resumes the game.</summary>
  /// <param name="paused"><see langword="true"/> to pause.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Controls, new ControlsSetPausedAction(paused), cancellationToken);

  /// <summary>Advances the paused game by one frame.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> StepAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Controls, new ControlsStepAction(), cancellationToken);

  /// <summary>Shows or hides hitboxes in the game.</summary>
  /// <param name="hitboxes"><see langword="true"/> to show them.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetHitboxesAsync(
      bool hitboxes, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Controls, new ControlsSetHitboxesAction(hitboxes), cancellationToken);

  /// <summary>Turns the game's free camera on or off.</summary>
  /// <param name="freeCam"><see langword="true"/> to turn it on.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Controls, new ControlsSetFreeCamAction(freeCam), cancellationToken);

  /// <summary>
  /// Sets how often the game samples stats, in Hz. The game clamps it to 1 to 30.
  /// </summary>
  /// <param name="rate">The sample rate in Hz.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetRateAsync(byte rate, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Controls, new ControlsSetRateAction(rate), cancellationToken);

  /// <summary>Overrides the game's log level.</summary>
  /// <param name="level">
  /// The level, or <see cref="LogLevel.Default"/> to use the game's own setting.
  /// </param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetLogLevelAsync(
      LogLevel level, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Controls, new ControlsSetLogLevelAction(level), cancellationToken);

  /// <summary>Clears the game's log and the lines stored here.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public async Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default)
  {
    var sent = await SendActionAsync(
        BuiltInFeeds.Logs, new LogsClearAction(), cancellationToken).ConfigureAwait(false);
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

    return SendActionAsync(BuiltInFeeds.Console, new ConsoleRunAction(line), cancellationToken);
  }

  /// <summary>Asks the game for a fresh scene tree snapshot, even if nothing changed.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestSceneTreeAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Scene, new SceneRequestTreeAction(), cancellationToken);

  /// <summary>Selects an object in the inspector by its id.</summary>
  /// <param name="id">The object id, for example from the scene tree.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SelectAsync(int id, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Inspector, new InspectorSelectAction(id), cancellationToken);

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
        BuiltInFeeds.Inspector, new InspectorSelectPathAction(path), cancellationToken);
  }

  /// <summary>Selects the parent of the inspector's selection.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SelectParentAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Inspector, new InspectorSelectParentAction(), cancellationToken);

  /// <summary>Clears the inspector's selection.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ClearSelectionAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Inspector, new InspectorClearAction(), cancellationToken);

  /// <summary>Moves the game camera to the inspector's selection.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> GoToSelectionAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Inspector, new InspectorGoToAction(), cancellationToken);

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
          BuiltInFeeds.Inspector,
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
          BuiltInFeeds.Inspector, new InspectorRequestThumbnailAction(id), cancellationToken);

  /// <summary>Asks the game for a thumbnail of a texture. The game may never answer.</summary>
  /// <param name="id">The texture id from the textures list.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestThumbnailAsync(int id, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Textures, new TexturesRequestThumbnailAction(id), cancellationToken);

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
    return SendActionAsync(BuiltInFeeds.Save, new SaveSetAction(path, text), cancellationToken);
  }

  /// <summary>Resets one save key (or a whole branch) to the game's default.</summary>
  /// <param name="path">The dotted path.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveResetKeyAsync(string path, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(path);
    return SendActionAsync(BuiltInFeeds.Save, new SaveResetKeyAction(path), cancellationToken);
  }

  /// <summary>Writes the save data to storage now.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveNowAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Save, new SaveSaveNowAction(), cancellationToken);

  /// <summary>
  /// Erases all save data and the stored save. Ask the person to confirm before calling this.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveResetAllAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Save, new SaveResetAllAction(), cancellationToken);

  /// <summary>Switches the save panel to another save of the game's save list.</summary>
  /// <param name="index">
  /// The position of the save in <see cref="SavePayload.Saves"/>. The game ignores an index that
  /// is out of range.
  /// </param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SaveSelectAsync(ushort index, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Save, new SaveSelectAction(index), cancellationToken);

  /// <summary>Chooses the camera the viewport map shows and camera actions act on.</summary>
  /// <param name="index">A position in the game's camera list.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportSetCameraAsync(
      byte index, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Viewport, new ViewportSetCameraAction(index), cancellationToken);

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
          BuiltInFeeds.Viewport, new ViewportPickAction(viewX, viewY), cancellationToken);

  /// <summary>Moves an object by a distance in world units.</summary>
  /// <param name="id">The object id.</param>
  /// <param name="dx">The horizontal distance in world units.</param>
  /// <param name="dy">The vertical distance in world units.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportMoveAsync(
      int id, float dx, float dy, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Viewport, new ViewportMoveAction(id, dx, dy), cancellationToken);

  /// <summary>Moves the viewport's camera by a distance in view units.</summary>
  /// <param name="dx">The horizontal distance; positive moves the camera right.</param>
  /// <param name="dy">The vertical distance; positive moves the camera down.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportPanAsync(
      float dx, float dy, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Viewport, new ViewportPanAction(dx, dy), cancellationToken);

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
          BuiltInFeeds.Viewport,
          new ViewportZoomAction(factor, anchorViewX, anchorViewY),
          cancellationToken);

  /// <summary>Turns the free camera off, which restores the camera exactly.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ViewportResetCameraAsync(CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Viewport, new ViewportResetCameraAction(), cancellationToken);

  /// <summary>Asks the game for a picture of its next finished frame.</summary>
  /// <param name="requestId">A number that comes back with the picture.</param>
  /// <param name="maxSide">The most pixels the longest side may have (the game clamps it).</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> RequestCaptureAsync(
      int requestId, ushort maxSide, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Capture, new CaptureRequestAction(requestId, maxSide), cancellationToken);

  /// <summary>Presses or releases a key in the game.</summary>
  /// <param name="down"><see langword="true"/> to press.</param>
  /// <param name="key">The framework key code from 0 to 255.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputKeyAsync(
      bool down, int key, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Input, new InputKeyAction(down, key), cancellationToken);

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
          BuiltInFeeds.Input,
          new InputPointerAction(phase, button, viewX, viewY),
          cancellationToken);

  /// <summary>Turns the mouse wheel in the game.</summary>
  /// <param name="amount">The amount. Positive scrolls down.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputScrollAsync(
      float amount, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Input, new InputScrollAction(amount), cancellationToken);

  /// <summary>Types text in the game.</summary>
  /// <param name="text">The text. The game cuts it at 1000 characters.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputTextAsync(string text, CancellationToken cancellationToken = default) =>
      SendActionAsync(BuiltInFeeds.Input, new InputTextAction(text), cancellationToken);

  /// <summary>Clicks the center of an object's viewport box.</summary>
  /// <param name="id">The object id.</param>
  /// <param name="button">0 left, 1 right, 2 middle.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> InputClickObjectAsync(
      int id, byte button = 0, CancellationToken cancellationToken = default) =>
      SendActionAsync(
          BuiltInFeeds.Input, new InputClickObjectAction(id, button), cancellationToken);

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
      case FeedList list:
        await ApplyFeedListAsync(list).ConfigureAwait(false);
        break;
      case FeedData data:
        ApplyFeedData(data);
        break;
      case PanelList panels:
        await ApplyPanelListAsync(panels).ConfigureAwait(false);
        break;
      case PanelTree tree:
        ApplyPanelTree(tree);
        break;
      case PanelPatch patch:
        ApplyPanelPatch(patch);
        break;
      case AppRequest request:
        Raise(AppRequested, new AppRequestEventArgs(request));
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

        // The game shows the last round trip in its stats, so it travels along with each ping.
        var rtt = Volatile.Read(ref _pingMs);
        var ping = new Ping(nonce, double.IsNaN(rtt) ? -1f : (float)rtt);
        if (!await SendAsync(ping, token).ConfigureAwait(false))
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

  private async Task ApplyFeedListAsync(FeedList list)
  {
    var previous = _byId;
    var byId = new Dictionary<string, FeedState>(list.Feeds.Count);
    var ordered = new List<FeedState>(list.Feeds.Count);
    _byIndex.Clear();
    foreach (var info in list.Feeds)
    {
      if (byId.ContainsKey(info.Id))
      {
        continue;
      }

      // Match by id: the same feed keeps its state even when its index changed.
      if (previous.TryGetValue(info.Id, out var state) && state.Kind == info.Kind)
      {
        state.Update(info);
      }
      else
      {
        state = new FeedState(info);
      }

      byId[info.Id] = state;
      _byIndex[info.Index] = state;
      ordered.Add(state);
    }

    ordered.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    _byId = byId;
    _feeds = [.. ordered];
    Raise(FeedListChanged);

    foreach (var feed in ordered)
    {
      if (IsFeedWanted(feed.Id))
      {
        await SendAsync(new FeedSubscribe((ushort)feed.Index, true), _abort.Token)
            .ConfigureAwait(false);
      }
    }
  }

  private async Task ApplyPanelListAsync(PanelList list)
  {
    var previous = _panelById;
    var byId = new Dictionary<string, UiPanelState>(list.Panels.Count, StringComparer.Ordinal);
    var ordered = new List<UiPanelState>(list.Panels.Count);
    lock (_panelGate)
    {
      _panelByIndex.Clear();
      foreach (var info in list.Panels)
      {
        if (byId.ContainsKey(info.Id))
        {
          continue;
        }

        if (previous.TryGetValue(info.Id, out var state))
        {
          state.Update(info);
        }
        else
        {
          state = new UiPanelState(info);
        }

        // The game closes every panel after a new list, so an old tree is stale now.
        state.ClearTree();
        byId[info.Id] = state;
        _panelByIndex[info.Index] = state;
        ordered.Add(state);
      }
    }

    ordered.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    _panelById = byId;
    _panels = [.. ordered];
    Raise(PanelListChanged);

    // Open again whatever someone still holds open; the game builds it and sends a fresh tree.
    foreach (var panel in ordered)
    {
      if (panel.IsWanted)
      {
        await SendAsync(new PanelOpen((ushort)panel.Index, true), _abort.Token)
            .ConfigureAwait(false);
      }
    }
  }

  private UiPanelState? FindPanel(ushort index)
  {
    lock (_panelGate)
    {
      return _panelByIndex.GetValueOrDefault(index);
    }
  }

  private void ApplyPanelTree(PanelTree tree)
  {
    if (FindPanel(tree.Panel) is not { } panel)
    {
      return;
    }

    panel.ApplyTree(tree.Root);
    Raise(PanelTreeReceived, new PanelTreeEventArgs(panel, tree.Root));
  }

  private void ApplyPanelPatch(PanelPatch patch)
  {
    if (FindPanel(patch.Panel) is not { } panel || !panel.HasTree)
    {
      // A patch without a tree belongs to a panel that was closed meanwhile.
      return;
    }

    panel.ApplyPatch(patch.Ops);
    Raise(PanelPatched, new PanelPatchEventArgs(panel, patch.Ops));
  }

  private void ApplyFeedData(FeedData data)
  {
    if (!_byIndex.TryGetValue(data.Feed, out var feed) || data.Payload is UnknownPayload)
    {
      // Data for a feed we have no entry for, or a payload kind from a newer minor version: ignore
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

    feed.SetPayload(payload);
    Raise(FeedUpdated, new FeedUpdatedEventArgs(this, feed, payload));
  }
}
