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
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Tests;

public class StatsPanelViewModelTests
{
    private static Dictionary<string, ValueRowViewModel> Rows(StatsPanelViewModel vm) =>
        vm.Groups.SelectMany(g => g.Rows).ToDictionary(r => r.Name);

    [Fact]
    public void GroupsAreClientWorldAndCamera()
    {
        var vm = new PanelFixture().Stats();

        Assert.Equal(["Client", "World", "Camera"], vm.Groups.Select(g => g.Title));
        Assert.Equal(
            ["FPS", "Frame ms", "Java heap MB", "Native MB", "Objects", "Draw calls", "Triangles", "Ping"],
            vm.Groups[0].Rows.Select(r => r.Name));
        Assert.Equal(["State", "Elapsed"], vm.Groups[1].Rows.Select(r => r.Name));
        Assert.Equal(["X", "Y", "Zoom"], vm.Groups[2].Rows.Select(r => r.Name));
    }

    [Fact]
    public void WithoutASampleValuesAreDashesAndPingIsNotAvailable()
    {
        var rows = Rows(new PanelFixture().Stats());

        Assert.Equal("-", rows["FPS"].Value);
        Assert.Equal("-", rows["State"].Value);
        Assert.Equal("n/a", rows["Ping"].Value);
    }

    [Fact]
    public void ShowsTheNewestSampleWithTheRightNumberOfDecimals()
    {
        var f = new PanelFixture();
        f.Game.Stats.Add(TestData.Stats(59.6f));
        var rows = Rows(f.Stats());

        Assert.Equal("60", rows["FPS"].Value);
        Assert.Equal("16.60", rows["Frame ms"].Value);
        Assert.Equal("64.0", rows["Java heap MB"].Value);
        Assert.Equal("32.0", rows["Native MB"].Value);
        Assert.Equal("250", rows["Objects"].Value);
        Assert.Equal("100", rows["Draw calls"].Value);
        Assert.Equal("2000", rows["Triangles"].Value);
        Assert.Equal("RUNNING", rows["State"].Value);
        Assert.Equal("12.5 s", rows["Elapsed"].Value);
        Assert.Equal("10.0", rows["X"].Value);
        Assert.Equal("20.0", rows["Y"].Value);
        Assert.Equal("1.50", rows["Zoom"].Value);
    }

    [Fact]
    public void RunningIsGreenAndPausedIsAmber()
    {
        var f = new PanelFixture();
        var vm = f.Stats();
        f.Game.Stats.Add(TestData.Stats(60));
        vm.Refresh();
        Assert.True(Rows(vm)["State"].IsGood);
        Assert.False(Rows(vm)["State"].IsWarning);

        f.Game.Stats.Add(TestData.Stats(60, paused: true));
        vm.Refresh();

        Assert.Equal("PAUSED", Rows(vm)["State"].Value);
        Assert.True(Rows(vm)["State"].IsWarning);
        Assert.False(Rows(vm)["State"].IsGood);
    }

    [Fact]
    public void PingAppearsOnceItIsMeasured()
    {
        var f = new PanelFixture();
        var vm = f.Stats();

        f.Game.PingMs = 12.4;
        f.Tick();

        Assert.Equal("12 ms", Rows(vm)["Ping"].Value);
    }

    [Fact]
    public void TheTimerBringsTheRowsUpToDateAndKeepsTheSameRowObjects()
    {
        var f = new PanelFixture();
        var vm = f.Stats();
        var fps = Rows(vm)["FPS"];

        f.Game.Stats.Add(TestData.Stats(30));
        f.Tick();

        Assert.Same(fps, Rows(vm)["FPS"]);
        Assert.Equal("30", fps.Value);
    }

    [Fact]
    public void SwitchingTheGameShowsTheOtherGamesNumbers()
    {
        var f = new PanelFixture();
        f.Game.Stats.Add(TestData.Stats(30));
        var vm = f.Stats();
        var other = new FakeGameSession("Other");
        other.Stats.Add(TestData.Stats(144));

        f.Active.Current = other;

        Assert.Equal("144", Rows(vm)["FPS"].Value);

        f.Active.Current = null;

        Assert.Equal("-", Rows(vm)["FPS"].Value);
    }
}
