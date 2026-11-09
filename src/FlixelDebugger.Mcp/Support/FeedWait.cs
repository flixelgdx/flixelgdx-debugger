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

using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Waits for the next payload of one feed, with a timeout.
/// </summary>
/// <remarks>
/// <para>
/// Game actions have no direct reply. The game changes something and then sends the feed's new
/// state a moment later. To turn that into a tool result, subscribe <b>before</b> sending the
/// action (otherwise a fast answer is missed), send, then <see cref="WaitAsync"/>. Disposing
/// unsubscribes, so always use <c>using</c>.
/// </para>
/// <para>
/// <see cref="GameSession.FeedUpdated"/> is raised on the receive thread after the payload is
/// stored, so the waiter completes its task without running continuations inline.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var wait = FeedWait.Start(session, BuiltInFeeds.Controls);
/// await session.SetPausedAsync(true);
/// var payload = await wait.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
/// </code>
/// </example>
public sealed class FeedWait : IDisposable
{
  private readonly GameSession _session;
  private readonly string _feedId;
  private readonly Func<FeedPayload, bool>? _match;
  private readonly TaskCompletionSource<FeedPayload> _done =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  private FeedWait(GameSession session, string feedId, Func<FeedPayload, bool>? match)
  {
    _session = session;
    _feedId = feedId;
    _match = match;
    session.FeedUpdated += OnUpdated;
  }

  /// <summary>
  /// Starts listening for a payload of a feed.
  /// </summary>
  /// <param name="session">The game to listen to.</param>
  /// <param name="feedId">The id of the feed, for example <c>viewport</c>.</param>
  /// <param name="match">
  /// Chooses which payloads count, or <see langword="null"/> to accept the next one.
  /// </param>
  /// <returns>A waiter that must be disposed.</returns>
  public static FeedWait Start(
      GameSession session, string feedId, Func<FeedPayload, bool>? match = null)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(feedId);
    return new FeedWait(session, feedId, match);
  }

  /// <summary>
  /// Waits for a matching payload.
  /// </summary>
  /// <param name="timeout">How long to wait.</param>
  /// <param name="cancellationToken">A token that cancels the wait.</param>
  /// <returns>The payload, or <see langword="null"/> if the timeout ran out first.</returns>
  public async Task<FeedPayload?> WaitAsync(
      TimeSpan timeout, CancellationToken cancellationToken = default)
  {
    try
    {
      return await _done.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      return null;
    }
  }

  /// <inheritdoc/>
  public void Dispose() => _session.FeedUpdated -= OnUpdated;

  private void OnUpdated(object? sender, FeedUpdatedEventArgs e)
  {
    if (!string.Equals(e.Feed.Id, _feedId, StringComparison.Ordinal))
    {
      return;
    }

    if (_match is null || _match(e.Payload))
    {
      _done.TrySetResult(e.Payload);
    }
  }
}
