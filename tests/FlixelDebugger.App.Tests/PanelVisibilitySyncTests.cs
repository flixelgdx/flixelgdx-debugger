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

using Dock.Model.Core;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Tests;

/// <summary>
/// Proves the Debug menu, the dock, the console commands, and the saved settings agree about which
/// panels are visible, using the real dock model. A panel counts as visible when its tab is open
/// and is the one in front in its group, which is what the person actually sees.
/// </summary>
public class PanelVisibilitySyncTests
{
  private static readonly string[] _middleGroup = ["scene", "inspector", "textures", "save"];

  private static IEnumerable<IDockable> Walk(IDockable dockable)
  {
    yield return dockable;
    if (dockable is IDock { VisibleDockables: { } visible })
    {
      foreach (var item in visible.SelectMany(Walk))
      {
        yield return item;
      }
    }
  }

  // What the person sees, read straight from the dock model and not from the code under test.
  private static bool OnScreen(IPanelLayout layout, string id) =>
      Walk(layout.Root!).OfType<PanelTool>().SingleOrDefault(t => t.Id == id)
          is { Owner: IDock owner } tool &&
      ReferenceEquals(owner.ActiveDockable, tool);

  private static void CloseTab(IPanelLayout layout, string id)
  {
    var tool = Walk(layout.Root!).OfType<PanelTool>().Single(t => t.Id == id);
    layout.Root!.Factory!.CloseDockable(tool);
  }

  private static void SelectTab(IPanelLayout layout, string id)
  {
    var tool = Walk(layout.Root!).OfType<PanelTool>().Single(t => t.Id == id);
    layout.Root!.Factory!.SetActiveDockable(tool);
  }

  // Does what a click on the menu item does: flips the check mark, which applies the change.
  private static void Click(MainWindowViewModel main, string id)
  {
    var item = Item(main, id);
    item.IsVisible = !item.IsVisible;
  }

  private static PanelMenuItemViewModel Item(MainWindowViewModel main, string id) =>
      main.PanelItems.Single(i => i.Id == id);

  // Every check mark must equal what the dock shows, for every panel.
  private static void AssertMenuMatchesScreen(Harness h)
  {
    foreach (var item in h.Main.PanelItems)
    {
      Assert.True(
          item.IsVisible == OnScreen(h.Layout, item.Id),
          $"The menu item '{item.Id}' disagrees with the dock.");
      Assert.Equal(item.IsVisible, h.Layout.IsVisible(item.Id));
    }
  }

