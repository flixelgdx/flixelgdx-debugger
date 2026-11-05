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
using FlixelDebugger.Core.Graphs;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class PerformancePanelViewModelTests
{
    private static PerformancePanelViewModel Make(PanelFixture f, FakeHost? host = null) =>
        new(f.Active, f.Scheduler, host ?? new FakeHost());

    private static PerformanceSectionViewModel Section(PerformancePanelViewModel vm, string title) =>
        vm.Sections.Single(s => s.Title == title);

    private static void Feed(PanelFixture f, int samples, float fps)
    {
        for (var i = 0; i < samples; i++)
        {
            f.Game.Stats.Add(TestData.Stats(fps));
        }
    }

    [Fact]
    public void SectionsAreCoreRenderMemoryAndNetworkAndOnlyCoreStartsOpen()
    {
        var vm = Make(new PanelFixture());

        Assert.Equal(["Core", "Render", "Memory", "Network"], vm.Sections.Select(s => s.Title));
        Assert.Equal([true, false, false, false], vm.Sections.Select(s => s.IsExpanded));
    }

    [Fact]
    public void OnlyAnExpandedSectionHasGraphs()
    {
        var vm = Make(new PanelFixture());

        Assert.Equal(["FPS", "Frame ms", "Draw calls"], Section(vm, "Core").Graphs.Select(g => g.Title));
        Assert.Empty(Section(vm, "Render").Graphs);
        Assert.Empty(Section(vm, "Memory").Graphs);
        Assert.Empty(Section(vm, "Network").Graphs);

        Section(vm, "Render").IsExpanded = true;
        Section(vm, "Memory").IsExpanded = true;
        Section(vm, "Network").IsExpanded = true;

        Assert.Equal(
            ["CPU ms", "Present ms", "Triangles", "Vertices", "Passes", "Submits", "Pipelines"],
            Section(vm, "Render").Graphs.Select(g => g.Title));
        Assert.Equal(["Heap MB", "Native MB", "Texture MB", "Ring KB"], Section(vm, "Memory").Graphs.Select(g => g.Title));
        Assert.Equal(["Send KB/s", "Receive KB/s", "Ping ms"], Section(vm, "Network").Graphs.Select(g => g.Title));
    }

    [Fact]
    public void CollapsingDropsTheGraphsAndTheyStopUpdating()
    {
        var f = new PanelFixture();
        var vm = Make(f);
        var core = Section(vm, "Core");
        var old = core.Graphs[0];
        var redraws = 0;
        old.Invalidated += (_, _) => redraws++;

        core.IsExpanded = false;
        f.Tick();

        Assert.Empty(core.Graphs);
        Assert.Equal(0, redraws);

        core.IsExpanded = true;

        Assert.Equal(3, core.Graphs.Count);
        Assert.NotSame(old, core.Graphs[0]);
    }

    [Fact]
    public void ExpandedGraphsAreRedrawnOnEveryTick()
    {
        var f = new PanelFixture();
        var vm = Make(f);
        var redraws = 0;
        Section(vm, "Core").Graphs[0].Invalidated += (_, _) => redraws++;

        f.Tick();
        f.Tick();

        Assert.Equal(2, redraws);
    }

    [Fact]
    public void MinimumScalesComeFromTheCatalog()
    {
        var vm = Make(new PanelFixture());
        Section(vm, "Render").IsExpanded = true;

        Assert.Equal(10, Section(vm, "Core").Graphs[0].Scaler.MinScale);
        Assert.Equal(10, Section(vm, "Core").Graphs[2].Scaler.MinScale);
        Assert.Equal(100, Section(vm, "Render").Graphs.Single(g => g.Title == "Triangles").Scaler.MinScale);
        Assert.Equal(GraphCatalog.Sections[0].Graphs[0].MinScale, Section(vm, "Core").Graphs[0].Scaler.MinScale);
    }

    [Fact]
    public void TheScaleGrowsAtOnceAndShrinksOnlyBelowHalf()
    {
        var f = new PanelFixture();
        var vm = Make(f);
        var fps = Section(vm, "Core").Graphs[0];

        Assert.Equal(10, fps.Scaler.Scale);

        Feed(f, 1, 60);
        f.Tick();
        Assert.Equal(80, fps.Scaler.Scale);
        Assert.Equal("80", fps.ScaleText);

        // 45 needs a scale of 60, which is not below half of 80, so the scale stays.
        Feed(f, 120, 45);
        f.Tick();
        Assert.Equal(80, fps.Scaler.Scale);

        // 20 needs a scale of 25, which is below half of 80, so the scale shrinks.
        Feed(f, 120, 20);
        f.Tick();
        Assert.Equal(25, fps.Scaler.Scale);
    }

    [Fact]
    public void TheSeriesIsCopiedIntoTheCallersBufferAndTheLatestValueIsShown()
    {
        var f = new PanelFixture();
        var vm = Make(f);
        var fps = Section(vm, "Core").Graphs[0];
        Assert.Equal("-", fps.LatestText);
        Feed(f, 3, 10);
        f.Game.Stats.Add(TestData.Stats(59.6f));
        f.Tick();

        var buffer = new double[120];
        var count = fps.CopyTo(buffer);

        Assert.Equal(4, count);
        Assert.Equal(10, buffer[0], 3);
        Assert.Equal(59.6, buffer[3], 1);
        Assert.Equal("60", fps.LatestText);
        Assert.Equal("60", fps.FormatValue(buffer[3]));
    }

    [Fact]
    public void ThePingGraphUsesTheMeasuredPings()
    {
        var f = new PanelFixture();
        var vm = Make(f);
        Section(vm, "Network").IsExpanded = true;
        var ping = Section(vm, "Network").Graphs[2];

        f.Game.Pings.Add(3.5);
        f.Tick();

        Assert.Equal("3.5", ping.LatestText);
        Assert.Equal(1, ping.Count);
    }

    [Fact]
    public async Task ShowsTheSampleRateAndTheListenAddress()
    {
        var f = new PanelFixture();
        var host = new FakeHost();
        var vm = Make(f, host);
        Assert.Equal("-", vm.SampleRateText);
        Assert.Equal("Server stopped", vm.ListenAddress);

        await host.StartAsync(TestContext.Current.CancellationToken);
        f.Game.Controls = new ControlsPayload(false, false, false, 25, LogLevel.Default, []);
        f.Tick();

        Assert.Equal("25 Hz", vm.SampleRateText);
        Assert.Equal("ws://127.0.0.1:6510/", vm.ListenAddress);
    }

    [Fact]
    public void SwitchingTheGameRebindsTheGraphs()
    {
        var f = new PanelFixture();
        Feed(f, 1, 30);
        var vm = Make(f);
        var fps = Section(vm, "Core").Graphs[0];
        Assert.Equal("30", fps.LatestText);
        var other = new FakeGameSession("Other");
        other.Stats.Add(TestData.Stats(144));

        f.Active.Current = other;

        Assert.Equal("144", fps.LatestText);
    }
}
