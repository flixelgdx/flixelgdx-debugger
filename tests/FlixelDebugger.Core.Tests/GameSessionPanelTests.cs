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

using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.Core.Tests;

public class GameSessionPanelTests
{
  private static WidgetNode Node(
      WidgetType type, int id, string name, params (WidgetProp Key, UiValue Value)[] props) =>
      new(type, id, name, props.ToDictionary(p => p.Key, p => p.Value), []);

  private static WidgetNode Tree() =>
      Node(WidgetType.Stack, 1, string.Empty) with
      {
        Children =
        [
            Node(WidgetType.Label, 2, "title", (WidgetProp.Text, UiValue.FromString("a"))),
            Node(WidgetType.Table, 3, "rows", (WidgetProp.Limit, UiValue.FromInt(2))),
        ],
      };

  private static UiValue Row(long id) =>
      UiValue.FromList([
          UiValue.FromLong(id),
          UiValue.FromInt(0),
          UiValue.EmptyList,
          UiValue.FromString(string.Empty),
      ]);

  private static async Task<(TestServer Server, FakeGame Game, GameSession Session)> ConnectAsync(
      Action<DebugServerOptions>? configure = null)
  {
    var server = await TestServer.StartAsync(configure);
    var game = await FakeGame.ConnectAsync(server.Port);
    await game.HandshakeAsync();
    var session = await server.NextConnectedAsync();
    return (server, game, session);
  }

  // Reads the next message that is not a PING.
  private static async Task<ProtocolMessage> NextAsync(FakeGame game)
  {
    while (true)
    {
      var message = await game.ReceiveAsync();
      Assert.NotNull(message);
      if (message is not Ping)
      {
        return message;
      }
    }
  }

  [Fact(Timeout = 15000)]
  public async Task PanelsOpenPerOwnerAndFollowTreesPatchesAndNewLists()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      var trees = 0;
      session.PanelTreeReceived += (_, _) => Interlocked.Increment(ref trees);
      await game.SendAsync(new PanelList([
          new PanelInfo(0, "stats", "Stats", PanelDock.Left, true),
          new PanelInfo(1, "save", "Save", PanelDock.Center, true),
      ]));
      await Deadline.UntilAsync(() => session.Panels.Count == 2);
      Assert.True(session.TryGetPanel("save", out var save));
      Assert.Equal(("Save", PanelDock.Center), (save.Title, save.Dock));

      var token = Deadline.Token();
      Assert.True(await session.SetPanelOpenAsync("save", "ui", true, token));
      Assert.Equal(new PanelOpen(1, true), await NextAsync(game));

      // A second owner changes nothing on the wire.
      Assert.True(await session.SetPanelOpenAsync("save", "mcp", true, token));

      await game.SendAsync(new PanelTree(1, Tree()));
      await Deadline.UntilAsync(() => save.HasTree);
      Assert.Equal("a", save.Get(2, WidgetProp.Text)!.AsString());

      await game.SendAsync(new PanelPatch(1, [
          new PatchOp(2, PatchCode.Set, WidgetProp.Text, UiValue.FromString("b")),
          new PatchOp(3, PatchCode.Append, WidgetProp.Items, UiValue.FromList([Row(1), Row(2)])),
          new PatchOp(3, PatchCode.Append, WidgetProp.Items, UiValue.FromList([Row(3)])),
          new PatchOp(99, PatchCode.Set, WidgetProp.Text, UiValue.FromString("ignored")),
      ]));
      await Deadline.UntilAsync(() => save.Get(2, WidgetProp.Text)?.AsString() == "b");
      var rows = save.Get(3, WidgetProp.Items)!;
      Assert.Equal([2L, 3L], rows.Items.Select(r => r[0].AsLong()));
      var snapshot = save.Snapshot()!;
      Assert.Equal("b", snapshot.Children[0].Get(WidgetProp.Text)!.AsString());

      Assert.True(await session.SendPanelEventAsync(
          "save", 2, WidgetEvent.Click, [], token));
      var click = Assert.IsType<PanelEvent>(await NextAsync(game));
      Assert.Equal((1, 2, WidgetEvent.Click), (click.Panel, click.Widget, click.Event));

