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

using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.App.Views;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

/// <summary>
/// Switching between connected games: the dropdown, the selection, and the panels that follow it.
/// </summary>
/// <remarks>
/// The tests that use a real <see cref="ComboBox"/> run on the headless platform, because the bug they guard
/// against lives in the way the control and the ViewModel talk to each other, not in either one alone.
/// </remarks>
public class SessionSwitchingTests
{
    // The main ViewModel is created on first use, so touch it before any game connects.
    private static Harness Start()
    {
        var h = new Harness();
        _ = h.Main;
        return h;
    }

    private static void Pump(Window window)
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private static FakeGameSession Seed(string title, string platform, string device, int fps, string logLine)
    {
        var game = new FakeGameSession(title, platform, device);
        game.Stats.Add(TestData.Stats(fps));
        game.Logs.Add(TestData.Log(fps, logLine));
        game.Watches = new WatchesPayload([TestData.Named("who", PropertyValue.FromString(logLine))]);
        game.Textures = TestData.Textures(TestData.Texture(fps, logLine + ".png"));
        return game;
    }

    // Reads what the panels show, so a test can tell which game they follow.
    private static string[] PanelSnapshot(Harness h)
    {
        h.Scheduler.Tick();
        var stats = (StatsPanelViewModel)h.Registry.GetViewModel("stats");
        var watches = (WatchesPanelViewModel)h.Registry.GetViewModel("watches");
        var textures = (TexturesPanelViewModel)h.Registry.GetViewModel("textures");
        return
        [
            stats.Groups[0].Rows[0].Value,
            string.Join(",", h.Logs.Rows.Select(r => r.Seq)),
            string.Join(",", watches.Rows.Select(r => r.Value)),
            string.Join(",", textures.Rows.Select(r => r.Name)),
        ];
    }

