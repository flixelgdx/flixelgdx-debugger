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
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Collects what a game changes in one panel for a short time, so a panel tool can tell the agent
/// what its action did.
/// </summary>
/// <remarks>
/// Start the watch <b>before</b> sending an event, then call <see cref="WaitAsync"/>. The watch
/// ends once the panel has been quiet for a moment, or after a time limit. Disposing unsubscribes,
/// so always use <c>using</c>.
/// </remarks>
public sealed class PanelWatch : IDisposable
{
  private static readonly TimeSpan _quiet = TimeSpan.FromMilliseconds(250);

  private readonly GameSession _session;
  private readonly string _panelId;
  private readonly Lock _gate = new();
  private readonly List<PatchOp> _ops = [];
  private readonly SemaphoreSlim _signal = new(0);
  private bool _newTree;

  private PanelWatch(GameSession session, string panelId)
  {
    _session = session;
    _panelId = panelId;
    session.PanelPatched += OnPatched;
    session.PanelTreeReceived += OnTree;
  }

  /// <summary>
  /// Starts collecting the changes of one panel.
  /// </summary>
  /// <param name="session">The game.</param>
  /// <param name="panelId">The panel id.</param>
  /// <returns>A watch that must be disposed.</returns>
  public static PanelWatch Start(GameSession session, string panelId)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(panelId);
    return new PanelWatch(session, panelId);
  }

  /// <summary>
  /// Waits until the panel has been quiet for a moment, or until the time limit runs out.
  /// </summary>
  /// <param name="limit">The longest time to wait.</param>
  /// <param name="cancellationToken">A token that cancels the wait.</param>
  /// <returns>
  /// The changes, last value per widget and property, and whether a whole new tree arrived.
  /// </returns>
  public async Task<(IReadOnlyList<PatchOp> Ops, bool NewTree)> WaitAsync(
      TimeSpan limit, CancellationToken cancellationToken = default)
  {
    var end = DateTime.UtcNow + limit;
    var heard = false;
    while (true)
    {
      var left = end - DateTime.UtcNow;
      if (left <= TimeSpan.Zero)
      {
        break;
      }

      // Before the first change, wait for the game as long as allowed; after it, only until the
      // panel goes quiet.
      var wait = heard && left > _quiet ? _quiet : left;
      if (!await _signal.WaitAsync(wait, cancellationToken).ConfigureAwait(false))
      {
        if (heard)
        {
          break;
        }

        continue;
      }

      heard = true;
    }

    lock (_gate)
    {
      var last = new Dictionary<(int, WidgetProp), PatchOp>();
      foreach (var op in _ops)
      {
        last[(op.Widget, op.Key)] = op;
      }

      return ([.. last.Values], _newTree);
    }
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    _session.PanelPatched -= OnPatched;
    _session.PanelTreeReceived -= OnTree;
    _signal.Dispose();
  }

  private void OnPatched(object? sender, PanelPatchEventArgs e)
  {
    if (!string.Equals(e.Panel.Id, _panelId, StringComparison.Ordinal))
    {
      return;
    }

    lock (_gate)
    {
      _ops.AddRange(e.Ops);
    }

    Release();
  }

  private void OnTree(object? sender, PanelTreeEventArgs e)
  {
    if (!string.Equals(e.Panel.Id, _panelId, StringComparison.Ordinal))
    {
      return;
    }

    lock (_gate)
    {
      _newTree = true;
    }

    Release();
  }

  private void Release()
  {
    try
    {
      _signal.Release();
    }
    catch (ObjectDisposedException)
    {
      // The tool finished while the game was still sending.
    }
  }
}
