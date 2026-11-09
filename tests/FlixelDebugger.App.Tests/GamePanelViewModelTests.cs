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
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.Widgets;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;
using static FlixelDebugger.App.Tests.Fakes.TestWidgets;

namespace FlixelDebugger.App.Tests;

public class GamePanelViewModelTests
{
  private static WidgetNode Tree(string title = "Hello") =>
      Node(WidgetType.Stack, 1).With(
          Node(WidgetType.Label, 2, "title", (WidgetProp.Text, S(title))),
          Node(WidgetType.Table, 3, "rows", (WidgetProp.Items, List(Row(1, "a")))));

  private static PanelFixture WithPanel()
  {
    var f = new PanelFixture();
    f.Game.SetPanels(new PanelInfo(0, "game.enemies", "Enemies", PanelDock.Center, true));
    return f;
  }

  [Fact]
  public void TheStatusTellsWhyThereIsNoTree()
  {
    var offline = new PanelFixture(connect: false);
    Assert.Equal("No game is connected.", offline.Panel().StatusText);

    var f = new PanelFixture();
    var vm = f.Panel();
    Assert.Equal("The connected game has no panel with this id.", vm.StatusText);

    f.Game.SetPanels(new PanelInfo(0, "game.enemies", "Enemies", PanelDock.Center, true));
    Assert.Equal("Waiting for the game to send this panel...", vm.StatusText);
    Assert.False(vm.HasRoot);
  }

  [Fact]
  public void ATreeBecomesWidgetsAndPatchesUpdateThem()
  {
    var f = WithPanel();
    var vm = f.Panel();
    f.Game.SendTree("game.enemies", Tree());

    Assert.True(vm.HasRoot);
    Assert.Equal(string.Empty, vm.StatusText);
    var title = vm.FindByName("title")!;
    Assert.Equal("Hello", title.GetString(WidgetProp.Text));
    var changes = new List<WidgetProp>();
    title.Changed += (_, key) => changes.Add(key);

    f.Game.SendPatch(
        "game.enemies",
        new PatchOp(2, PatchCode.Set, WidgetProp.Text, S("Bye")),
        new PatchOp(3, PatchCode.Append, WidgetProp.Items, List(Row(2, "b"))));

    Assert.Equal("Bye", title.GetString(WidgetProp.Text));
    Assert.Equal([WidgetProp.Text], changes);
    Assert.Equal(2, vm.FindByName("rows")!.Get(WidgetProp.Items).Count);
  }

  [Fact]
  public void ANewTreeKeepsWidgetsWithTheSameIdAndType()
  {
    var f = WithPanel();
    var vm = f.Panel();
    f.Game.SendTree("game.enemies", Tree());
    var root = vm.Root;
    var title = vm.FindByName("title");

    f.Game.SendTree("game.enemies", Tree("Again"));

    Assert.Same(root, vm.Root);
    Assert.Same(title, vm.FindByName("title"));
    Assert.Equal("Again", title!.GetString(WidgetProp.Text));
  }

  [Fact]
  public void PatchesThatArriveOnABackgroundThreadAreAppliedOnTheUiThread()
  {
    var f = WithPanel();
    var vm = f.Panel();
    f.Game.SendTree("game.enemies", Tree());
    f.Scheduler.Deferred = true;

    f.Game.SendPatch("game.enemies", new PatchOp(2, PatchCode.Set, WidgetProp.Text, S("Later")));
    f.Game.SendPatch("game.enemies", new PatchOp(2, PatchCode.Set, WidgetProp.Text, S("Last")));

    Assert.Equal("Hello", vm.FindByName("title")!.GetString(WidgetProp.Text));
    Assert.Equal(1, f.Scheduler.Pending);
    f.Scheduler.Flush();
    Assert.Equal("Last", vm.FindByName("title")!.GetString(WidgetProp.Text));
  }

  [Fact]
  public async Task EventsGoToTheGameAfterPendingTypingAndNotFromDisabledWidgets()
  {
    var f = WithPanel();
    var vm = f.Panel();
    f.Game.SendTree("game.enemies", Tree());
    var order = new List<string>();
    vm.SetFlusher(9, () =>
    {
      order.Add("flush");
      return Task.CompletedTask;
    });

    var title = vm.FindByName("title")!;
    await title.Send(WidgetEvent.Click);
    order.Add("sent");
    f.Game.SendPatch("game.enemies", new PatchOp(2, PatchCode.Set, WidgetProp.Enabled, B(false)));
    Assert.False(await title.Send(WidgetEvent.Click));

    Assert.Equal(["flush", "sent"], order);
    var sent = Assert.Single(f.Game.Events);
    Assert.Equal(("game.enemies", 2, WidgetEvent.Click), (sent.Panel, sent.Widget, sent.Kind));
  }

