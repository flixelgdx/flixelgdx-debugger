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
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.Views;
using FlixelDebugger.App.Widgets;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Tests;

/// <summary>
/// Opens the real main window with the real dock on a headless platform, so XAML and binding
/// mistakes show up as test failures instead of as a window that does not open.
/// </summary>
public class MainWindowSmokeTests
{
  private static readonly string[] _panels = ["stats", "everything", "console"];

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
    var game = new FakeGameSession("Smoke Game");
    game.Stats.Add(TestData.Stats(60));
    game.PingMs = 2.5;
    game.Controls = new ControlsPayload(
        false, true, false, 20, LogLevel.Info, [new Keybind("F3", "Pause")]);
    game.SetPanels(
        new PanelInfo(0, "stats", "Stats", PanelDock.Left, true),
        new PanelInfo(1, "everything", "Everything", PanelDock.Center, true),
        new PanelInfo(2, "console", "Console", PanelDock.Bottom, true),
        new PanelInfo(3, "hidden", "Hidden", PanelDock.Right, false));
    game.SendTree(
        "stats",
        TestWidgets.Node(WidgetType.Label, 1, "fps", (WidgetProp.Text, TestWidgets.S("60 FPS"))));
    game.SendTree("everything", TestWidgets.Everything());
    game.SendTree("console", TestWidgets.Node(WidgetType.TextBox, 1, "command",
        (WidgetProp.History, TestWidgets.I(8)),
        (WidgetProp.Completions, TestWidgets.List(TestWidgets.List(
            TestWidgets.S("spawn"), TestWidgets.S("spawn <type>"), TestWidgets.S("Spawns."))))));
    return game;
  }

  [Fact]
  public async Task TheMainWindowShowsEveryPanelTheGameBuilt()
  {
    var session = TestAppBuilder.Session;

    // The work runs on the UI thread and only returns plain values: a failed assertion inside the
    // dispatcher would leave the session waiting, and a failed assertion on a visual tree would try
    // to print the whole tree.
    var result = await session.Dispatch(
        () =>
        {
          var h = new Harness();
          var window = new MainWindow(h.Get<WindowNotificationService>()) { DataContext = h.Main };
          var seeded = SeededGame();
          h.Host.Connect(seeded);
          window.Show();
          Pump(window);
          h.Scheduler.Tick();
          var docks = window.GetVisualDescendants().OfType<DockControl>().Count();
          var missing = new List<string>();
          foreach (var id in _panels)
          {
            h.Layout.Reveal(id);
            Pump(window);
            if (!window.GetVisualDescendants().OfType<GamePanelView>()
                .Any(v => v.DataContext is Panels.GamePanelViewModel panel && panel.Id == id))
            {
              missing.Add(id);
            }
          }

          h.Layout.Reveal("everything");
          Pump(window);
          var all = window.GetVisualDescendants().ToList();
          var drawn = 0;
          foreach (var control in all.OfType<Control>()
              .Where(c => c is GraphWidgetControl or CanvasWidgetControl))
          {
            using var bitmap = new RenderTargetBitmap(new PixelSize(320, 180));
            control.Measure(new Size(320, 180));
            control.Arrange(new Rect(0, 0, 320, 180));
            bitmap.Render(control);
            drawn++;
          }

          var tables = all.OfType<ListBox>().Count();

          // The tree and the property grid live on the second page of the tab strip.
          all.OfType<TabControl>().First(t => t.ItemCount == 2).SelectedIndex = 1;
          Pump(window);
          all = window.GetVisualDescendants().ToList();
          var trees = all.OfType<TreeView>().Count();
          var rows = all.OfType<PropertyRowView>().Count();
          var checkbox = all.OfType<CheckBox>().First(c => (c.Content as string) == "Flag");
          checkbox.IsChecked = true;
          Pump(window);
          var sentChange = seeded.Events.Any(e => e.Kind == WidgetEvent.Change && e.Widget == 5);
          var hiddenOpen = seeded.IsOpen("hidden");
          var everythingOpen = seeded.IsOpen("everything");
          window.Close();
          return (
              Docks: docks,
              Missing: string.Join(",", missing),
              Drawn: drawn,
              Tables: tables,
              Trees: trees,
              Rows: rows,
              SentChange: sentChange,
              HiddenOpen: hiddenOpen,
              EverythingOpen: everythingOpen);
        },
        TestContext.Current.CancellationToken);

    Assert.Equal(1, result.Docks);
    Assert.Equal(string.Empty, result.Missing);
    Assert.Equal(2, result.Drawn);
    Assert.True(result.Tables >= 1);
    Assert.Equal(1, result.Trees);
    Assert.Equal(1, result.Rows);
    Assert.True(result.SentChange);
    Assert.False(result.HiddenOpen);
    Assert.True(result.EverythingOpen);
  }

  [Fact]
  public async Task TheSettingsDialogOpensWithTheMcpSection()
  {
    var session = TestAppBuilder.Session;

    var result = await session.Dispatch(
        () =>
        {
          var model = new ViewModels.SettingsViewModel(6510, false, true, 6511);
          var window = new SettingsWindow { DataContext = model };
          window.Show();
          Pump(window);
          var boxes = window.GetVisualDescendants().OfType<TextBox>().ToList();
          var texts = boxes.Select(b => b.Text ?? string.Empty).ToList();
          window.Close();
          return (
              Boxes: boxes.Count,
              HasCommand: texts.Any(t => t.StartsWith("claude mcp add", StringComparison.Ordinal)));
        },
        TestContext.Current.CancellationToken);

    Assert.Equal(5, result.Boxes);
    Assert.True(result.HasCommand);
  }
}
