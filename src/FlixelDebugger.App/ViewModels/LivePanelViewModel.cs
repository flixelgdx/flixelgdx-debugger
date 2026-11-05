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

using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The base class of panels that follow the active game's latest data on a timer.
/// </summary>
/// <remarks>
/// <para>
/// The server raises events on background threads, many times a second. Redrawing on every event
/// would waste time, and touching a control from that thread is not allowed. A live panel therefore
/// does not react to events at all: a timer on the UI thread calls <see cref="Refresh"/> (about 10
/// times a second by default), and the panel compares the newest payload with the one it showed
/// last. Payloads are immutable and replaced as a whole, so a reference comparison costs nothing.
/// Think of a shop assistant who checks the delivery shelf every few minutes instead of running to
/// the door each time the bell rings.
/// </para>
/// <para>
/// When the active game changes, <see cref="OnSessionSwitched"/> lets the panel forget what it
/// showed, and the panel refreshes at once.
/// </para>
/// </remarks>
public abstract class LivePanelViewModel : PanelViewModel, IDisposable
{
  /// <summary>
  /// How often a panel refreshes by default: ten times a second is smooth enough to read.
  /// </summary>
  public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

  private readonly IDisposable _timer;

  /// <summary>
  /// Creates the panel and starts its refresh timer.
  /// </summary>
  /// <param name="id">The panel id.</param>
  /// <param name="title">The title on the panel's tab.</param>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  /// <param name="interval">
  /// The time between two refreshes, or <see langword="null"/> for <see cref="DefaultInterval"/>.
  /// </param>
  protected LivePanelViewModel(
      string id,
      string title,
      IActiveSession active,
      IUiScheduler scheduler,
      TimeSpan? interval = null)
      : base(id, title, active)
  {
    ArgumentNullException.ThrowIfNull(scheduler);
    _timer = scheduler.Every(interval ?? DefaultInterval, Refresh);
  }

  /// <summary>
  /// Brings the panel up to date with the active game. The timer calls this; it does nothing when
  /// nothing changed.
  /// </summary>
  public abstract void Refresh();

  /// <summary>
  /// Stops the refresh timer.
  /// </summary>
  public void Dispose()
  {
    _timer.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Called when the active game changed, before the panel refreshes. Forget everything remembered
  /// about the old game here.
  /// </summary>
  /// <param name="oldSession">The game that was active before, if any.</param>
  /// <param name="newSession">The game that is active now, if any.</param>
  protected virtual void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession) { }

  /// <inheritdoc/>
  protected sealed override void OnSessionChanged(
      IGameSession? oldSession, IGameSession? newSession)
  {
    OnSessionSwitched(oldSession, newSession);
    Refresh();
  }
}
