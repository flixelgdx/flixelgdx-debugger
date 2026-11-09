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
using Dock.Model.Core;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Tests;

public class DockPanelLayoutTests
{
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

    if (dockable is IRootDock { HiddenDockables: { } hidden })
    {
      foreach (var item in hidden.SelectMany(Walk))
      {
        yield return item;
      }
    }
  }

  private static string[] VisibleIds(IRootDock root) =>
      [.. Walk(root).OfType<PanelTool>()
          .Where(t => root.HiddenDockables?.Contains(t) != true)
          .Select(t => t.Id)];

  private static IToolDock GroupOf(IRootDock root, string id) =>
      (IToolDock)Walk(root).OfType<PanelTool>().Single(t => t.Id == id).Owner!;

  [Fact]
  public void TheDefaultLayoutContainsEveryPanelIdExactlyOnce()
  {
    var h = new Harness();
    var root = h.Layout.Root!;

    var ids = Walk(root).OfType<PanelTool>().Select(t => t.Id).ToArray();

    Assert.Equal(h.Registry.Definitions.Count, ids.Length);
    Assert.Equal(h.Registry.Definitions.Select(d => d.Id).Order(), ids.Order());
    Assert.All(h.Registry.Definitions, d => Assert.True(h.Layout.IsOpen(d.Id)));
  }

  [Fact]
  public void ThePanelsSitWhereTheDefaultArrangementPutsThem()
  {
    var root = new Harness().Layout.Root!;

    Assert.Equal(["stats"], GroupOf(root, "stats").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(["performance"], GroupOf(root, "performance").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(["logs"], GroupOf(root, "logs").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(["watches"], GroupOf(root, "watches").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(["controls"], GroupOf(root, "controls").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(["trackers"], GroupOf(root, "trackers").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(["console"], GroupOf(root, "console").VisibleDockables!.Select(d => d.Id));
    Assert.Equal(
        ["scene", "viewport", "inspector", "textures", "save"],
        GroupOf(root, "scene").VisibleDockables!.Select(d => d.Id));
    Assert.Same(GroupOf(root, "scene"), GroupOf(root, "save"));
  }

  [Fact]
  public void EachToolShowsItsRegisteredViewModel()
  {
    var h = new Harness();
    var root = h.Layout.Root!;

    foreach (var tool in Walk(root).OfType<PanelTool>())
    {
      Assert.Same(h.Registry.GetViewModel(tool.Id), tool.Context);
    }
  }

  [Fact]
  public void RevealMakesTheTabTheActiveOneOfItsGroup()
  {
    var h = new Harness();
    var group = GroupOf(h.Layout.Root!, "inspector");
    Assert.Equal("scene", group.ActiveDockable!.Id);

    Assert.True(h.Layout.Reveal("inspector"));

    Assert.Equal("inspector", group.ActiveDockable!.Id);
    Assert.False(h.Layout.Reveal("missing"));
  }

  [Fact]
  public void RevealShowsAHiddenPanelAgain()
  {
    var h = new Harness();
    h.Layout.SetVisible("inspector", false);
    Assert.False(h.Layout.IsVisible("inspector"));

    Assert.True(h.Layout.Reveal("inspector"));

    Assert.True(h.Layout.IsVisible("inspector"));
    Assert.Equal("inspector", GroupOf(h.Layout.Root!, "inspector").ActiveDockable!.Id);
  }

  [Fact]
  public void HidingAndShowingAPanelUpdatesVisibilityAndRaisesEvents()
  {
    var h = new Harness();
    var layout = h.Layout;
    var events = new List<(string, bool)>();
    layout.VisibilityChanged += (_, e) => events.Add((e.PanelId, e.Visible));

    Assert.True(layout.SetVisible("inspector", false));
    Assert.False(layout.IsVisible("inspector"));
    Assert.DoesNotContain("inspector", VisibleIds(layout.Root!));

    Assert.True(layout.SetVisible("inspector", true));
    Assert.True(layout.IsVisible("inspector"));
    Assert.Contains("inspector", VisibleIds(layout.Root!));
    Assert.Contains(("inspector", false), events);
    Assert.Contains(("inspector", true), events);
  }

  [Fact]
  public void ATabBehindAnotherOneIsOpenButNotVisibleUntilShown()
  {
    var layout = new Harness().Layout;
    Assert.True(layout.IsOpen("inspector"));
    Assert.False(layout.IsVisible("inspector"));
    Assert.True(layout.IsVisible("scene"));

    Assert.True(layout.SetVisible("inspector", true));

    Assert.True(layout.IsVisible("inspector"));
    Assert.False(layout.IsVisible("scene"));
    Assert.True(layout.IsOpen("scene"));
  }

  [Fact]
  public void AnUnknownPanelCannotBeShownOrHidden()
  {
    var layout = new Harness().Layout;

    Assert.False(layout.SetVisible("nothing", true));
    Assert.False(layout.SetVisible("nothing", false));
    Assert.False(layout.IsVisible("nothing"));
  }

  [Fact]
  public void ResetShowsEveryPanelAgainAndReplacesTheRoot()
  {
    var h = new Harness();
    var layout = h.Layout;
    var old = layout.Root;
    layout.SetVisible("stats", false);
    var raised = 0;
    layout.RootChanged += (_, _) => raised++;

    layout.Reset();

    Assert.NotSame(old, layout.Root);
    Assert.Equal(1, raised);
    Assert.True(layout.IsVisible("stats"));
  }

  [Fact]
  public void ANewPanelGoesWhereItsDockHintSays()
  {
    var h = new Harness();
    var layout = h.Layout;
    var root = layout.Root!;

    h.Registry.Register(new PanelDefinition("game.player", "Player", PanelDock.Center, true));
    h.Registry.Register(new PanelDefinition("game.ai", "AI", PanelDock.Right, true));
    h.Registry.Register(new PanelDefinition("game.debug", "Debug", PanelDock.Bottom, false));

    Assert.True(layout.IsVisible("game.player"));
    Assert.Same(GroupOf(root, "scene"), GroupOf(root, "game.player"));
    Assert.Same(GroupOf(root, "watches"), GroupOf(root, "game.ai"));
    Assert.False(layout.IsOpen("game.debug"));
  }

  [Fact]
  public void TheDefaultLayoutFollowsTheDockHintsAndClosesPanelsThatAreNotShown()
  {
    var h = new Harness(standardPanels: false);
    h.Registry.RegisterAll(
    [
        new PanelDefinition("a", "A", PanelDock.Left, true),
        new PanelDefinition("b", "B", PanelDock.Left, true),
        new PanelDefinition("c", "C", PanelDock.Center, true),
        new PanelDefinition("d", "D", PanelDock.Center, false),
        new PanelDefinition("e", "E", PanelDock.Bottom, true),
    ]);
    var root = h.Layout.Root!;

    Assert.NotSame(GroupOf(root, "a"), GroupOf(root, "b"));
    Assert.True(h.Layout.IsVisible("a") && h.Layout.IsVisible("b") && h.Layout.IsVisible("e"));
    Assert.False(h.Layout.IsOpen("d"));
    Assert.True(h.Layout.SetVisible("d", true));
    Assert.Same(GroupOf(root, "c"), GroupOf(root, "d"));
  }

  [Fact]
  public void TheFirstPanelListReplacesTheEmptyStartingLayout()
  {
    var h = new Harness(standardPanels: false);
    var empty = h.Layout.Root!;
    Assert.Empty(Walk(empty).OfType<PanelTool>());

    h.Registry.RegisterAll(TestData.StandardPanels.Select(
        p => new PanelDefinition(p.Id, p.Title, p.Dock, p.Shown)));

    Assert.NotSame(empty, h.Layout.Root);
    Assert.Equal(12, Walk(h.Layout.Root!).OfType<PanelTool>().Count());
    Assert.NotSame(GroupOf(h.Layout.Root!, "stats"), GroupOf(h.Layout.Root!, "logs"));
    Assert.Same(GroupOf(h.Layout.Root!, "scene"), GroupOf(h.Layout.Root!, "save"));
  }

  [Fact]
  public void SavedLayoutsRestoreWithHiddenPanelsAndFreshViewModels()
  {
    var h = new Harness();
    var layout = h.Layout;
    _ = layout.Root;
    layout.SetVisible("textures", false);
    var text = layout.Save();
    Assert.False(string.IsNullOrWhiteSpace(text));

    var other = new Harness();
    _ = other.Layout.Root;
    Assert.True(other.Layout.IsOpen("textures"));
    Assert.True(other.Layout.Restore(text));

    Assert.False(other.Layout.IsOpen("textures"));
    Assert.True(other.Layout.IsVisible("scene"));
    Assert.Equal(
        other.Registry.Definitions.Count, Walk(other.Layout.Root!).OfType<PanelTool>().Count());
    foreach (var tool in Walk(other.Layout.Root!).OfType<PanelTool>())
    {
      Assert.Same(other.Registry.GetViewModel(tool.Id), tool.Context);
    }

    Assert.True(other.Layout.SetVisible("textures", true));
    Assert.True(other.Layout.IsVisible("textures"));
    Assert.True(other.Layout.IsOpen("textures"));
  }

  [Fact]
  public void RestoringBrokenTextKeepsTheCurrentLayout()
  {
    var layout = new Harness().Layout;
    var root = layout.Root;

    Assert.False(layout.Restore(null));
    Assert.False(layout.Restore("   "));
    Assert.False(layout.Restore("{ this is not a layout"));

    Assert.Same(root, layout.Root);
  }

  [Fact]
  public void ARestoredLayoutGetsTabsForPanelsItDidNotKnow()
  {
    var h = new Harness();
    _ = h.Layout.Root;
    var text = h.Layout.Save();

    var other = new Harness();
    other.Registry.Register(new PanelDefinition("game.later", "Later", PanelDock.Center, true));
    Assert.True(other.Layout.Restore(text));

    Assert.True(other.Layout.IsVisible("game.later"));
  }

  [Fact]
  public void ALayoutSavedBeforeTheViewportExistedGetsAViewportTab()
  {
    // Builds a layout the way an older version saved it: without the viewport tab.
    var h = new Harness();
    var old = Walk(h.Layout.Root!).OfType<PanelTool>().Single(t => t.Id == "viewport");
    ((IDock)old.Owner!).VisibleDockables!.Remove(old);
    var text = h.Layout.Save();
    Assert.DoesNotContain("\"viewport\"", text, StringComparison.Ordinal);

    var other = new Harness();
    Assert.True(other.Layout.Restore(text));

    var root = other.Layout.Root!;
    var tool = Walk(root).OfType<PanelTool>().Single(t => t.Id == "viewport");
    Assert.True(other.Layout.IsOpen("viewport"));
    Assert.Same(GroupOf(root, "scene"), GroupOf(root, "viewport"));
    Assert.Same(other.Registry.GetViewModel("viewport"), tool.Context);
  }

  [Fact]
  public void APanelInASavedLayoutIsRegisteredAgainBeforeAnyGameConnects()
  {
    var h = new Harness();
    h.Registry.Register(new PanelDefinition("game.player", "Player", PanelDock.Right, true));
    _ = h.Layout.Root;
    var text = h.Layout.Save();

    var other = new Harness(standardPanels: false);
    Assert.True(other.Layout.Restore(text));

    Assert.True(other.Registry.TryGet("game.player", out var definition));
    Assert.Equal("Player", definition.Title);
    Assert.True(other.Layout.IsVisible("game.player"));
    Assert.True(other.Registry.TryGet("stats", out _));
  }

  [Fact]
  public void ClosingATabHidesThePanelInsteadOfDestroyingIt()
  {
    var h = new Harness();
    var layout = h.Layout;
    var root = layout.Root!;
    var factory = root.Factory!;
    var tool = Walk(root).OfType<PanelTool>().Single(t => t.Id == "save");

    factory.CloseDockable(tool);

    Assert.False(layout.IsVisible("save"));
    Assert.True(layout.SetVisible("save", true));
    Assert.True(layout.IsVisible("save"));
  }

  [Fact]
  public void DockAsDocumentMovesThePanelIntoTheMiddleGroup()
  {
    var h = new Harness();
    var layout = h.Layout;
    var root = layout.Root!;
    var tool = Walk(root).OfType<PanelTool>().Single(t => t.Id == "watches");
    var oldGroup = GroupOf(root, "watches");

    root.Factory!.DockAsDocument(tool);

    var middle = GroupOf(root, "scene");
    Assert.Same(middle, GroupOf(root, "watches"));
    Assert.DoesNotContain(tool, oldGroup.VisibleDockables ?? []);
    Assert.Same(tool, middle.ActiveDockable);
    Assert.True(layout.IsVisible("watches"));
  }

  [Fact]
  public void DockAsDocumentWorksAfterRestoringASavedLayout()
  {
    var h = new Harness();
    _ = h.Layout.Root;
    var text = h.Layout.Save();
    var other = new Harness();
    Assert.True(other.Layout.Restore(text));
    var root = other.Layout.Root!;
    var tool = Walk(root).OfType<PanelTool>().Single(t => t.Id == "console");

    root.Factory!.DockAsDocument(tool);

    Assert.Same(GroupOf(root, "scene"), GroupOf(root, "console"));
    Assert.True(other.Layout.IsVisible("console"));
  }

  [Fact]
  public void DockAsDocumentLeavesAPanelAlreadyInTheMiddleGroupAlone()
  {
    var root = new Harness().Layout.Root!;
    var middle = GroupOf(root, "scene");
    var before = middle.VisibleDockables!.Select(d => d.Id).ToArray();
    var tool = Walk(root).OfType<PanelTool>().Single(t => t.Id == "inspector");

    root.Factory!.DockAsDocument(tool);

    Assert.Equal(before, middle.VisibleDockables!.Select(d => d.Id));
  }
}
