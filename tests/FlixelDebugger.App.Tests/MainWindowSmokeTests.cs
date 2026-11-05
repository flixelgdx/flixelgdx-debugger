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

using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.Views;

namespace FlixelDebugger.App.Tests;

/// <summary>
/// Opens the real main window with the real dock on a headless platform, so XAML and binding mistakes show up as
/// test failures instead of as a window that does not open.
/// </summary>
public class MainWindowSmokeTests
{
    [Fact]
    public async Task TheMainWindowOpensWithTheDockAndEveryPanelView()
    {
        // The session is not disposed on purpose: disposing it blocks forever under xUnit's synchronization context.
        // Its thread ends together with the test process.
        var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

        // The work runs on the UI thread and only returns plain numbers: a failed assertion inside the dispatcher
        // would leave the session waiting, and a failed assertion on a visual tree would try to print the whole tree.
        var counts = await session.Dispatch(
            () =>
            {
                var h = new Harness();
                var window = new MainWindow(h.Get<WindowNotificationService>()) { DataContext = h.Main };
                h.Connect("Smoke Game");
                window.Show();

                // The dock creates its panels in later layout passes, so let the UI thread catch up.
                for (var i = 0; i < 10; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                var tree = window.GetVisualDescendants().ToList();
                var result = (
                    Docks: tree.OfType<DockControl>().Count(),
                    Logs: tree.OfType<LogsPanelView>().Count(),
                    Consoles: tree.OfType<ConsolePanelView>().Count(),
                    Placeholders: tree.OfType<PlaceholderPanelView>().Count());
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, counts.Docks);
        Assert.Equal(1, counts.Logs);
        Assert.Equal(1, counts.Consoles);
        Assert.True(counts.Placeholders > 0);
    }
}