  [Fact]
  public void SwitchingGamesShowsTheOtherGamesPanel()
  {
    var f = WithPanel();
    var vm = f.Panel();
    f.Game.SendTree("game.enemies", Tree("First"));
    var other = new FakeGameSession("Other");
    other.SetPanels(new PanelInfo(0, "game.enemies", "Enemies", PanelDock.Center, true));
    other.SendTree("game.enemies", Tree("Second"));

    f.Active.Current = other;
    Assert.Equal("Second", vm.FindByName("title")!.GetString(WidgetProp.Text));

    // The old game's changes no longer reach the panel.
    f.Game.SendPatch("game.enemies", new PatchOp(2, PatchCode.Set, WidgetProp.Text, S("Stale")));
    Assert.Equal("Second", vm.FindByName("title")!.GetString(WidgetProp.Text));
  }
}

public class PanelSyncTests
{
  [Fact]
  public void TheWindowSubscribesToTheFeedsItReadsOnlyForTheActiveGame()
  {
    var h = new Harness();
    _ = h.Main;
    var first = h.Connect("First");
    Assert.Contains("stats:ui", first.WantedFeeds);
    Assert.Contains("controls:ui", first.WantedFeeds);

    var second = h.Connect("Second");
    h.Main.SelectedSession = h.Main.Sessions.First(s => ReferenceEquals(s.Session, second));

    Assert.Empty(first.WantedFeeds);
    Assert.Contains("stats:ui", second.WantedFeeds);
    Assert.False(first.IsOpen("stats"));
    Assert.True(second.IsOpen("stats"));
  }

  [Fact]
  public void TheGameCanNotifyAndResetTheLayout()
  {
    var h = new Harness();
    _ = h.Main;
    var game = h.Connect();
    h.Layout.SetVisible("stats", false);

    game.Request(new AppRequest(AppRequestKind.Notify, string.Empty, Tone.Error, "Broken"));
    game.Request(
        new AppRequest(AppRequestKind.ResetLayout, string.Empty, Tone.Normal, string.Empty));
    game.Request(new AppRequest(AppRequestKind.ShowPanel, "nope", Tone.Normal, string.Empty));

    Assert.Equal((NotificationLevel.Error, "Broken"), Assert.Single(h.Notifier.Messages));
    Assert.True(h.Layout.IsVisible("stats"));
  }

  [Fact]
  public void APanelThatIsNotShownAtFirstIsNotBuiltByTheGame()
  {
    var h = new Harness(standardPanels: false);
    _ = h.Main;
    var game = new FakeGameSession();
    game.SetPanels(
        new PanelInfo(0, "a", "A", PanelDock.Center, true),
        new PanelInfo(1, "b", "B", PanelDock.Center, false));
    h.Host.Connect(game);

    Assert.True(game.IsOpen("a"));
    Assert.False(game.IsOpen("b"));
    h.Layout.SetVisible("b", true);
    Assert.True(game.IsOpen("b"));
    Assert.False(game.IsOpen("a"));
  }
}

public class TableRowTests
{
  [Fact]
  public void CellsCanShowOneThingAndSortByAnother()
  {
    var big = TableRow.Read(List(L(1), I(3), List(List(S("64 x 64"), I(4096)), S("b")), S("d")));
    var small = TableRow.Read(List(L(2), I(0), List(List(S("8 x 8"), I(64)), S("A")), S("")));

    Assert.Equal(["64 x 64", "b"], big.Cells);
    Assert.Equal(("d", 3), (big.Detail, big.Style));
    Assert.True(big.Matches("X 6"));
    Assert.False(big.Matches("zzz"));
    Assert.True(new TableRow.Comparer(0).Compare(small, big) < 0);
    Assert.True(new TableRow.Comparer(1).Compare(small, big) < 0);
    var same = List(L(1), I(3), List(List(S("64 x 64"), I(4096)), S("b")), S("d"));
    Assert.Equal(big, TableRow.Read(same));
  }
}
