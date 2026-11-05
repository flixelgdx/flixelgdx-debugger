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

using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.App.Sessions;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The base class of every panel's ViewModel.
/// </summary>
/// <remarks>
/// <para>
/// A panel knows its <see cref="Id"/> and <see cref="Title"/> and always shows the game that is active in the
/// main window. Override <see cref="OnSessionChanged"/> to move event subscriptions from the old game to the new
/// one. The base class raises that call on the UI thread.
/// </para>
/// <para>
/// To add a real panel, derive from this class, add a view and a <c>DataTemplate</c> for it in
/// <c>App.axaml</c>, and register a <see cref="PanelDefinition"/> that creates it (see
/// <see cref="DefaultPanels"/>).
/// </para>
/// </remarks>
public abstract class PanelViewModel : ObservableObject
{
  private readonly IActiveSession _active;

  /// <summary>
  /// Creates a panel and starts following the active game.
  /// </summary>
  /// <param name="id">The panel id, for example <c>stats</c>.</param>
  /// <param name="title">The title shown on the panel's tab.</param>
  /// <param name="active">Tells which game is active.</param>
  protected PanelViewModel(string id, string title, IActiveSession active)
  {
    ArgumentException.ThrowIfNullOrEmpty(id);
    ArgumentNullException.ThrowIfNull(title);
    ArgumentNullException.ThrowIfNull(active);
    Id = id;
    Title = title;
    _active = active;
    active.Changed += (_, e) => OnSessionChanged(e.OldSession, e.NewSession);
  }

  /// <summary>Gets the stable panel id.</summary>
  public string Id { get; }

  /// <summary>Gets the title shown on the panel's tab.</summary>
  public string Title { get; }

  /// <summary>Gets the game this panel shows, or <see langword="null"/> when there is none.</summary>
  protected IGameSession? Session => _active.Current;

  /// <summary>
  /// Called when the active game changed.
  /// </summary>
  /// <param name="oldSession">The game that was active before, if any. Unsubscribe from it.</param>
  /// <param name="newSession">The game that is active now, if any. Subscribe to it.</param>
  protected virtual void OnSessionChanged(IGameSession? oldSession, IGameSession? newSession) { }
}
