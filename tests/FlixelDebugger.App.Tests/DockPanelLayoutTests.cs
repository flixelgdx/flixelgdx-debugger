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
        [.. Walk(root).OfType<PanelTool>().Where(t => root.HiddenDockables?.Contains(t) != true).Select(t => t.Id)];

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
        Assert.All(h.Registry.Definitions, d => Assert.True(h.Layout.IsVisible(d.Id)));
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
        Assert.Equal(["scene", "inspector", "textures", "save"], GroupOf(root, "scene").VisibleDockables!.Select(d => d.Id));
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
    public void ACustomPanelIsAddedToTheMiddleGroup()
    {
        var h = new Harness();
        var layout = h.Layout;
        var root = layout.Root!;

        h.Registry.RegisterCustom("custom.player", "Player");

        Assert.True(layout.IsVisible("custom.player"));
        Assert.Same(GroupOf(root, "scene"), GroupOf(root, "custom.player"));
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
        Assert.True(other.Layout.IsVisible("textures"));
        Assert.True(other.Layout.Restore(text));

        Assert.False(other.Layout.IsVisible("textures"));
        Assert.True(other.Layout.IsVisible("scene"));
        Assert.Equal(other.Registry.Definitions.Count, Walk(other.Layout.Root!).OfType<PanelTool>().Count());
        foreach (var tool in Walk(other.Layout.Root!).OfType<PanelTool>())
        {
            Assert.Same(other.Registry.GetViewModel(tool.Id), tool.Context);
        }

        Assert.True(other.Layout.SetVisible("textures", true));
        Assert.True(other.Layout.IsVisible("textures"));
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
        other.Registry.RegisterCustom("custom.later", "Later");
        Assert.True(other.Layout.Restore(text));

        Assert.True(other.Layout.IsVisible("custom.later"));
    }

    [Fact]
    public void ACustomPanelInASavedLayoutIsRegisteredAgain()
    {
        var h = new Harness();
        h.Registry.RegisterCustom("custom.player", "Player");
        _ = h.Layout.Root;
        var text = h.Layout.Save();

        var other = new Harness();
        Assert.True(other.Layout.Restore(text));

        Assert.True(other.Registry.TryGet("custom.player", out var definition));
        Assert.Equal("Player", definition.Title);
        Assert.True(other.Layout.IsVisible("custom.player"));
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
}
