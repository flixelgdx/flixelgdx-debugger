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

using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Sessions;

/// <summary>
/// Describes a feed of a game whose data just changed.
/// </summary>
/// <param name="feedId">The id of the feed.</param>
/// <param name="kind">The kind of the feed.</param>
public sealed class SessionFeedEventArgs(string feedId, FeedKind kind) : EventArgs
{
  /// <summary>Gets the id of the feed.</summary>
  public string FeedId { get; } = feedId;

  /// <summary>Gets the kind of the feed.</summary>
  public FeedKind Kind { get; } = kind;
}

/// <summary>
/// One connected game, as the app sees it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameSession"/> in Core is sealed and only the server can create it, so the app would
/// be impossible to test with it. This interface lists the members the app needs.
/// <see cref="GameSessionAdapter"/> forwards every member to the real session, and tests use a
/// small fake. Core still does all the work; nothing is re-implemented here.
/// </para>
/// <para>
/// The game builds every panel itself, so the app needs very little: the panel list, the panels'
/// widgets, a way to open panels and report what the person did, and the few values the window's
/// status bar and Debug menu show (from the stats and controls feeds).
/// </para>
/// <para>
/// <b>Threads.</b> Events are raised on background threads. Read the data properties from any
/// thread, and move to the UI thread (see <see cref="Services.IUiScheduler"/>) before touching a
/// control.
/// </para>
/// </remarks>
public interface IGameSession
{
  /// <summary>Raised when <see cref="State"/> changed.</summary>
  public event EventHandler? StateChanged;

  /// <summary>Raised when a feed the session subscribed to received data.</summary>
  public event EventHandler<SessionFeedEventArgs>? FeedUpdated;

  /// <summary>Raised when a new round trip time was measured.</summary>
  public event EventHandler? PingUpdated;

  /// <summary>Raised after the game sent a new panel list.</summary>
  public event EventHandler? PanelListChanged;

  /// <summary>Raised after an open panel received a whole new widget tree.</summary>
  public event EventHandler<PanelTreeEventArgs>? PanelTreeReceived;

  /// <summary>Raised after changes to the widgets of an open panel were applied.</summary>
  public event EventHandler<PanelPatchEventArgs>? PanelPatched;

  /// <summary>
  /// Raised when the game asks the app to show or hide a panel or to notify the person.
  /// </summary>
  public event EventHandler<AppRequestEventArgs>? AppRequested;

  /// <summary>Gets an id that is unique for this connection.</summary>
  public Guid Id { get; }

  /// <summary>Gets the game's title, or a fallback when the game sent none.</summary>
  public string Title { get; }

  /// <summary>Gets the platform name the game reported, for example <c>desktop</c>.</summary>
  public string Platform { get; }

  /// <summary>Gets the device name the game reported.</summary>
  public string Device { get; }

  /// <summary>Gets the connection state.</summary>
  public SessionState State { get; }

  /// <summary>
  /// Gets the last measured round trip time in milliseconds, or <see langword="null"/> before the
  /// first one.
  /// </summary>
  public double? PingMs { get; }

  /// <summary>Gets the recent stats samples, filled while the stats feed is wanted.</summary>
  public StatsHistory Stats { get; }

  /// <summary>
  /// Gets the latest controls state, or <see langword="null"/> before the controls feed sent any.
  /// </summary>
  public ControlsPayload? Controls { get; }

  /// <summary>Gets the panels the game has, ordered by index.</summary>
  public IReadOnlyList<UiPanelState> Panels { get; }

  /// <summary>Pauses or resumes the game.</summary>
  /// <param name="paused"><see langword="true"/> to pause.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default);

  /// <summary>Advances the paused game by one frame.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> StepAsync(CancellationToken cancellationToken = default);

  /// <summary>Shows or hides hitboxes.</summary>
  /// <param name="hitboxes"><see langword="true"/> to show them.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetHitboxesAsync(bool hitboxes, CancellationToken cancellationToken = default);

  /// <summary>Turns the free camera on or off.</summary>
  /// <param name="freeCam"><see langword="true"/> to turn it on.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default);

  /// <summary>Clears the game's log.</summary>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the action was sent.</returns>
  public Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default);

  /// <summary>Subscribes to one feed on behalf of one owner, or lets go of it.</summary>
  /// <param name="feedId">The feed id.</param>
  /// <param name="owner">Who wants the feed, for example <c>ui</c>.</param>
  /// <param name="wanted"><see langword="true"/> to subscribe.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the message was sent or nothing had to be sent.</returns>
  public Task<bool> SetFeedWantedAsync(
      string feedId, string owner, bool wanted, CancellationToken cancellationToken = default);

  /// <summary>Opens or closes a panel on behalf of one owner.</summary>
  /// <param name="panelId">The panel id.</param>
  /// <param name="owner">Who holds the panel open, for example <c>ui</c>.</param>
  /// <param name="open"><see langword="true"/> to hold it open.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the game knows the panel and the message was sent or not needed.</returns>
  public Task<bool> SetPanelOpenAsync(
      string panelId, string owner, bool open, CancellationToken cancellationToken = default);

  /// <summary>Tells the game that the person did something to a widget.</summary>
  /// <param name="panelId">The panel id.</param>
  /// <param name="widget">The widget id.</param>
  /// <param name="kind">What happened.</param>
  /// <param name="args">The event's arguments.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>Whether the event was sent.</returns>
  public Task<bool> SendPanelEventAsync(
      string panelId,
      int widget,
      WidgetEvent kind,
      IReadOnlyList<UiValue> args,
      CancellationToken cancellationToken = default);
}
