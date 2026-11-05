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

using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FlixelDebugger.App.Tests;

public class PanelRegistryTests
{
    private static readonly string[] _builtInIds =
    [
        "stats", "performance", "controls", "watches", "trackers", "logs", "console", "scene", "inspector", "textures", "save",
    ];

    [Fact]
    public void EveryBuiltInPanelIsRegisteredInMenuOrder()
    {
        var registry = new Harness().Registry;

        Assert.Equal(_builtInIds, registry.Definitions.Select(d => d.Id));
        Assert.Equal(
            ["Stats", "Performance", "Controls", "Watches", "Trackers", "Logs", "Console", "Scene", "Inspector", "Textures", "Save"],
            registry.Definitions.Select(d => d.Title));
    }

    [Fact]
    public void EveryBuiltInPanelHasItsOwnViewModel()
    {
        var registry = new Harness().Registry;
        var expected = new Dictionary<string, Type>
        {
            ["stats"] = typeof(StatsPanelViewModel),
            ["performance"] = typeof(PerformancePanelViewModel),
            ["controls"] = typeof(ControlsPanelViewModel),
            ["watches"] = typeof(WatchesPanelViewModel),
            ["trackers"] = typeof(TrackersPanelViewModel),
            ["logs"] = typeof(LogsPanelViewModel),
            ["console"] = typeof(ConsolePanelViewModel),
            ["scene"] = typeof(ScenePanelViewModel),
            ["inspector"] = typeof(InspectorPanelViewModel),
            ["textures"] = typeof(TexturesPanelViewModel),
            ["save"] = typeof(SavePanelViewModel),
        };

        foreach (var definition in registry.Definitions)
        {
            var viewModel = registry.GetViewModel(definition.Id);
            Assert.Equal(definition.Id, viewModel.Id);
            Assert.Equal(definition.Title, viewModel.Title);
            Assert.IsType(expected[definition.Id], viewModel);
        }
    }

    [Fact]
    public void TryFindMatchesIdOrTitleIgnoringCase()
    {
        var registry = new Harness().Registry;

        Assert.True(registry.TryFind("SCENE", out var byId));
        Assert.Equal("scene", byId.Id);
        Assert.True(registry.TryFind(" Textures ", out var byTitle));
        Assert.Equal("textures", byTitle.Id);
        Assert.False(registry.TryFind("nope", out _));
        Assert.False(registry.TryGet("Scene", out _));
    }

    [Fact]
    public void ViewModelsAreCreatedOnceAndShared()
    {
        var registry = new Harness().Registry;

        Assert.Same(registry.GetViewModel("scene"), registry.GetViewModel("scene"));
        Assert.Throws<KeyNotFoundException>(() => registry.GetViewModel("missing"));
    }

    [Fact]
    public void RegisteringAnExistingIdSwapsThePanelAndRaisesChanged()
    {
        var h = new Harness();
        var registry = h.Registry;
        var before = registry.GetViewModel("scene");
        var raised = 0;
        registry.Changed += (_, _) => raised++;
        var real = new PanelDefinition("scene", "Scene", sp => new CustomPanelViewModel("scene", "Scene 2", sp.GetRequiredService<IActiveSession>(), sp.GetRequiredService<IUiScheduler>()));

        registry.Register(real);

        Assert.Equal(1, raised);
        Assert.Equal(_builtInIds, registry.Definitions.Select(d => d.Id));
        Assert.NotSame(before, registry.GetViewModel("scene"));
        Assert.Equal("Scene 2", registry.GetViewModel("scene").Title);
    }

    [Fact]
    public void RegisteringANewIdAddsItAtTheEnd()
    {
        var registry = new Harness().Registry;

        registry.Register(new PanelDefinition("extra", "Extra", sp => new CustomPanelViewModel("extra", "Extra", sp.GetRequiredService<IActiveSession>(), sp.GetRequiredService<IUiScheduler>())));

        Assert.Equal("extra", registry.Definitions[^1].Id);
    }

    [Fact]
    public void CustomPanelsAreRegisteredOnceWithTheGenericView()
    {
        var registry = new Harness().Registry;

        Assert.True(registry.RegisterCustom("custom.player", "Player"));
        Assert.False(registry.RegisterCustom("custom.player", "Player again"));

        Assert.True(registry.TryGet("custom.player", out var definition));
        Assert.True(definition.IsCustom);
        Assert.Equal("Player", definition.Title);
        Assert.IsType<CustomPanelViewModel>(registry.GetViewModel("custom.player"));
        Assert.False(registry.Definitions[0].IsCustom);
    }

    [Fact]
    public void AnEmptyTitleFallsBackToTheId()
    {
        var registry = new Harness().Registry;

        registry.RegisterCustom("custom.x", " ");

        Assert.True(registry.TryGet("custom.x", out var definition));
        Assert.Equal("custom.x", definition.Title);
    }
}
