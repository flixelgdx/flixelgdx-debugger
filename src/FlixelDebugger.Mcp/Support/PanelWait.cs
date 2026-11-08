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
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Waits for the next payload of one panel, with a timeout.
/// </summary>
/// <remarks>
/// <para>
/// Game actions have no direct reply. The game changes something and then sends the panel's new
/// state a moment later. To turn that into a tool result, subscribe <b>before</b> sending the
/// action (otherwise a fast answer is missed), send, then <see cref="WaitAsync"/>. Disposing
/// unsubscribes, so always use <c>using</c>.
/// </para>
/// <para>
/// <see cref="GameSession.PanelUpdated"/> is raised on the receive thread after the payload is
/// stored, so the waiter completes its task without running continuations inline.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var wait = PanelWait.Start(session, BuiltInPanels.Controls);
/// await session.SetPausedAsync(true);
/// var payload = await wait.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
/// </code>
/// </example>
public sealed class PanelWait : IDisposable
{
  private readonly GameSession _session;
  private readonly string _panelId;
  private readonly Func<PanelPayload, bool>? _match;
  private readonly TaskCompletionSource<PanelPayload> _done =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  private PanelWait(GameSession session, string panelId, Func<PanelPayload, bool>? match)
  {
    _session = session;
    _panelId = panelId;
    _match = match;
    session.PanelUpdated += OnUpdated;
  }

  /// <summary>
  /// Starts listening for a payload of a panel.
  /// </summary>
  /// <param name="session">The game to listen to.</param>
  /// <param name="panelId">The id of the panel, for example <c>viewport</c>.</param>
  /// <param name="match">
  /// Chooses which payloads count, or <see langword="null"/> to accept the next one.
  /// </param>
  /// <returns>A waiter that must be disposed.</returns>
  public static PanelWait Start(
      GameSession session, string panelId, Func<PanelPayload, bool>? match = null)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(panelId);
    return new PanelWait(session, panelId, match);
  }

  /// <summary>
  /// Waits for a matching payload.
  /// </summary>
  /// <param name="timeout">How long to wait.</param>
  /// <param name="cancellationToken">A token that cancels the wait.</param>
  /// <returns>The payload, or <see langword="null"/> if the timeout ran out first.</returns>
  public async Task<PanelPayload?> WaitAsync(
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
  public void Dispose() => _session.PanelUpdated -= OnUpdated;

  private void OnUpdated(object? sender, PanelUpdatedEventArgs e)
  {
    if (!string.Equals(e.Panel.Id, _panelId, StringComparison.Ordinal))
    {
      return;
    }

    if (_match is null || _match(e.Payload))
    {
      _done.TrySetResult(e.Payload);
    }
  }
}
