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

using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.Core.Adb;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void TheFirstGameThatConnectsIsSelectedAutomatically()
    {
        var h = new Harness();
        var main = h.Main;
        Assert.True(main.HasNoSessions);
        Assert.Null(h.Active.Current);

        var game = h.Connect();

        Assert.False(main.HasNoSessions);
        Assert.Same(game, main.SelectedSession?.Session);
        Assert.Same(game, h.Active.Current);
        Assert.True(main.HasActiveSession);
    }

    [Fact]
    public void AGameThatConnectsLaterDoesNotTakeOverTheSelection()
    {
        var h = new Harness();
        var main = h.Main;
        var first = h.Connect("One");
        h.Connect("Two");

        Assert.Equal(2, main.Sessions.Count);
        Assert.Same(first, h.Active.Current);
    }

    [Fact]
    public void ChoosingAnotherGameMakesItTheActiveOne()
    {
        var h = new Harness();
        var main = h.Main;
        h.Connect("One");
        var second = h.Connect("Two");

        main.SelectedSession = main.Sessions[1];

        Assert.Same(second, h.Active.Current);
    }

    [Fact]
    public void ListEntriesShowTitlePlatformDeviceAndPing()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect("Cool Game", "Pixel 8");
        game.PingMs = 12.4;
        h.Scheduler.Tick();

        var item = main.Sessions[0];
        Assert.Equal("Cool Game", item.Title);
        Assert.Equal("desktop", item.Platform);
        Assert.Equal("Pixel 8", item.Device);
        Assert.Equal("12 ms", item.PingText);
        Assert.Contains("Cool Game", item.Label);
        Assert.Contains("Pixel 8", item.Label);
        Assert.Equal("12 ms", main.PingText);
    }

    [Fact]
    public void ADisconnectedActiveGameStaysSelectedWithItsData()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect();
        game.Logs.Add(TestData.Log(1, "last words"));

        h.Host.Disconnect(game);

        Assert.True(main.IsActiveDisconnected);
        Assert.Contains("Disconnected", main.GameText);
        Assert.Same(game, h.Active.Current);
        Assert.Equal(1, game.Logs.Count);
        Assert.False(main.SelectedSession!.IsConnected);
    }

    [Fact]
    public void TheSameGameReconnectingIsSelectedAndReplacesTheOldEntry()
    {
        var h = new Harness();
        var main = h.Main;
        var old = h.Connect("Game", "Pixel");
        h.Host.Disconnect(old);

        var fresh = h.Connect("Game", "Pixel");

        Assert.Same(fresh, h.Active.Current);
        Assert.Single(main.Sessions);
        Assert.False(main.IsActiveDisconnected);
    }

    [Fact]
    public void ADifferentGameDoesNotReplaceTheDisconnectedSelection()
    {
        var h = new Harness();
        var main = h.Main;
        var old = h.Connect("Game", "Pixel");
        h.Host.Disconnect(old);

        h.Connect("Other", "Laptop");

        Assert.Same(old, h.Active.Current);
        Assert.True(main.IsActiveDisconnected);
        Assert.Equal(2, main.Sessions.Count);

        main.SelectedSession = main.Sessions[1];
        h.Scheduler.Tick();

        // Once the person moves on, the old disconnected entry is dropped on the next status tick.
        Assert.Single(main.Sessions);
    }

    [Fact]
    public void MenuCheckItemsReflectTheControlsPayloadWithoutSendingActions()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect();

        game.Controls = TestData.Controls(paused: true, hitboxes: true, freeCam: false);
        game.RaisePanelUpdated(PanelKind.Controls, "controls");

        Assert.True(main.IsPaused);
        Assert.True(main.ShowHitboxes);
        Assert.False(main.FreeCamera);
        Assert.Empty(game.Actions);
    }

    [Fact]
    public void ClickingACheckItemSendsTheMatchingAction()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect();

        main.ShowHitboxes = true;
        main.IsPaused = true;
        main.FreeCamera = true;
        main.ToggleHitboxesCommand.Execute(null);

        Assert.Equal(["hitboxes:True", "pause:True", "freecam:True", "hitboxes:False"], game.Actions);
    }

    [Fact]
    public async Task StepFrameAndClearLogUseTheActiveGame()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect();
        game.Logs.Add(TestData.Log(1));

        await main.StepFrameCommand.ExecuteAsync(null);
        await main.ClearLogCommand.ExecuteAsync(null);

        Assert.Equal(["step", "clearLogs"], game.Actions);
        Assert.Equal(0, game.Logs.Count);
    }

    [Fact]
    public void TheStatusBarShowsFpsHeapAndPausedState()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect("Cool Game");
        Assert.Equal("- FPS | - MB | Running", main.StatusText);

        game.Stats.Add(TestData.Stats(59.6f, 64.25f));
        h.Scheduler.Tick();
        Assert.Equal("60 FPS | 64.3 MB | Running", main.StatusText);
        Assert.Equal("Cool Game (desktop)", main.GameText);

        game.Stats.Add(TestData.Stats(30f, 70f, paused: true));
        h.Scheduler.Tick();
        Assert.Equal("30 FPS | 70.0 MB | PAUSED", main.StatusText);
    }

    [Fact]
    public async Task APortInUseShowsTheBannerAndRetryClearsIt()
    {
        var h = new Harness();
        var main = h.Main;
        h.Host.StartFailure = new PortInUseException(6510, new InvalidOperationException());

        await main.InitializeAsync();

        Assert.True(main.HasServerError);
        Assert.Equal(
            "Port 6510 is already in use. Close the other debugger or change the port in Settings.",
            main.ServerError);

        h.Host.StartFailure = null;
        await main.StartServerCommand.ExecuteAsync(null);

        Assert.False(main.HasServerError);
        Assert.True(h.Host.IsRunning);
        Assert.Equal("ws://127.0.0.1:6510/", main.ListenText);
    }

    [Fact]
    public async Task StartingUsesTheSavedSettings()
    {
        var h = new Harness();
        h.Settings.Current.Port = 7000;
        h.Settings.Current.ListenOnAllInterfaces = true;

        await h.Main.InitializeAsync();

        Assert.Equal(["configure:7000:True", "start"], h.Host.Calls);
        Assert.Equal("ws://0.0.0.0:7000/", h.Main.ListenText);
    }

    [Fact]
    public async Task ChangingThePortRestartsTheServerAndSavesTheSettings()
    {
        var h = new Harness();
        var main = h.Main;
        await main.InitializeAsync();
        h.Host.Calls.Clear();

        await main.ApplySettingsAsync(7001, false);

        Assert.Equal(["stop", "configure:7001:False", "start"], h.Host.Calls);
        Assert.Equal(7001, h.Settings.Current.Port);
        Assert.Equal(1, h.Settings.Saves);
    }

    [Fact]
    public async Task UnchangedSettingsDoNotRestartTheServer()
    {
        var h = new Harness();
        var main = h.Main;
        await main.InitializeAsync();
        h.Host.Calls.Clear();

        await main.ApplySettingsAsync(6510, false);

        Assert.Empty(h.Host.Calls);
    }

    [Fact]
    public async Task TheSettingsDialogAppliesWhatWasSaved()
    {
        var h = new Harness();
        var main = h.Main;
        await main.InitializeAsync();
        h.Dialogs.SettingsResult = true;
        h.Dialogs.EditSettings = s =>
        {
            s.PortText = "7002";
            s.ListenOnAllInterfaces = true;
        };

        await main.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(7002, h.Settings.Current.Port);
        Assert.True(h.Settings.Current.ListenOnAllInterfaces);
        Assert.Equal(7002, h.Host.Port);
    }

    [Fact]
    public async Task CancellingTheSettingsDialogChangesNothing()
    {
        var h = new Harness();
        var main = h.Main;
        await main.InitializeAsync();
        h.Host.Calls.Clear();
        h.Dialogs.SettingsResult = false;
        h.Dialogs.EditSettings = s => s.PortText = "7003";

        await main.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(6510, h.Settings.Current.Port);
        Assert.Empty(h.Host.Calls);
    }

    [Fact]
    public async Task ConnectAndroidShowsTheFriendlyMessage()
    {
        var h = new Harness();
        h.Adb.Results.Enqueue(new AdbResult(AdbStatus.AdbNotFound, "adb was not found.", []));

        await h.Main.ConnectAndroidCommand.ExecuteAsync(null);

        Assert.Equal([(6510, (string?)null)], h.Adb.Calls);
        Assert.Equal([(NotificationLevel.Warning, "adb was not found.")], h.Notifier.Messages);
    }

    [Fact]
    public async Task ConnectAndroidWithSeveralDevicesAsksWhichOne()
    {
        var h = new Harness();
        AdbDevice[] devices =
        [
            new("a1", AdbDeviceState.Ready, "Pixel", null, "device"),
            new("b2", AdbDeviceState.Ready, "Galaxy", null, "device"),
        ];
        h.Adb.Results.Enqueue(new AdbResult(AdbStatus.ManyDevices, "More than one device is connected.", devices));
        h.Adb.Results.Enqueue(new AdbResult(AdbStatus.Ok, "Port 6510 is forwarded.", devices));
        h.Dialogs.PickedSerial = "b2";

        await h.Main.ConnectAndroidCommand.ExecuteAsync(null);

        Assert.Equal([(6510, (string?)null), (6510, "b2")], h.Adb.Calls);
        Assert.Equal(NotificationLevel.Success, h.Notifier.Messages[0].Level);
    }

    [Fact]
    public async Task CancellingTheDevicePickerStopsQuietly()
    {
        var h = new Harness();
        h.Adb.Results.Enqueue(new AdbResult(AdbStatus.ManyDevices, "More than one device.", []));
        h.Dialogs.PickedSerial = null;

        await h.Main.ConnectAndroidCommand.ExecuteAsync(null);

        Assert.Single(h.Adb.Calls);
        Assert.Empty(h.Notifier.Messages);
    }

    [Fact]
    public void ACustomPanelAnnouncedByTheGameAppearsInTheRegistryTheMenuAndTheLayout()
    {
        var h = new Harness();
        var main = h.Main;
        var game = h.Connect();

        game.Panels = [new SessionPanel("custom.player", "Player", PanelKind.Custom)];
        game.RaisePanelListChanged();

        Assert.True(h.Registry.TryGet("custom.player", out _));
        Assert.True(h.Layout.IsVisible("custom.player"));
        Assert.Contains(main.PanelItems, i => i.Id == "custom.player" && i.IsVisible);
    }

    [Fact]
    public void StateOfASessionThatClosesIsVisibleThroughTheStatusTimer()
    {
        var h = new Harness();
        var game = h.Connect();
        game.State = SessionState.Closed;

        h.Scheduler.Tick();

        Assert.True(h.Main.IsActiveDisconnected);
    }
}
