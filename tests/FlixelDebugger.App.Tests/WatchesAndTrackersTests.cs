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

using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class WatchesAndTrackersTests
{
  [Fact]
  public void WatchesShowTheHintWhileEmpty()
  {
    var vm = new PanelFixture().Watches();

    Assert.True(vm.IsEmpty);
    Assert.Empty(vm.Rows);
  }

  [Fact]
  public void WatchesFormatNumbersByTheSharedRules()
  {
    var f = new PanelFixture();
    f.Game.Watches = new WatchesPayload(
    [
        TestData.Named("hp", PropertyValue.FromInt(5)),
        TestData.Named("speed", PropertyValue.FromFloat(1.5f)),
        TestData.Named("whole", PropertyValue.FromDouble(3.0)),
        TestData.Named("big", PropertyValue.FromLong(12345678901)),
        TestData.Named("name", PropertyValue.FromString("Bob")),
        TestData.Named("alive", PropertyValue.FromBool(true)),
    ]);

    var vm = f.Watches();

    Assert.False(vm.IsEmpty);
    Assert.Equal(["hp", "speed", "whole", "big", "name", "alive"], vm.Rows.Select(r => r.Name));
    Assert.Equal(["5", "1.50", "3", "12345678901", "Bob", "true"], vm.Rows.Select(r => r.Value));
    Assert.All(vm.Rows, r => Assert.False(r.IsError));
  }

  [Fact]
  public void AWatchTheGameFailedToReadShowsAsAnError()
  {
    var f = new PanelFixture();
    f.Game.Watches = new WatchesPayload([TestData.Named("bad", PropertyValue.FromError("boom"))]);

    var vm = f.Watches();

    Assert.Equal("error: boom", vm.Rows[0].Value);
    Assert.True(vm.Rows[0].IsError);
  }

  [Fact]
  public void WatchesUpdateInPlaceAndShrink()
  {
    var f = new PanelFixture();
    f.Game.Watches = new WatchesPayload(
        [TestData.Named("a", PropertyValue.FromInt(1)),
            TestData.Named("b", PropertyValue.FromInt(2))]);
    var vm = f.Watches();
    var first = vm.Rows[0];

    f.Game.Watches = new WatchesPayload([TestData.Named("a", PropertyValue.FromInt(10))]);
    f.Tick();

    Assert.Single(vm.Rows);
    Assert.Same(first, vm.Rows[0]);
    Assert.Equal("10", first.Value);
  }

  [Fact]
  public void TrackersAreOneSectionEachWithRowsInPayloadOrder()
  {
    var f = new PanelFixture();
    f.Game.Trackers = new TrackersPayload(
    [
        new TrackerBlock(
            "Player",
            [TestData.Named("hp", PropertyValue.FromInt(3)),
                TestData.Named("x", PropertyValue.FromFloat(2.5f))]),
        new TrackerBlock("Boss", [TestData.Named("phase", PropertyValue.FromString("two"))]),
    ]);

    var vm = f.Trackers();

    Assert.Equal(["Player", "Boss"], vm.Sections.Select(s => s.Name));
    Assert.Equal(["hp", "x"], vm.Sections[0].Rows.Select(r => r.Name));
    Assert.Equal(["3", "2.50"], vm.Sections[0].Rows.Select(r => r.Value));
    Assert.Equal(["two"], vm.Sections[1].Rows.Select(r => r.Value));
    Assert.All(vm.Sections, s => Assert.True(s.IsExpanded));
  }

  [Fact]
  public void ATrackerThatDisappearsIsHiddenAndKeepsItsExpandedStateWhenItReturns()
  {
    var f = new PanelFixture();
    var player = new TrackerBlock("Player", [TestData.Named("hp", PropertyValue.FromInt(3))]);
    var boss = new TrackerBlock("Boss", [TestData.Named("phase", PropertyValue.FromInt(1))]);
    f.Game.Trackers = new TrackersPayload([player, boss]);
    var vm = f.Trackers();
    vm.Sections[1].IsExpanded = false;
    var bossSection = vm.Sections[1];

    f.Game.Trackers = new TrackersPayload([player]);
    f.Tick();
    Assert.Equal(["Player"], vm.Sections.Select(s => s.Name));

    f.Game.Trackers = new TrackersPayload([player, boss]);
    f.Tick();

    Assert.Equal(["Player", "Boss"], vm.Sections.Select(s => s.Name));
    Assert.Same(bossSection, vm.Sections[1]);
    Assert.False(vm.Sections[1].IsExpanded);
  }

  [Fact]
  public void TrackersWithTheSameNameGetSeparateSectionsAndAnEmptyListIsEmpty()
  {
    var f = new PanelFixture();
    f.Game.Trackers = new TrackersPayload([new TrackerBlock("T", []), new TrackerBlock("T", [])]);
    var vm = f.Trackers();
    Assert.Equal(2, vm.Sections.Count);
    Assert.NotSame(vm.Sections[0], vm.Sections[1]);

    f.Game.Trackers = new TrackersPayload([]);
    f.Tick();

    Assert.True(vm.IsEmpty);
  }
}
