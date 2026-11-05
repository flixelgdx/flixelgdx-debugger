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

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// Remembers which selection the person made in the debugger itself, as opposed to one made in the
/// game.
/// </summary>
/// <remarks>
/// <para>
/// The inspector pops into view when the game reports a new selection, for example after an
/// Alt+click in the game window. When the person selects a node in the scene tree, though, the
/// scene tab is already in front, and switching to the inspector tab would take the tree away right
/// after a click. The scene panel therefore calls <see cref="MarkLocal"/>, and the inspector asks
/// <see cref="TryConsume"/> before it reveals itself.
/// </para>
/// </remarks>
public sealed class SelectionOrigin
{
  private int _local = -1;

  /// <summary>
  /// Records that the person selected an object in the debugger.
  /// </summary>
  /// <param name="id">The object id.</param>
  public void MarkLocal(int id) => _local = id;

  /// <summary>
  /// Tells whether a selection came from the debugger, and forgets the mark.
  /// </summary>
  /// <param name="id">The id the game reported as selected.</param>
  /// <returns><see langword="true"/> if the person selected that object here.</returns>
  public bool TryConsume(int id)
  {
    var local = _local == id;
    _local = -1;
    return local;
  }
}