    [Fact]
    public async Task PickingTheOtherGameInTheRealDropdownAfterOneDisconnectsShowsItsData()
    {
        var result = await TestAppBuilder.Session.Dispatch(
            () =>
            {
                var h = Start();
                var window = new MainWindow(h.Get<WindowNotificationService>()) { DataContext = h.Main };
                var desktop = Seed("Game", "desktop", "Linux", 60, "desktop");
                var web = Seed("Game", "web", "Linux", 30, "web");
                h.Host.Connect(desktop);
                h.Host.Connect(web);
                window.Show();
                Pump(window);
                var combo = window.GetVisualDescendants().OfType<ComboBox>().First();
                _ = PanelSnapshot(h);

                h.Host.Disconnect(desktop);
                h.Scheduler.Tick();
                Pump(window);

                // The same thing a click on the second entry does.
                combo.SelectedItem = h.Main.Sessions[1];
                Pump(window);
                var panels = PanelSnapshot(h);
                var outcome = (
                    ActiveIsWeb: ReferenceEquals(h.Active.Current, web),
                    SelectedIsWeb: ReferenceEquals(h.Main.SelectedSession?.Session, web),
                    ComboShowsWeb: ReferenceEquals((combo.SelectedItem as SessionItemViewModel)?.Session, web),
                    h.Main.HasActiveSession,
                    h.Main.IsActiveDisconnected,
                    Panels: string.Join("|", panels));
                window.Close();
                return outcome;
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.ActiveIsWeb);
        Assert.True(result.SelectedIsWeb);
        Assert.True(result.ComboShowsWeb);
        Assert.True(result.HasActiveSession);
        Assert.False(result.IsActiveDisconnected);
        Assert.Equal("30|30|web|web.png", result.Panels);
    }

    [Fact]
    public async Task TheRealDropdownKeepsItsSelectionWhileTheListChanges()
    {
        var result = await TestAppBuilder.Session.Dispatch(
            () =>
            {
                var h = Start();
                var window = new MainWindow(h.Get<WindowNotificationService>()) { DataContext = h.Main };
                var first = Seed("One", "desktop", "Linux", 60, "one");
                var second = Seed("Two", "desktop", "Linux", 30, "two");
                h.Host.Connect(first);
                h.Host.Connect(second);
                window.Show();
                Pump(window);
                var combo = window.GetVisualDescendants().OfType<ComboBox>().First();
                var switches = 0;
                h.Active.Changed += (_, _) => switches++;

                h.Host.Disconnect(first);
                h.Scheduler.Tick();
                Pump(window);

                // A third game joins, the closed first game is dropped, and the dropdown rebuilds its entries.
                h.Host.Connect(Seed("Three", "web", "Linux", 20, "three"));
                combo.SelectedItem = h.Main.Sessions[^1];
                Pump(window);
                combo.SelectedItem = h.Main.Sessions.First(s => ReferenceEquals(s.Session, second));
                h.Scheduler.Tick();
                Pump(window);
                var outcome = (
                    ActiveIsSecond: ReferenceEquals(h.Active.Current, second),
                    ComboShowsSecond: ReferenceEquals((combo.SelectedItem as SessionItemViewModel)?.Session, second),
                    Entries: h.Main.Sessions.Count,
                    Switches: switches);
                window.Close();
                return outcome;
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.ActiveIsSecond);
        Assert.True(result.ComboShowsSecond);
        Assert.Equal(2, result.Entries);
        Assert.Equal(2, result.Switches);
    }

    [Fact]
    public void PickingTheOtherGameAfterTheActiveOneDisconnectsMakesEveryPanelFollowIt()
    {
        var h = Start();
        var probe = RegisterProbe(h);
        var desktop = Seed("Game", "desktop", "Linux", 60, "desktop");
        var web = Seed("Game", "web", "Linux", 30, "web");
        h.Host.Connect(desktop);
        h.Host.Connect(web);
        Assert.Equal(["60", "60", "desktop", "desktop.png"], PanelSnapshot(h));

        h.Host.Disconnect(desktop);
        h.Main.SelectedSession = h.Main.Sessions.First(s => ReferenceEquals(s.Session, web));
        h.Scheduler.Tick();

        Assert.Equal(["30", "30", "web", "web.png"], PanelSnapshot(h));
        Assert.Same(web, probe.Seen);
        Assert.True(h.Main.HasActiveSession);
        Assert.False(h.Main.IsActiveDisconnected);
        Assert.Equal("Game (web)", h.Main.GameText);
    }

    [Fact]
    public void TheActiveGameThatDisconnectsShowsDisconnectedKeepsItsDataAndTheListKeepsTheLiveGames()
    {
        var h = Start();
        var desktop = Seed("Game", "desktop", "Linux", 60, "desktop");
        var web = Seed("Game", "web", "Linux", 30, "web");
        h.Host.Connect(desktop);
        h.Host.Connect(web);

        h.Host.Disconnect(desktop);
        h.Scheduler.Tick();

        Assert.True(h.Main.IsActiveDisconnected);
        Assert.Contains("Disconnected", h.Main.GameText);
        Assert.Same(desktop, h.Active.Current);
        Assert.Equal(["60", "60", "desktop", "desktop.png"], PanelSnapshot(h));
        Assert.Equal(2, h.Main.Sessions.Count);
        Assert.Contains("disconnected", h.Main.Sessions[0].Label);
        Assert.DoesNotContain("disconnected", h.Main.Sessions[1].Label);
        Assert.Contains(h.Main.SelectedSession, h.Main.Sessions);
    }

    [Fact]
    public void ADisconnectedEntryThatIsSelectedAgainShowsItsLastDataNotTheEmptyView()
    {
        var h = Start();
        var desktop = Seed("Game", "desktop", "Linux", 60, "desktop");
        var web = Seed("Game", "web", "Linux", 30, "web");
        h.Host.Connect(desktop);
        h.Host.Connect(web);
        var closedEntry = h.Main.Sessions[0];
        h.Host.Disconnect(desktop);
        h.Main.SelectedSession = h.Main.Sessions[1];

        h.Main.SelectedSession = closedEntry;

        Assert.Same(desktop, h.Active.Current);
        Assert.True(h.Main.HasActiveSession);
        Assert.True(h.Main.IsActiveDisconnected);
        Assert.Equal(["60", "60", "desktop", "desktop.png"], PanelSnapshot(h));

        // Staying on the closed entry keeps it in the list, however many ticks pass.
        h.Scheduler.Tick();
        h.Scheduler.Tick();
        Assert.Contains(closedEntry, h.Main.Sessions);
    }

    [Fact]
    public void AClosedEntryThatIsNoLongerSelectedLeavesTheListOnTheNextTickNotBefore()
    {
        var h = Start();
        var first = h.Connect("One");
        h.Connect("Two");
        h.Host.Disconnect(first);

        h.Main.SelectedSession = h.Main.Sessions[1];

        // The dropdown is still handling the choice, so the list must not change yet.
        Assert.Equal(2, h.Main.Sessions.Count);

        h.Scheduler.Tick();

        Assert.Single(h.Main.Sessions);
        Assert.Equal("Two", h.Main.SelectedSession!.Title);
    }

    [Fact]
    public void AReconnectingGameDoesNotTakeOverWhenThePersonChoseAnotherLiveGame()
    {
        var h = Start();
        var main = h.Main;
        var first = h.Connect("Game", "Pixel");
        var other = h.Connect("Other", "Laptop");
        h.Host.Disconnect(first);
        main.SelectedSession = main.Sessions[1];
        var switches = 0;
        h.Active.Changed += (_, _) => switches++;

        var back = h.Connect("Game", "Pixel");

        Assert.Same(other, h.Active.Current);
        Assert.Equal(0, switches);
        Assert.Contains(main.Sessions, s => ReferenceEquals(s.Session, back));
        Assert.DoesNotContain(main.Sessions, s => ReferenceEquals(s.Session, first));
        Assert.Same(other, main.SelectedSession?.Session);
    }

    [Fact]
    public void AReconnectingGameIsSelectedWhenItWasTheClosedSelectionEvenWithOthersConnected()
    {
        var h = Start();
        var first = h.Connect("Game", "Pixel");
        h.Connect("Other", "Laptop");
        h.Host.Disconnect(first);

        var back = h.Connect("Game", "Pixel");

        Assert.Same(back, h.Active.Current);
        Assert.False(h.Main.IsActiveDisconnected);
        Assert.Equal(2, h.Main.Sessions.Count);
    }

    [Fact]
    public void DisconnectingAGameThatIsNotActiveLeavesTheActiveOneAlone()
    {
        var h = Start();
        var first = Seed("One", "desktop", "Linux", 60, "one");
        var second = Seed("Two", "web", "Linux", 30, "two");
        h.Host.Connect(first);
        h.Host.Connect(second);
        var switches = 0;
        h.Active.Changed += (_, _) => switches++;
        _ = PanelSnapshot(h);

        h.Host.Disconnect(second);
        h.Scheduler.Tick();

        Assert.Equal(0, switches);
        Assert.Same(first, h.Active.Current);
        Assert.False(h.Main.IsActiveDisconnected);
        Assert.Equal(["60", "60", "one", "one.png"], PanelSnapshot(h));
        Assert.Single(h.Main.Sessions);
        Assert.Same(first, h.Main.SelectedSession?.Session);
    }

    [Fact]
    public void AfterTheLastGameDisconnectsTheSameGameComingBackIsSelectedAutomatically()
    {
        var h = Start();
        var probe = RegisterProbe(h);
        var first = Seed("Game", "desktop", "Linux", 60, "old");
        h.Host.Connect(first);
        h.Host.Disconnect(first);
        Assert.True(h.Main.IsActiveDisconnected);

        var back = Seed("Game", "desktop", "Linux", 144, "new");
        h.Host.Connect(back);

        Assert.Same(back, h.Active.Current);
        Assert.Same(back, probe.Seen);
        Assert.False(h.Main.IsActiveDisconnected);
        Assert.Equal(["144", "144", "new", "new.png"], PanelSnapshot(h));
        Assert.Single(h.Main.Sessions);
    }

    [Fact]
    public void AfterTheLastGameDisconnectsADifferentGameCanBePickedAndShowsItsData()
    {
        var h = Start();
        var first = Seed("Game", "desktop", "Linux", 60, "old");
        h.Host.Connect(first);
        h.Host.Disconnect(first);

        var other = Seed("Other", "web", "Browser", 30, "other");
        h.Host.Connect(other);

        // The closed game stays in front, so its last data can still be read.
        Assert.Same(first, h.Active.Current);

        h.Main.SelectedSession = h.Main.Sessions[1];

        Assert.Same(other, h.Active.Current);
        Assert.Equal(["30", "30", "other", "other.png"], PanelSnapshot(h));
    }

    [Fact]
    public void ADropdownThatReportsNoSelectionWhileTheListIsRebuiltIsIgnored()
    {
        var h = Start();
        var first = Seed("One", "desktop", "Linux", 60, "one");
        h.Host.Connect(first);
        h.Host.Connect(Seed("Two", "web", "Linux", 30, "two"));
        var selected = h.Main.SelectedSession;
        var switches = 0;
        h.Active.Changed += (_, _) => switches++;
        var raised = new List<string?>();
        h.Main.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        h.Main.SelectedSession = null;

        Assert.Same(selected, h.Main.SelectedSession);
        Assert.Same(first, h.Active.Current);
        Assert.Equal(0, switches);
        Assert.True(h.Main.HasActiveSession);

        // The change is raised again so the control goes back to showing the real selection.
        Assert.Contains(nameof(MainWindowViewModel.SelectedSession), raised);
    }

    [Fact]
    public void AddingAndRemovingEntriesDoesNotResetThePanels()
    {
        var h = Start();
        h.Host.Connect(Seed("One", "desktop", "Linux", 60, "one"));
        var gone = Seed("Two", "web", "Linux", 30, "two");
        h.Host.Connect(gone);
        var switches = 0;
        h.Active.Changed += (_, _) => switches++;

        h.Host.Connect(Seed("Three", "android", "Pixel", 20, "three"));
        h.Host.Disconnect(gone);
        h.Scheduler.Tick();

        Assert.Equal(0, switches);
        Assert.Equal(["60", "60", "one", "one.png"], PanelSnapshot(h));
    }

    [Fact]
    public async Task SessionEventsFromBackgroundThreadsAreHandedToTheUiScheduler()
    {
        var h = Start();
        h.Scheduler.Deferred = true;
        var main = h.Main;
        var game = Seed("Game", "desktop", "Linux", 60, "game");

        await Task.Run(() => h.Host.Connect(game), TestContext.Current.CancellationToken);

        Assert.Empty(main.Sessions);
        Assert.Null(h.Active.Current);
        Assert.True(h.Scheduler.Pending > 0);

        h.Scheduler.Flush();
        Assert.Single(main.Sessions);
        Assert.Same(game, h.Active.Current);

        await Task.Run(() => h.Host.Disconnect(game), TestContext.Current.CancellationToken);

        Assert.False(main.IsActiveDisconnected);
        Assert.True(h.Scheduler.Pending > 0);

        h.Scheduler.Flush();
        Assert.True(main.IsActiveDisconnected);
    }

    private static ProbePanel RegisterProbe(Harness h)
    {
        h.Registry.Register(new PanelDefinition("probe", "Probe", _ => new ProbePanel(h.Active)));
        return (ProbePanel)h.Registry.GetViewModel("probe");
    }

    // A minimal panel that only follows the active game, which is all the base class promises every panel does.
    private sealed class ProbePanel : PanelViewModel
    {
        public ProbePanel(IActiveSession active)
            : base("probe", "Probe", active)
        {
            Seen = Session;
        }

        public IGameSession? Seen { get; private set; }

        protected override void OnSessionChanged(IGameSession? oldSession, IGameSession? newSession) => Seen = newSession;
    }
}
