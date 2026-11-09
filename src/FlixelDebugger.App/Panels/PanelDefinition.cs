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

using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// Describes one panel the app knows: its id, its title, and where it goes in the default layout.
/// </summary>
/// <remarks>
/// <para>
/// Every panel is built by the game, built-in or custom, so a definition carries no content. The
/// dock builds its tabs from it, the Debug menu lists it, and the saved layout is keyed by
/// <see cref="Id"/>. The game's panel list fills the registry; a panel the saved layout remembers
/// is registered too, so its tab keeps its place until a game that has it connects.
/// </para>
/// </remarks>
/// <param name="Id">The stable panel id, for example <c>stats</c> or <c>game.enemies</c>.</param>
/// <param name="Title">The title shown on the tab and in the Debug menu.</param>
/// <param name="Dock">Where the panel goes in the default layout.</param>
/// <param name="Shown">Whether the panel is open in the default layout.</param>
public sealed record PanelDefinition(string Id, string Title, PanelDock Dock, bool Shown);