      // After a new list the game closed everything, so the session opens the panel again under
      // its new index and forgets the old tree.
      await game.SendAsync(new PanelList([
          new PanelInfo(0, "save", "Save file", PanelDock.Center, true),
          new PanelInfo(1, "stats", "Stats", PanelDock.Left, true),
      ]));
      Assert.Equal(new PanelOpen(0, true), await NextAsync(game));
      Assert.True(session.TryGetPanel("save", out var again));
      Assert.Same(save, again);
      Assert.Equal((0, "Save file", false), (again.Index, again.Title, again.HasTree));

      // The panel closes only when the last owner lets go.
      Assert.True(await session.SetPanelOpenAsync("save", "ui", false, token));
      Assert.True(await session.SetPanelOpenAsync("save", "mcp", false, token));
      Assert.Equal(new PanelOpen(0, false), await NextAsync(game));
      Assert.True(Volatile.Read(ref trees) >= 1);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task UnknownPanelsAreRefusedAndTheirMessagesIgnored()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      var token = Deadline.Token();
      Assert.False(await session.SetPanelOpenAsync("nope", "ui", true, token));
      Assert.False(await session.SendPanelEventAsync("nope", 1, WidgetEvent.Click, [], token));

      // A tree and a patch for an index the game never listed change nothing.
      await game.SendAsync(new PanelTree(5, Tree()));
      await game.SendAsync(new PanelPatch(5, []));
      await game.SendAsync(new PanelList([new PanelInfo(0, "a", "A", PanelDock.Right, false)]));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);
      Assert.False(session.Panels[0].HasTree);
      Assert.False(session.Panels[0].Shown);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task FeedSubscriptionsFollowTheirOwners()
  {
    var (server, game, session) = await ConnectAsync(o => o.SubscribeFeeds = false);
    await using (server)
    await using (game)
    {
      var token = Deadline.Token();

      // A wish made before the game listed its feeds is kept and sent with the list.
      Assert.True(await session.SetFeedWantedAsync("controls", "ui", true, token));
      await game.SendAsync(new FeedList([
          new FeedInfo(0, "stats", FeedKind.Stats),
          new FeedInfo(1, "controls", FeedKind.Controls),
      ]));
      Assert.Equal(new FeedSubscribe(1, true), await NextAsync(game));
      Assert.True(session.IsFeedWanted("controls"));
      Assert.False(session.IsFeedWanted("stats"));

      Assert.True(await session.SetFeedWantedAsync("stats", "ui", true, token));
      Assert.Equal(new FeedSubscribe(0, true), await NextAsync(game));
      Assert.True(await session.SetFeedWantedAsync("stats", "mcp", true, token));
      Assert.True(await session.SetFeedWantedAsync("stats", "ui", false, token));
      Assert.True(await session.SetFeedWantedAsync("stats", "mcp", false, token));
      Assert.Equal(new FeedSubscribe(0, false), await NextAsync(game));

      // Wanting every feed only subscribes to those nobody wanted yet.
      Assert.True(await session.SetFeedsWantedAsync(true, token));
      Assert.Equal(new FeedSubscribe(0, true), await NextAsync(game));
      Assert.True(await session.SetFeedsWantedAsync(false, token));
      Assert.Equal(new FeedSubscribe(0, false), await NextAsync(game));
      Assert.True(session.IsFeedWanted("controls"));
    }
  }

  [Fact(Timeout = 15000)]
  public async Task AppRequestsAreRaised()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      var seen = new TaskCompletionSource<AppRequest>();
      session.AppRequested += (_, e) => seen.TrySetResult(e.Request);
      await game.SendAsync(new AppRequest(AppRequestKind.Notify, string.Empty, Tone.Warning, "Hi"));
      var request = await seen.Task.WaitAsync(Deadline.Token());
      Assert.Equal(
          (AppRequestKind.Notify, Tone.Warning, "Hi"),
          (request.Request, request.Tone, request.Text));
    }
  }

  [Fact]
  public void AppendKeepsTheNewestEntriesWithinTheLimit()
  {
    var appended = UiPatch.Append(
        UiValue.FromList([UiValue.FromInt(1), UiValue.FromInt(2)]),
        UiValue.FromList([UiValue.FromInt(3), UiValue.FromInt(4)]),
        3);
    Assert.Equal([2, 3, 4], appended.Items.Select(v => v.AsInt()));
    Assert.Equal(
        UiPatch.DefaultGraphLimit,
        UiPatch.LimitOf(WidgetType.Graph, new Dictionary<WidgetProp, UiValue>()));
    Assert.Equal(0, UiPatch.LimitOf(WidgetType.Table, new Dictionary<WidgetProp, UiValue>()));
  }
}
