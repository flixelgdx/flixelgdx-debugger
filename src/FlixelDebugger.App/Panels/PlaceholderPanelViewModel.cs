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

using FlixelDebugger.App.Sessions;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// A panel that only says it is not implemented yet.
/// </summary>
/// <remarks>
/// Every panel that has no real content yet is registered with this ViewModel, so the layout, the Debug menu,
/// and the saved settings already treat it like a real panel. Replacing it later only means registering another
/// <see cref="PanelDefinition"/> for the same id.
/// </remarks>
public sealed class PlaceholderPanelViewModel : PanelViewModel
{
    /// <summary>
    /// Creates a placeholder.
    /// </summary>
    /// <param name="id">The panel id.</param>
    /// <param name="title">The panel title.</param>
    /// <param name="active">Tells which game is active.</param>
    public PlaceholderPanelViewModel(string id, string title, IActiveSession active)
        : base(id, title, active)
    {
        Description = $"The {title} panel is not available yet.";
    }

    /// <summary>Gets the sentence shown in the panel.</summary>
    public string Description { get; }
}