  [Fact]
  public void ThePanelsBehindAnotherTabStartUncheckedBecauseTheyAreNotOnScreen()
  {
    var h = new Harness();
    _ = h.Main;

    Assert.True(Item(h.Main, "scene").IsVisible);
    Assert.False(Item(h.Main, "inspector").IsVisible);
    Assert.False(Item(h.Main, "textures").IsVisible);
    Assert.False(Item(h.Main, "save").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void OneClickOnAPanelBehindAnotherTabBringsItToTheFront()
  {
    var h = new Harness();

    Click(h.Main, "inspector");

    Assert.True(OnScreen(h.Layout, "inspector"));
    Assert.True(Item(h.Main, "inspector").IsVisible);
    Assert.False(Item(h.Main, "scene").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void OneClickOnAFrontPanelHidesItAndTheNextTabTakesItsPlace()
  {
    var h = new Harness();

    Click(h.Main, "scene");

    Assert.False(h.Layout.IsOpen("scene"));
    Assert.False(Item(h.Main, "scene").IsVisible);
    AssertMenuMatchesScreen(h);
    Assert.Contains(
        h.Main.PanelItems, i => i.IsVisible && i.Id is "inspector" or "textures" or "save");
  }

  [Fact]
  public void SwitchingTabsInTheDockMovesTheCheckMarks()
  {
    var h = new Harness();
    _ = h.Main;

    SelectTab(h.Layout, "textures");

    Assert.True(Item(h.Main, "textures").IsVisible);
    Assert.False(Item(h.Main, "scene").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void ClosingATabWithItsXButtonUnchecksTheMenuItemAtOnce()
  {
    var h = new Harness();
    _ = h.Main;

    CloseTab(h.Layout, "stats");

    Assert.False(Item(h.Main, "stats").IsVisible);
    Assert.False(h.Layout.IsOpen("stats"));
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void OneClickBringsBackAPanelThatWasClosedWithItsXButton()
  {
    var h = new Harness();
    _ = h.Main;
    CloseTab(h.Layout, "stats");

    Click(h.Main, "stats");

    Assert.True(OnScreen(h.Layout, "stats"));
    Assert.True(Item(h.Main, "stats").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void OneClickBringsBackAPanelWhoseWholeGroupWasClosed()
  {
    var h = new Harness();
    _ = h.Main;
    foreach (var id in _middleGroup)
    {
      CloseTab(h.Layout, id);
    }

    AssertMenuMatchesScreen(h);
    Assert.All(_middleGroup, id => Assert.False(Item(h.Main, id).IsVisible));

    Click(h.Main, "textures");

    Assert.True(OnScreen(h.Layout, "textures"));
    Assert.True(Item(h.Main, "textures").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public async Task TheShowAndCloseCommandsKeepTheMenuInSync()
  {
    var h = new Harness();
    _ = h.Main;
    h.Connect();

    h.Console.Text = "show inspector";
    await h.Console.RunAsync();
    Assert.True(OnScreen(h.Layout, "inspector"));
    Assert.True(Item(h.Main, "inspector").IsVisible);
    AssertMenuMatchesScreen(h);

    h.Console.Text = "close inspector";
    await h.Console.RunAsync();
    Assert.False(h.Layout.IsOpen("inspector"));
    Assert.False(Item(h.Main, "inspector").IsVisible);
    AssertMenuMatchesScreen(h);

    CloseTab(h.Layout, "stats");
    h.Console.Text = "show stats";
    await h.Console.RunAsync();
    Assert.True(OnScreen(h.Layout, "stats"));
    Assert.True(Item(h.Main, "stats").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void ResetLayoutSyncsEveryCheckMarkWithTheDefaultArrangement()
  {
    var h = new Harness();
    _ = h.Main;
    CloseTab(h.Layout, "stats");
    CloseTab(h.Layout, "console");
    SelectTab(h.Layout, "save");

    h.Main.ResetLayoutCommand.Execute(null);

    Assert.True(Item(h.Main, "stats").IsVisible);
    Assert.True(Item(h.Main, "console").IsVisible);
    Assert.True(Item(h.Main, "scene").IsVisible);
    Assert.False(Item(h.Main, "save").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void ARestoredLayoutWithClosedPanelsAndAnotherFrontTabMatchesTheMenu()
  {
    var first = new Harness();
    _ = first.Main;
    CloseTab(first.Layout, "stats");
    CloseTab(first.Layout, "console");
    SelectTab(first.Layout, "save");
    first.Main.SaveLayout();

    var second = new Harness();
    second.Settings.Current.WindowLayout = first.Settings.Current.WindowLayout;
    foreach (var (id, visible) in first.Settings.Current.PanelVisibility)
    {
      second.Settings.Current.PanelVisibility[id] = visible;
    }

    _ = second.Main;
    second.Main.RestoreLayout();

    Assert.False(Item(second.Main, "stats").IsVisible);
    Assert.False(Item(second.Main, "console").IsVisible);
    Assert.True(Item(second.Main, "save").IsVisible);
    Assert.False(Item(second.Main, "scene").IsVisible);
    AssertMenuMatchesScreen(second);

    Click(second.Main, "stats");
    Assert.True(OnScreen(second.Layout, "stats"));
    AssertMenuMatchesScreen(second);
  }

  [Fact]
  public void SavingKeepsPanelsBehindATabAsOpen()
  {
    var h = new Harness();
    _ = h.Main;
    CloseTab(h.Layout, "stats");

    h.Main.SaveLayout();

    var saved = h.Settings.Current.PanelVisibility;
    Assert.False(saved["stats"]);
    Assert.True(saved["inspector"]);
    Assert.True(saved["scene"]);
  }

  [Fact]
  public void RevealBringsAClosedPanelBackAndChecksItsMenuItem()
  {
    var h = new Harness();
    _ = h.Main;
    CloseTab(h.Layout, "inspector");
    Assert.False(Item(h.Main, "inspector").IsVisible);

    Assert.True(h.Layout.Reveal("inspector"));

    Assert.True(OnScreen(h.Layout, "inspector"));
    Assert.True(Item(h.Main, "inspector").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void RevealOnAPanelBehindATabChecksItAndUnchecksTheOldFrontTab()
  {
    var h = new Harness();
    _ = h.Main;

    Assert.True(h.Layout.Reveal("textures"));

    Assert.True(Item(h.Main, "textures").IsVisible);
    Assert.False(Item(h.Main, "scene").IsVisible);
    AssertMenuMatchesScreen(h);
  }

  [Fact]
  public void ACustomPanelFollowsTheSameRules()
  {
    var h = new Harness();
    _ = h.Main;
    h.Registry.RegisterCustom("custom.player", "Player");

    // A new tab joins the middle group and comes to the front, so Scene is covered now.
    Assert.True(Item(h.Main, "custom.player").IsVisible);
    Assert.False(Item(h.Main, "scene").IsVisible);
    AssertMenuMatchesScreen(h);

    SelectTab(h.Layout, "scene");
    Assert.False(Item(h.Main, "custom.player").IsVisible);
    AssertMenuMatchesScreen(h);

    Click(h.Main, "custom.player");
    Assert.True(OnScreen(h.Layout, "custom.player"));
    Assert.True(Item(h.Main, "custom.player").IsVisible);
    AssertMenuMatchesScreen(h);

    CloseTab(h.Layout, "custom.player");
    Assert.False(Item(h.Main, "custom.player").IsVisible);
    AssertMenuMatchesScreen(h);

    Click(h.Main, "custom.player");
    Assert.True(OnScreen(h.Layout, "custom.player"));
    AssertMenuMatchesScreen(h);
  }
}

/// <summary>
/// Drives the real main window: the tab's X button and the Debug menu items are clicked like a
/// person would.
/// </summary>
public class DebugMenuWindowTests
{
  private static void Pump(Avalonia.Controls.Window window)
  {
    for (var i = 0; i < 10; i++)
    {
      Avalonia.Threading.Dispatcher.UIThread.RunJobs();
      window.UpdateLayout();
    }
  }

  private static void ClickMenuItem(
      Avalonia.Controls.Window window,
      Avalonia.Controls.MenuItem menu,
      Avalonia.Controls.MenuItem item)
  {
    menu.IsSubMenuOpen = true;
    Pump(window);
    var point = Avalonia.VisualExtensions.TranslatePoint(
        item, new Avalonia.Point(8, 8), window)!.Value;
    Avalonia.Headless.HeadlessWindowExtensions.MouseMove(window, point);
    Avalonia.Headless.HeadlessWindowExtensions.MouseDown(
        window, point, Avalonia.Input.MouseButton.Left);
    Avalonia.Headless.HeadlessWindowExtensions.MouseUp(
        window, point, Avalonia.Input.MouseButton.Left);
    Pump(window);
  }

  [Fact]
  public async Task ClickingTheDebugMenuInTheRealWindowShowsPanelsInOneClick()
  {
    var result = await TestAppBuilder.Session.Dispatch(
        () =>
        {
          var h = new Harness();
          var window = new Views.MainWindow(h.Get<Services.WindowNotificationService>())
          {
            DataContext = h.Main,
          };
          window.Show();
          Pump(window);

          var menu = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
              .OfType<Avalonia.Controls.MenuItem>()
              .First(m => (string?)m.Header == "_Debug");
          var items = menu.Items.OfType<Avalonia.Controls.MenuItem>()
              .Where(m => m.ToggleType == Avalonia.Controls.MenuItemToggleType.CheckBox)
              .ToList();
          MenuItemState Read(string title) => new(
              items.Single(m => (string?)m.Header == title).IsChecked,
              h.Layout.IsVisible(title.ToLowerInvariant()));

          var start = Read("Inspector");

          // The Inspector tab sits behind Scene, so one click on its item must bring it to the
          // front.
          ClickMenuItem(window, menu, items.Single(m => (string?)m.Header == "Inspector"));
          var afterFirstClick = Read("Inspector");

          // Close Stats with its X button, and the check mark must clear.
          var close = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
              .OfType<Avalonia.Controls.Button>()
              .Single(b => b.Name == "PART_CloseButton" &&
                  b.CommandParameter is PanelTool { Id: "stats" });
          close.Command!.Execute(close.CommandParameter);
          Pump(window);
          var afterClose = Read("Stats");

          // One click on the unchecked item must bring it back.
          ClickMenuItem(window, menu, items.Single(m => (string?)m.Header == "Stats"));
          var afterReopen = Read("Stats");
          window.Close();
          return (start, afterFirstClick, afterClose, afterReopen);
        },
        TestContext.Current.CancellationToken);

    Assert.False(result.start.Checked);
    Assert.True(result.afterFirstClick.Checked);
    Assert.True(result.afterFirstClick.Visible);
    Assert.False(result.afterClose.Checked);
    Assert.False(result.afterClose.Visible);
    Assert.True(result.afterReopen.Checked);
    Assert.True(result.afterReopen.Visible);
  }

  private readonly record struct MenuItemState(bool Checked, bool Visible);
}
