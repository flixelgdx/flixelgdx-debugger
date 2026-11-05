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

public class ControlsPanelViewModelTests
{
    private static ControlsPayload Payload(
        bool paused = false,
        bool hitboxes = false,
        bool freeCam = false,
        byte rate = 20,
        LogLevel level = LogLevel.Default,
        params Keybind[] keybinds) => new(paused, hitboxes, freeCam, rate, level, keybinds);

    [Fact]
    public void CopiesTheGamesStateWithoutSendingAnything()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload(paused: true, hitboxes: true, rate: 12, level: LogLevel.Warn);

        var vm = f.Controls();

        Assert.True(vm.Paused);
        Assert.True(vm.ShowHitboxes);
        Assert.False(vm.FreeCam);
        Assert.Equal(12, vm.RateHz);
        Assert.Equal("12 Hz", vm.RateText);
        Assert.Empty(f.Game.Actions);
    }

    [Fact]
    public void EveryCheckboxSendsItsAction()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload();
        var vm = f.Controls();

        vm.ShowHitboxes = true;
        vm.Paused = true;
        vm.FreeCam = true;
        vm.Paused = false;

        Assert.Equal(["hitboxes:True", "pause:True", "freecam:True", "pause:False"], f.Game.Actions);
    }

    [Fact]
    public void ANewPayloadResyncsTheCheckboxesWithoutAnEcho()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload();
        var vm = f.Controls();

        f.Game.Controls = Payload(freeCam: true, hitboxes: true);
        f.Tick();

        Assert.True(vm.FreeCam);
        Assert.True(vm.ShowHitboxes);
        Assert.Empty(f.Game.Actions);
    }

    [Fact]
    public void StepFrameSendsStep()
    {
        var f = new PanelFixture();
        var vm = f.Controls();

        vm.StepFrameCommand.Execute(null);

        Assert.Equal(["step"], f.Game.Actions);
    }

    [Fact]
    public void LogLevelButtonsSendAndTheActiveOneComesFromThePayload()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload(level: LogLevel.Warn);
        var vm = f.Controls();

        Assert.Equal(["Default", "Debug", "Info", "Warn", "Error"], vm.LogLevels.Select(l => l.Label));
        Assert.Equal(["Warn"], vm.LogLevels.Where(l => l.IsActive).Select(l => l.Label));

        vm.SetLogLevelCommand.Execute(LogLevel.Error);

        Assert.Equal(["logLevel:Error"], f.Game.Actions);
        Assert.Equal(["Warn"], vm.LogLevels.Where(l => l.IsActive).Select(l => l.Label));

        f.Game.Controls = Payload(level: LogLevel.Error);
        f.Tick();

        Assert.Equal(["Error"], vm.LogLevels.Where(l => l.IsActive).Select(l => l.Label));
    }

    [Fact]
    public void KeybindsComeFromThePayloadAndTheWindowShortcutsAreListed()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload(keybinds: [new Keybind("F3", "Pause"), new Keybind("Alt + Click", "Select")]);

        var vm = f.Controls();

        Assert.Equal([("F3", "Pause"), ("Alt + Click", "Select")], vm.Keybinds.Select(k => (k.Key, k.Description)));
        Assert.Equal(["F2", "F3", "F4"], vm.AppShortcuts.Select(k => k.Key));
    }

    [Fact]
    public void TheSliderSendsOnceWhenTheDragEnds()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload();
        var vm = f.Controls();

        vm.BeginRateDrag();
        vm.RateHz = 7;
        vm.RateHz = 9;
        f.Tick();
        f.Tick();
        f.Tick();
        Assert.Empty(f.Game.Actions);

        vm.EndRateDrag();

        Assert.Equal(["rate:9"], f.Game.Actions);
    }

    [Fact]
    public void ANewPayloadDoesNotFightTheSliderWhileTheUserDragsOrWaitsForTheEcho()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload(rate: 20);
        var vm = f.Controls();

        vm.BeginRateDrag();
        vm.RateHz = 9;
        f.Game.Controls = Payload(rate: 20, paused: true);
        f.Tick();
        Assert.Equal(9, vm.RateHz);
        Assert.True(vm.Paused);

        vm.EndRateDrag();
        f.Game.Controls = Payload(rate: 20, paused: false);
        f.Tick();
        Assert.Equal(9, vm.RateHz);

        // The game confirms the new rate, so the slider follows the game again.
        f.Game.Controls = Payload(rate: 9);
        f.Tick();
        f.Game.Controls = Payload(rate: 15);
        f.Tick();
        Assert.Equal(15, vm.RateHz);
        Assert.Equal(["rate:9"], f.Game.Actions);
    }

    [Fact]
    public void AKeyboardChangeIsSentAfterAShortPause()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload();
        var vm = f.Controls();

        vm.RateHz = 5;
        f.Tick();
        Assert.Empty(f.Game.Actions);
        vm.RateHz = 6;
        f.Tick();
        Assert.Empty(f.Game.Actions);
        f.Tick();
        f.Tick();
        f.Tick();

        Assert.Equal(["rate:6"], f.Game.Actions);
    }

    [Fact]
    public void TheSliderFollowsTheGameAgainWhenTheEchoNeverComes()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload(rate: 20);
        var vm = f.Controls();
        vm.BeginRateDrag();
        vm.RateHz = 9;
        vm.EndRateDrag();
        f.Game.Controls = Payload(rate: 20, paused: true);

        for (var i = 0; i < 40; i++)
        {
            f.Tick();
        }

        Assert.Equal(20, vm.RateHz);
    }

    [Fact]
    public void ResetReturnsTheRateToTwentyAndSendsIt()
    {
        var f = new PanelFixture();
        f.Game.Controls = Payload(rate: 7);
        var vm = f.Controls();
        Assert.Equal(7, vm.RateHz);

        vm.ResetRateCommand.Execute(null);

        Assert.Equal(20, vm.RateHz);
        Assert.Equal(["rate:20"], f.Game.Actions);
    }

    [Fact]
    public void ResetLayoutAsksTheLayoutAndWithoutAGameTheControlsAreDisabled()
    {
        var f = new PanelFixture(connect: false);
        var vm = f.Controls();
        Assert.False(vm.HasSession);

        vm.ResetLayoutCommand.Execute(null);
        vm.StepFrameCommand.Execute(null);

        Assert.Equal(1, f.Layout.Resets);
        Assert.Empty(f.Game.Actions);

        f.Active.Current = f.Game;
        Assert.True(vm.HasSession);
    }
}
