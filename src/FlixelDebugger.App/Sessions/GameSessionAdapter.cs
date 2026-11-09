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
/// Makes a Core <see cref="GameSession"/> look like an <see cref="IGameSession"/>.
/// </summary>
/// <remarks>
/// Every member forwards to the real session. Events are re-raised with this adapter as the
/// sender, so handlers can compare the sender with the session they subscribed to.
/// </remarks>
public sealed class GameSessionAdapter : IGameSession
{
  private const string FallbackTitle = "Game";

  /// <summary>
  /// Wraps a session.
  /// </summary>
  /// <param name="core">The real session.</param>
  public GameSessionAdapter(GameSession core)
  {
    ArgumentNullException.ThrowIfNull(core);
    Core = core;
    core.StateChanged += (_, e) => StateChanged?.Invoke(this, e);
    core.PingUpdated += (_, e) => PingUpdated?.Invoke(this, e);
    core.FeedUpdated += (_, e) =>
        FeedUpdated?.Invoke(this, new SessionFeedEventArgs(e.Feed.Id, e.Feed.Kind));
    core.PanelListChanged += (_, e) => PanelListChanged?.Invoke(this, e);
    core.PanelTreeReceived += (_, e) => PanelTreeReceived?.Invoke(this, e);
    core.PanelPatched += (_, e) => PanelPatched?.Invoke(this, e);
    core.AppRequested += (_, e) => AppRequested?.Invoke(this, e);
  }

  /// <inheritdoc/>
  public event EventHandler? StateChanged;

  /// <inheritdoc/>
  public event EventHandler<SessionFeedEventArgs>? FeedUpdated;

  /// <inheritdoc/>
  public event EventHandler? PingUpdated;

  /// <inheritdoc/>
  public event EventHandler? PanelListChanged;

  /// <inheritdoc/>
  public event EventHandler<PanelTreeEventArgs>? PanelTreeReceived;

  /// <inheritdoc/>
  public event EventHandler<PanelPatchEventArgs>? PanelPatched;

  /// <inheritdoc/>
  public event EventHandler<AppRequestEventArgs>? AppRequested;

  /// <summary>Gets the wrapped session.</summary>
  public GameSession Core { get; }

  /// <inheritdoc/>
  public Guid Id => Core.Id;

  /// <inheritdoc/>
  public string Title =>
      Core.Hello is { GameTitle.Length: > 0 } hello ? hello.GameTitle : FallbackTitle;

  /// <inheritdoc/>
  public string Platform => Core.Hello?.Platform ?? string.Empty;

  /// <inheritdoc/>
  public string Device => Core.Hello?.Device ?? string.Empty;

  /// <inheritdoc/>
  public SessionState State => Core.State;

  /// <inheritdoc/>
  public double? PingMs => Core.PingMs;

  /// <inheritdoc/>
  public StatsHistory Stats => Core.Stats;

  /// <inheritdoc/>
  public ControlsPayload? Controls => Core.Controls;

  /// <inheritdoc/>
  public IReadOnlyList<UiPanelState> Panels => Core.Panels;

  /// <inheritdoc/>
  public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) =>
      Core.SetPausedAsync(paused, cancellationToken);

  /// <inheritdoc/>
  public Task<bool> StepAsync(CancellationToken cancellationToken = default) =>
      Core.StepAsync(cancellationToken);

  /// <inheritdoc/>
  public Task<bool> SetHitboxesAsync(
      bool hitboxes, CancellationToken cancellationToken = default) =>
      Core.SetHitboxesAsync(hitboxes, cancellationToken);

  /// <inheritdoc/>
  public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default) =>
      Core.SetFreeCamAsync(freeCam, cancellationToken);

  /// <inheritdoc/>
  public Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default) =>
      Core.ClearLogsAsync(cancellationToken);

  /// <inheritdoc/>
  public Task<bool> SetFeedWantedAsync(
      string feedId, string owner, bool wanted, CancellationToken cancellationToken = default) =>
      Core.SetFeedWantedAsync(feedId, owner, wanted, cancellationToken);

  /// <inheritdoc/>
  public Task<bool> SetPanelOpenAsync(
      string panelId, string owner, bool open, CancellationToken cancellationToken = default) =>
      Core.SetPanelOpenAsync(panelId, owner, open, cancellationToken);

  /// <inheritdoc/>
  public Task<bool> SendPanelEventAsync(
      string panelId,
      int widget,
      WidgetEvent kind,
      IReadOnlyList<UiValue> args,
      CancellationToken cancellationToken = default) =>
      Core.SendPanelEventAsync(panelId, widget, kind, args, cancellationToken);
}
