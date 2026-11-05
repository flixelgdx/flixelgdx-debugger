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
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The panels that exist from the start, in the order the Debug menu lists them.
/// </summary>
/// <remarks>
/// <para>
/// The console and the logs are real. Every other panel is a <see cref="PlaceholderPanelViewModel"/> until its
/// content is built. To replace one, change its line in <see cref="CreateDefinitions"/> to create the real
/// ViewModel and add a <c>DataTemplate</c> for that ViewModel in <c>App.axaml</c>. Nothing else has to change:
/// the dock, the menu, and the settings already work with the id.
/// </para>
/// </remarks>
public static class DefaultPanels
{
    /// <summary>The id of the performance panel. Core's <see cref="BuiltInPanels"/> has no such id because the protocol has no separate performance panel; the app shows it as its own panel.</summary>
    public const string Performance = "performance";

    /// <summary>
    /// Creates the definitions of all built-in panels.
    /// </summary>
    /// <returns>The definitions in menu order.</returns>
    public static IReadOnlyList<PanelDefinition> CreateDefinitions() =>
    [
        Placeholder(BuiltInPanels.Stats, "Stats"),
        Placeholder(Performance, "Performance"),
        Placeholder(BuiltInPanels.Controls, "Controls"),
        Placeholder(BuiltInPanels.Watches, "Watches"),
        Placeholder(BuiltInPanels.Trackers, "Trackers"),
        new PanelDefinition(BuiltInPanels.Logs, "Logs", sp => new LogsPanelViewModel(
            sp.GetRequiredService<IActiveSession>(),
            sp.GetRequiredService<Services.IUiScheduler>())),
        new PanelDefinition(BuiltInPanels.Console, "Console", sp => new ConsolePanelViewModel(
            sp.GetRequiredService<IActiveSession>(),
            sp.GetRequiredService<Services.IUiScheduler>(),
            sp.GetRequiredService<IPanelRegistry>(),
            sp.GetRequiredService<IPanelLayout>())),
        Placeholder(BuiltInPanels.Scene, "Scene"),
        Placeholder(BuiltInPanels.Inspector, "Inspector"),
        Placeholder(BuiltInPanels.Textures, "Textures"),
        Placeholder(BuiltInPanels.Save, "Save"),
    ];

    private static PanelDefinition Placeholder(string id, string title) =>
        new(id, title, sp => new PlaceholderPanelViewModel(id, title, sp.GetRequiredService<IActiveSession>()));
}
