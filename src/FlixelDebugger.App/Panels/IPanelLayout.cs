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

using Dock.Model.Controls;
using FlixelDebugger.App.Panels.Docking;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// Describes a panel that was shown or hidden.
/// </summary>
/// <param name="panelId">The id of the panel.</param>
/// <param name="visible"><see langword="true"/> if the panel is visible now, meaning its tab is open and in front.</param>
public sealed class PanelVisibilityChangedEventArgs(string panelId, bool visible) : EventArgs
{
    /// <summary>Gets the id of the panel.</summary>
    public string PanelId { get; } = panelId;

    /// <summary>Gets whether the panel is visible now, meaning its tab is open and in front.</summary>
    public bool Visible { get; } = visible;
}

/// <summary>
/// The arrangement of the panels: which are visible, where they sit, and how that is saved.
/// </summary>
/// <remarks>
/// <para>
/// The real implementation is <see cref="DockPanelLayout"/>, which is built on Dock.Avalonia. ViewModels only
/// talk to this interface, so tests can replace the dock with a fake.
/// </para>
/// <para>
/// A panel is <b>open</b> when its tab exists in the layout and was not closed. It is <b>visible</b> when it is
/// open and is also the tab in front in its group, which is what the person actually sees. Several panels can
/// share a group (the middle group holds Scene, Inspector, Textures, and Save), so an open panel can sit
/// behind another tab. The Debug menu check marks follow <see cref="IsVisible"/>, and the saved settings
/// follow <see cref="IsOpen"/>, because a tab behind another one must still come back after a restart.
/// </para>
/// <para>This type is not thread safe; use it from the UI thread.</para>
/// </remarks>
public interface IPanelLayout
{
    /// <summary>Raised after the whole layout was replaced, by <see cref="Reset"/> or <see cref="Restore"/>.</summary>
    public event EventHandler? RootChanged;

    /// <summary>Raised after a panel was shown or hidden, by code or by the person closing its tab.</summary>
    public event EventHandler<PanelVisibilityChangedEventArgs>? VisibilityChanged;

    /// <summary>Gets the root of the dock layout, which the window's dock control shows. It is created on first use.</summary>
    public IRootDock? Root { get; }

    /// <summary>
    /// Tells whether a panel is open: its tab exists in the layout and was not closed.
    /// </summary>
    /// <param name="id">The panel id.</param>
    /// <returns><see langword="true"/> if the panel exists in the layout and is not hidden, even when another tab covers it.</returns>
    public bool IsOpen(string id);

    /// <summary>
    /// Tells whether a panel is visible: open and the tab in front in its group.
    /// </summary>
    /// <param name="id">The panel id.</param>
    /// <returns><see langword="true"/> if the person can see the panel right now.</returns>
    public bool IsVisible(string id);

    /// <summary>
    /// Shows or hides a panel.
    /// </summary>
    /// <remarks>
    /// Showing a panel opens its tab if it was closed (even when the tab's group is gone) and always brings it to
    /// the front. Hiding a panel closes its tab, whether or not it was in front.
    /// </remarks>
    /// <param name="id">The panel id.</param>
    /// <param name="visible"><see langword="true"/> to show the panel, <see langword="false"/> to hide it.</param>
    /// <returns><see langword="false"/> if no panel has that id.</returns>
    public bool SetVisible(string id, bool visible);

    /// <summary>
    /// Brings a panel into view: opens it if it is closed and makes its tab the one in front in its group.
    /// </summary>
    /// <param name="id">The panel id.</param>
    /// <returns><see langword="false"/> if no panel has that id.</returns>
    public bool Reveal(string id);

    /// <summary>
    /// Replaces the layout with the default arrangement and shows every panel.
    /// </summary>
    public void Reset();

    /// <summary>
    /// Serializes the layout, including which panels are closed.
    /// </summary>
    /// <returns>The text to store in the settings, or <see langword="null"/> if saving is not possible.</returns>
    public string? Save();

    /// <summary>
    /// Replaces the layout with a saved one.
    /// </summary>
    /// <param name="text">The text <see cref="Save"/> returned earlier.</param>
    /// <returns><see langword="false"/> if the text is empty or unreadable, in which case the layout is unchanged.</returns>
    public bool Restore(string? text);
}
