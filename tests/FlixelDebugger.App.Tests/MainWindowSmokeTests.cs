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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using FlixelDebugger.App.Controls;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.Views;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

/// <summary>
/// Opens the real main window with the real dock on a headless platform, so XAML and binding mistakes show up as
/// test failures instead of as a window that does not open.
/// </summary>
public class MainWindowSmokeTests
{
    // The view type every panel must show once it is in front.
    private static readonly (string Id, Type View)[] _views =
    [
        ("stats", typeof(StatsPanelView)),
        ("performance", typeof(PerformancePanelView)),
        ("controls", typeof(ControlsPanelView)),
        ("watches", typeof(WatchesPanelView)),
        ("trackers", typeof(TrackersPanelView)),
        ("logs", typeof(LogsPanelView)),
        ("console", typeof(ConsolePanelView)),
        ("scene", typeof(ScenePanelView)),
        ("inspector", typeof(InspectorPanelView)),
        ("textures", typeof(TexturesPanelView)),
        ("save", typeof(SavePanelView)),
        ("custom.player", typeof(CustomPanelView)),
    ];

    private static void Pump(Window window)
    {
        // The dock creates its panels in later layout passes, so let the UI thread catch up.
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private static FakeGameSession SeededGame()
    {
        var game = new FakeGameSession("Smoke Game")
        {
            Panels = [new SessionPanel("custom.player", "Player", PanelKind.Custom)],
        };
        game.Stats.Add(TestData.Stats(60));
        game.Pings.Add(2.5);
        game.PingMs = 2.5;
        game.Controls = new ControlsPayload(false, true, false, 20, LogLevel.Info, [new Keybind("F3", "Pause")]);
        game.Watches = new WatchesPayload([TestData.Named("hp", PropertyValue.FromInt(3)), TestData.Named("bad", PropertyValue.FromError("boom"))]);
        game.Trackers = new TrackersPayload([new TrackerBlock("Player", [TestData.Named("x", PropertyValue.FromFloat(1.5f))])]);
        game.Scene = TestData.Tree();
        game.Inspector = TestData.Inspector(
            3,
            "state.1",
            true,
            TestData.Section("FlixelSprite", TestData.Row("x", PropertyValue.FromFloat(1.5f), true), TestData.Row("alive", PropertyValue.FromBool(true), true)));
        game.Textures = TestData.Textures(TestData.Texture(1, "a.png", duplicate: true), TestData.Texture(2, "b.png"));
        game.Save = new SavePayload([new SaveRow("player.coins", "12", ValueKind.Double, true)], ["old.key"], "1 value", false);
        game.Customs["custom.player"] = new CustomPayload([TestData.Row("hp", PropertyValue.FromInt(5), true)], [new CustomButton(0, "Heal")]);
        return game;
    }

    [Fact]
    public async Task TheMainWindowOpensWithTheDockAndEveryPanelView()
    {
        var session = TestAppBuilder.Session;

        // The work runs on the UI thread and only returns plain values: a failed assertion inside the dispatcher
        // would leave the session waiting, and a failed assertion on a visual tree would try to print the whole tree.
        var result = await session.Dispatch(
            () =>
            {
                var h = new Harness();
                var window = new MainWindow(h.Get<WindowNotificationService>()) { DataContext = h.Main };
                h.Host.Connect(SeededGame());
                window.Show();
                Pump(window);
                h.Scheduler.Tick();

                var docks = window.GetVisualDescendants().OfType<DockControl>().Count();
                var missing = new List<string>();
                foreach (var (id, view) in _views)
                {
                    // Bring each panel to the front once, so every tab of the middle group gets built.
                    h.Layout.Reveal(id);
                    Pump(window);
                    h.Scheduler.Tick();
                    Pump(window);
                    if (!window.GetVisualDescendants().Any(v => v.GetType() == view))
                    {
                        missing.Add(id);
                    }
                }

                // Draw every graph once, so the custom Render override runs with real data.
                h.Layout.Reveal("performance");
                Pump(window);
                var graphs = window.GetVisualDescendants().OfType<GraphControl>().ToList();
                foreach (var graph in graphs)
                {
                    using var bitmap = new RenderTargetBitmap(new PixelSize(200, 52));
                    graph.Measure(new Size(200, 52));
                    graph.Arrange(new Rect(0, 0, 200, 52));
                    bitmap.Render(graph);
                }

                var rows = window.GetVisualDescendants().OfType<PropertyRowView>().Count();
                window.Close();
                return (Docks: docks, Missing: string.Join(",", missing), Graphs: graphs.Count, Rows: rows);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Docks);
        Assert.Equal(string.Empty, result.Missing);
        Assert.Equal(3, result.Graphs);
        Assert.True(result.Rows > 0);
    }
}
