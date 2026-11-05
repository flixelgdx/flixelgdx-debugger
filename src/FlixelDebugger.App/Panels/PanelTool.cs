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

using Dock.Model.Mvvm.Controls;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The dock item of one panel: a tab with an id and a title.
/// </summary>
/// <remarks>
/// <para>
/// A <c>PanelTool</c> is deliberately thin so that saving the layout stores only what the dock needs (the id,
/// the title, and where the tab sits). The panel's ViewModel lives in the inherited <c>Context</c> property,
/// which is never saved. When a saved layout is loaded, <see cref="Docking.DockPanelLayout"/> looks the ViewModel up in
/// the <see cref="IPanelRegistry"/> by id. Think of the tool as a coat hook: the layout remembers which hook is
/// where, and the coat (the ViewModel) is fetched from the cloakroom when you come back.
/// </para>
/// <para>
/// The view is chosen by a data template for <c>PanelTool</c> in <c>App.axaml</c>, which shows the context.
/// </para>
/// </remarks>
public sealed class PanelTool : Tool
{
}
