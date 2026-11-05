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
/// The list of all panels, keyed by id.
/// </summary>
/// <remarks>
/// <para>
/// This is the extension point for panels. The dock, the Debug menu, the console commands <c>show</c> and
/// <c>close</c>, and the visibility settings all ask the registry instead of keeping their own lists. To add a
/// panel, <see cref="Register"/> a <see cref="PanelDefinition"/>. To swap a placeholder for real content,
/// register a definition with the same id.
/// </para>
/// <para>This type is not thread safe; use it from the UI thread.</para>
/// </remarks>
/// <example>
/// <code>
/// registry.Register(new PanelDefinition("stats", "Stats", sp => new StatsPanelViewModel(sp.GetRequiredService&lt;IActiveSession&gt;())));
/// </code>
/// </example>
public interface IPanelRegistry
{
    /// <summary>Raised after a definition was added or replaced.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets all definitions in menu order.</summary>
    public IReadOnlyList<PanelDefinition> Definitions { get; }

    /// <summary>
    /// Looks up a panel by its exact id.
    /// </summary>
    /// <param name="id">The panel id.</param>
    /// <param name="definition">The definition, when found.</param>
    /// <returns><see langword="true"/> if a panel with that id exists.</returns>
    public bool TryGet(string id, out PanelDefinition definition);

    /// <summary>
    /// Looks up a panel by id or title, ignoring case. This is what the <c>show</c> and <c>close</c> commands use.
    /// </summary>
    /// <param name="idOrTitle">The text the person typed.</param>
    /// <param name="definition">The definition, when found.</param>
    /// <returns><see langword="true"/> if a panel matches.</returns>
    public bool TryFind(string idOrTitle, out PanelDefinition definition);

    /// <summary>
    /// Gets the ViewModel of a panel, creating it the first time.
    /// </summary>
    /// <param name="id">The panel id.</param>
    /// <returns>The same ViewModel on every call until the panel is registered again.</returns>
    /// <exception cref="KeyNotFoundException">No panel has that id.</exception>
    public PanelViewModel GetViewModel(string id);

    /// <summary>
    /// Adds a panel, or replaces the panel that has the same id.
    /// </summary>
    /// <param name="definition">The definition to add.</param>
    public void Register(PanelDefinition definition);

    /// <summary>
    /// Registers a generic panel (rows and buttons) for a panel a game announced, unless that id is already known.
    /// </summary>
    /// <param name="id">The panel id, usually starting with <c>custom.</c>.</param>
    /// <param name="title">The title the game gave the panel.</param>
    /// <returns><see langword="true"/> if the panel is new.</returns>
    public bool RegisterCustom(string id, string title);
}
