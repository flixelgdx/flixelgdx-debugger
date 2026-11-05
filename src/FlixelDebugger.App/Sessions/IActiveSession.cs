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

namespace FlixelDebugger.App.Sessions;

/// <summary>
/// Tells which game every panel currently shows.
/// </summary>
/// <remarks>
/// <para>
/// The main window sets <see cref="Current"/> when the person chooses a game (or the first game
/// connects). Panels read it and listen to <see cref="Changed"/>, so no panel needs to know about
/// the game list.
/// </para>
/// <para>
/// A disconnected game stays current until another one is chosen. That is on purpose: the last data
/// stays visible so the person can still read what happened right before the crash.
/// </para>
/// </remarks>
public interface IActiveSession
{
  /// <summary>Raised on the UI thread after <see cref="Current"/> changed.</summary>
  public event EventHandler<ActiveSessionChangedEventArgs>? Changed;

  /// <summary>
  /// Gets or sets the game every panel shows, or <see langword="null"/> when there is none.
  /// </summary>
  public IGameSession? Current { get; set; }
}

/// <summary>
/// Describes a change of the active game.
/// </summary>
/// <param name="oldSession">The game that was active before, if any.</param>
/// <param name="newSession">The game that is active now, if any.</param>
public sealed class ActiveSessionChangedEventArgs(IGameSession? oldSession, IGameSession? newSession) : EventArgs
{
  /// <summary>Gets the game that was active before, if any.</summary>
  public IGameSession? OldSession { get; } = oldSession;

  /// <summary>Gets the game that is active now, if any.</summary>
  public IGameSession? NewSession { get; } = newSession;
}

/// <summary>
/// The default <see cref="IActiveSession"/>.
/// </summary>
public sealed class ActiveSession : IActiveSession
{
  private IGameSession? _current;

  /// <inheritdoc/>
  public event EventHandler<ActiveSessionChangedEventArgs>? Changed;

  /// <inheritdoc/>
  public IGameSession? Current
  {
    get => _current;
    set
    {
      if (ReferenceEquals(_current, value))
      {
        return;
      }

      var old = _current;
      _current = value;
      Changed?.Invoke(this, new ActiveSessionChangedEventArgs(old, value));
    }
  }
}
