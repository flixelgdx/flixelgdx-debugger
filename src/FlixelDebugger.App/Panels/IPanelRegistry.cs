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

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The list of all panels the app knows, keyed by id.
/// </summary>
/// <remarks>
/// <para>
/// The registry is neutral: it has no built-in entries. The game announces its panels, built-in
/// ones included, and the app registers each of them. The dock, the Debug menu, and the saved
/// layout all ask the registry instead of keeping their own lists. Think of it as a guest list that
/// the game writes and the app only reads at the door.
/// </para>
/// <para>This type is not thread safe; use it from the UI thread.</para>
/// </remarks>
public interface IPanelRegistry
{
  /// <summary>Raised after a definition was added or changed.</summary>
  public event EventHandler? Changed;

  /// <summary>Gets all definitions in the order they were first registered.</summary>
  public IReadOnlyList<PanelDefinition> Definitions { get; }

  /// <summary>
  /// Looks up a panel by its exact id.
  /// </summary>
  /// <param name="id">The panel id.</param>
  /// <param name="definition">The definition, when found.</param>
  /// <returns><see langword="true"/> if a panel with that id exists.</returns>
  public bool TryGet(string id, out PanelDefinition definition);

  /// <summary>
  /// Gets the ViewModel of a panel, creating it the first time.
  /// </summary>
  /// <param name="id">The panel id.</param>
  /// <returns>The same ViewModel on every call.</returns>
  /// <exception cref="KeyNotFoundException">No panel has that id.</exception>
  public GamePanelViewModel GetViewModel(string id);

  /// <summary>
  /// Adds a panel, or updates the title, dock, and shown flag of the panel with the same id. The
  /// ViewModel of an existing panel is kept.
  /// </summary>
  /// <param name="definition">The definition to add.</param>
  /// <returns><see langword="true"/> if the panel is new.</returns>
  public bool Register(PanelDefinition definition);

  /// <summary>
  /// Registers several panels at once and raises <see cref="Changed"/> only once, so the layout
  /// sees a whole panel list together.
  /// </summary>
  /// <param name="definitions">The definitions to add or update.</param>
  public void RegisterAll(IEnumerable<PanelDefinition> definitions);
}
