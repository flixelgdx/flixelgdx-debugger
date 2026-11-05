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

using System.Net.WebSockets;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Tests;

public class GameSessionTests
{
  private static PanelList ListOf(params (ushort Index, string Id, PanelKind Kind)[] panels) =>
      new([.. panels.Select(p => new PanelInfo(p.Index, p.Id, p.Id.ToUpperInvariant(), p.Kind))]);

  private static async Task<(TestServer Server, FakeGame Game, GameSession Session)> ConnectAsync(Action<DebugServerOptions>? configure = null)
  {
    var server = await TestServer.StartAsync(configure);
    var game = await FakeGame.ConnectAsync(server.Port);
    await game.HandshakeAsync();
    var session = await server.NextConnectedAsync();
    return (server, game, session);
  }

  [Fact(Timeout = 15000)]
  public async Task HelloIsAnsweredWithHelloAckAndStored()
  {
    await using var server = await TestServer.StartAsync();
    await using var game = await FakeGame.ConnectAsync(server.Port);

    await game.SendAsync(FakeGame.Hello(title: "My Game"));
    var ack = await game.ReceiveAsync<HelloAck>();
    var session = await server.NextConnectedAsync();

    Assert.Equal(ProtocolConstants.VersionMajor, ack.ProtocolMajor);
    Assert.Equal(ProtocolConstants.VersionMinor, ack.ProtocolMinor);
    Assert.Equal(SessionState.Live, session.State);
    Assert.NotNull(session.Hello);
    Assert.Equal("desktop", session.Hello.Platform);
    Assert.Equal("Test OS", session.Hello.Device);
    Assert.Equal(1280, session.Hello.ScreenWidth);
    Assert.Equal(720, session.Hello.ScreenHeight);
    Assert.Equal("1.2.3", session.Hello.FrameworkVersion);
    Assert.Equal("My Game (desktop)", session.DisplayName);
    Assert.Same(session, Assert.Single(server.Server.Sessions));
  }

  [Fact(Timeout = 15000)]
  public async Task ADifferentMajorVersionIsAnsweredThenClosedWithAReason()
  {
    await using var server = await TestServer.StartAsync();
    await using var game = await FakeGame.ConnectAsync(server.Port);

    await game.SendAsync(FakeGame.Hello(major: 2));
    var ack = await game.ReceiveAsync<HelloAck>();
    var closed = await game.ReceiveBytesAsync();
    var failure = await server.NextFailureAsync();

    Assert.Equal(ProtocolConstants.VersionMajor, ack.ProtocolMajor);
    Assert.Null(closed);
    Assert.Equal(WebSocketCloseStatus.PolicyViolation, game.CloseStatus);
    Assert.Contains("protocol version 2.0", game.CloseDescription, StringComparison.Ordinal);
    Assert.Contains("1.x", game.CloseDescription, StringComparison.Ordinal);
    Assert.Contains("protocol version 2.0", failure, StringComparison.Ordinal);
    Assert.Equal(0, server.ConnectedCount());
    Assert.Empty(server.Server.Sessions);
  }

  [Fact(Timeout = 15000)]
  public async Task AMinorVersionDifferenceIsAccepted()
  {
    await using var server = await TestServer.StartAsync();
    await using var game = await FakeGame.ConnectAsync(server.Port);

    await game.SendAsync(new Hello(ProtocolConstants.VersionMajor, 9, "android", "Pixel", 1080, 2400, "T", "9.9"));
    await game.ReceiveAsync<HelloAck>();

    Assert.Equal(SessionState.Live, (await server.NextConnectedAsync()).State);
  }

  [Fact(Timeout = 15000)]
  public async Task TheFirstMessageMustBeHello()
  {
    await using var server = await TestServer.StartAsync();
    await using var game = await FakeGame.ConnectAsync(server.Port);

    await game.SendAsync(new Pong(1));
    var closed = await game.ReceiveBytesAsync();
    var failure = await server.NextFailureAsync();

    Assert.Null(closed);
    Assert.Equal(WebSocketCloseStatus.ProtocolError, game.CloseStatus);
    Assert.Contains("HELLO", failure, StringComparison.Ordinal);
    Assert.Equal(0, server.ConnectedCount());
  }

  [Fact(Timeout = 15000)]
  public async Task AGameThatNeverSaysHelloIsDroppedAfterTheTimeout()
  {
    await using var server = await TestServer.StartAsync(o => o.HandshakeTimeout = TimeSpan.FromMilliseconds(150));
    await using var game = await FakeGame.ConnectAsync(server.Port);

    var failure = await server.NextFailureAsync();

    Assert.Contains("HELLO", failure, StringComparison.Ordinal);
    Assert.Equal(0, server.ConnectedCount());
  }

  [Fact(Timeout = 15000)]
  public async Task PanelListThenPanelDataUpdatesTheSession()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      var updated = new List<(string Id, PanelPayload Payload)>();
      var listChanges = 0;
      session.PanelUpdated += (_, e) =>
      {
        lock (updated)
        {
          updated.Add((e.Panel.Id, e.Payload));
        }
      };
      session.PanelListChanged += (_, _) => Interlocked.Increment(ref listChanges);

      await game.SendAsync(ListOf((0, "stats", PanelKind.Stats), (1, "logs", PanelKind.Logs), (2, "custom.player", PanelKind.Custom)));
      await Deadline.UntilAsync(() => session.Panels.Count == 3);
      await game.SendAsync(new PanelData(0, TestData.Stats(60)));
      await game.SendAsync(new PanelData(1, TestData.Logs(true, TestData.Log(1), TestData.Log(2))));
      await game.SendAsync(new PanelData(2, new CustomPayload([new PropertyRow("hp", PropertyValue.FromInt(10), true)], [new CustomButton(4, "Heal")])));
      await Deadline.UntilAsync(() => session.Stats.Count == 1 && session.Logs.Count == 2 && session.Panels[2].Custom is not null);

      Assert.Equal(1, Volatile.Read(ref listChanges));
      Assert.Equal(["stats", "logs", "custom.player"], session.Panels.Select(p => p.Id));
      Assert.Equal(60, session.Stats.Latest(StatsField.Fps));
      Assert.True(session.TryGetPanel("stats", out var stats));
      Assert.IsType<StatsPayload>(stats.Payload);
      Assert.Equal(1, stats.Version);
      Assert.Equal(PanelKind.Stats, stats.Kind);
      Assert.Equal("STATS", stats.Name);
      Assert.True(session.TryGetPanel("custom.player", out var custom));
      Assert.Equal("hp", Assert.Single(custom.Custom!.Rows).Name);
      Assert.Equal(4, Assert.Single(custom.Custom.Buttons).ActionIndex);
      lock (updated)
      {
        Assert.Equal(["stats", "logs", "custom.player"], updated.Select(u => u.Id));
      }
    }
  }

  [Fact(Timeout = 15000)]
  public async Task PanelsAreMatchedByIdWhenTheListChanges()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf((0, "stats", PanelKind.Stats), (1, "logs", PanelKind.Logs), (2, "custom.player", PanelKind.Custom)));
      await Deadline.UntilAsync(() => session.Panels.Count == 3);
      await game.SendAsync(new PanelData(0, TestData.Stats(60)));
      await Deadline.UntilAsync(() => session.Stats.Count == 1);
      session.TryGetPanel("stats", out var statsBefore);
      session.TryGetPanel("custom.player", out var customBefore);

      // The indices are swapped and the custom panel is gone.
      await game.SendAsync(ListOf((0, "logs", PanelKind.Logs), (1, "stats", PanelKind.Stats)));
      await Deadline.UntilAsync(() => session.Panels.Count == 2);

      Assert.True(session.TryGetPanel("stats", out var statsAfter));
      Assert.Same(statsBefore, statsAfter);
      Assert.Equal(1, statsAfter.Index);
      Assert.IsType<StatsPayload>(statsAfter.Payload);
      Assert.False(session.TryGetPanel("custom.player", out _));
      Assert.Equal(["logs", "stats"], session.Panels.Select(p => p.Id));

      // New data goes to the right panel under its new index.
      await game.SendAsync(new PanelData(1, TestData.Stats(30)));
      await game.SendAsync(new PanelData(0, TestData.Logs(false, TestData.Log(7))));
      await Deadline.UntilAsync(() => session.Stats.Count == 2 && session.Logs.Count == 1);
      Assert.Equal(30, session.Stats.Latest(StatsField.Fps));

      // A panel that comes back is a new panel and starts empty.
      await game.SendAsync(ListOf((0, "logs", PanelKind.Logs), (1, "stats", PanelKind.Stats), (2, "custom.player", PanelKind.Custom)));
      await Deadline.UntilAsync(() => session.Panels.Count == 3);
      Assert.True(session.TryGetPanel("custom.player", out var customAfter));
      Assert.NotSame(customBefore, customAfter);
      Assert.Null(customAfter.Payload);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task EveryBuiltInPayloadKindIsStored()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf(
          (0, "watches", PanelKind.Watches),
          (1, "trackers", PanelKind.Trackers),
          (2, "controls", PanelKind.Controls),
          (3, "console", PanelKind.Console),
          (4, "scene", PanelKind.Scene),
          (5, "inspector", PanelKind.Inspector),
          (6, "textures", PanelKind.Textures),
          (7, "save", PanelKind.Save)));
      await Deadline.UntilAsync(() => session.Panels.Count == 8);

      await game.SendAsync(new PanelData(0, new WatchesPayload([new NamedValue("fps", PropertyValue.FromInt(60))])));
      await game.SendAsync(new PanelData(1, new TrackersPayload([new TrackerBlock("Player", [new NamedValue("x", PropertyValue.FromFloat(1.5f))])])));
      await game.SendAsync(new PanelData(2, new ControlsPayload(true, false, true, 20, LogLevel.Default, [new Keybind("F3", "Pause")])));
      await game.SendAsync(new PanelData(3, new ConsoleCommandsPayload([new ConsoleCommand("goto", "goto <x> <y>", "Centers the camera.")])));
      await game.SendAsync(new PanelData(3, new ConsoleResultPayload(ConsoleStatus.Ran, "done")));
      await game.SendAsync(new PanelData(4, new ScenePayload([new SceneNode(1, -1, "State", "state", false), new SceneNode(2, 1, "Sprite", "0", false)])));
      await game.SendAsync(new PanelData(5, new InspectorStatePayload(2, "state.0", [new InspectorSection("Sprite", "Sprite", [new PropertyRow("x", PropertyValue.FromFloat(3), true)])], true)));
      await game.SendAsync(new PanelData(5, new InspectorThumbnailPayload(2, 4, 4, new byte[] { 1, 2, 3 })));
      await game.SendAsync(new PanelData(6, new TexturesListPayload(
          [new TextureEntry(1, "a.png", 8, 8, "RGBA8", 256, 1, false), new TextureEntry(2, "b.png", 8, 8, "RGBA8", 256, 1, false)], 2, 512, 0)));
      await game.SendAsync(new PanelData(6, new TexturesThumbnailPayload(1, 2, 2, new byte[] { 9 })));
      await game.SendAsync(new PanelData(6, new TexturesThumbnailPayload(2, 2, 2, new byte[] { 8 })));
      await game.SendAsync(new PanelData(7, new SavePayload([new SaveRow("coins", "5", ValueKind.Int, false)], ["old"], "ok", false)));
      await Deadline.UntilAsync(() => session.Save is not null && session.TextureThumbnails.Count == 2);

      Assert.Equal("fps", Assert.Single(session.Watches!.Rows).Name);
      Assert.Equal("Player", Assert.Single(session.Trackers!.Blocks).Name);
      Assert.True(session.Controls!.Paused);
      Assert.Equal("goto", Assert.Single(session.ConsoleCommands!.Commands).Name);
      Assert.Equal("done", session.ConsoleResult!.Message);
      Assert.Equal(2, session.Scene.Nodes.Count);
      Assert.Equal(2, Assert.Single(session.Scene.GetChildren(1)).Id);
      Assert.Equal("state.0", session.Inspector!.Path);
      Assert.True(session.InspectorThumbnails.TryGet(2, out var inspectorThumb));
      Assert.Equal([1, 2, 3], inspectorThumb.Png.ToArray());
      Assert.Equal(2, session.Textures!.TextureCount);
      Assert.True(session.TextureThumbnails.TryGet(1, out var textureThumb));
      Assert.Equal([9], textureThumb.Png.ToArray());
      Assert.Equal("coins", Assert.Single(session.Save!.Rows).Path);

      // A newer list without texture 2 drops its thumbnail.
      await game.SendAsync(new PanelData(6, new TexturesListPayload([new TextureEntry(1, "a.png", 8, 8, "RGBA8", 256, 1, false)], 1, 256, 0)));
      await Deadline.UntilAsync(() => session.TextureThumbnails.Count == 1);
      Assert.False(session.TextureThumbnails.TryGet(2, out _));
      Assert.True(session.TryGetPanel("console", out var console));
      Assert.IsType<ConsoleResultPayload>(console.Payload);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task DataForAnUnknownPanelIsIgnoredAndMalformedDataIsCounted()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf((0, "stats", PanelKind.Stats)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);

      await game.SendAsync(new PanelData(99, TestData.Stats(1)));

      // A stats payload cut short cannot be decoded.
      var broken = new PanelData(0, TestData.Stats(1)).ToArray()[..10];
      await game.SendRawAsync(broken);
      await game.SendAsync(new PanelData(0, TestData.Stats(42)));
      await Deadline.UntilAsync(() => session.Stats.Count == 1);

      Assert.Equal(42, session.Stats.Latest(StatsField.Fps));
      Assert.Equal(1, session.ProtocolErrors);
      Assert.Equal(SessionState.Live, session.State);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task TextMessagesAreIgnored()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendTextAsync("hello, this is text");
      await game.SendAsync(ListOf((0, "stats", PanelKind.Stats)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);

      Assert.Equal(0, session.ProtocolErrors);
      Assert.Equal(SessionState.Live, session.State);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ActionsBeforeThePanelListArePolitelyRefused()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      Assert.False(await session.SetPausedAsync(true, Deadline.Token()));
      Assert.False(await session.RunCommandAsync("goto 1 2", Deadline.Token()));
      Assert.False(await session.CustomPressAsync("custom.player", 1, Deadline.Token()));
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ActionsArriveAtTheGameWithTheCorrectBytes()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf(
          (3, "controls", PanelKind.Controls),
          (7, "console", PanelKind.Console),
          (2, "custom.player", PanelKind.Custom),
          (9, "inspector", PanelKind.Inspector),
          (4, "textures", PanelKind.Textures),
          (5, "save", PanelKind.Save),
          (6, "scene", PanelKind.Scene),
          (8, "logs", PanelKind.Logs)));
      await Deadline.UntilAsync(() => session.Panels.Count == 8);
      var token = Deadline.Token();

      Assert.True(await session.SetPausedAsync(true, token));
      Assert.Equal(new byte[] { 5, 3, 0, 1, 1 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.StepAsync(token));
      Assert.Equal(new byte[] { 5, 3, 0, 2 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.SetRateAsync(30, token));
      Assert.Equal(new byte[] { 5, 3, 0, 5, 30 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.SetLogLevelAsync(LogLevel.Default, token));
      Assert.Equal(new byte[] { 5, 3, 0, 6, 255 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.RunCommandAsync("go 1", token));
      Assert.Equal(new byte[] { 5, 7, 0, 1, 4, 0, (byte)'g', (byte)'o', (byte)' ', (byte)'1' }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.CustomPressAsync("custom.player", 258, token));
      Assert.Equal(new byte[] { 5, 2, 0, 1, 2, 1 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.CustomSetPropertyAsync("custom.player", 1, PropertyValue.FromBool(true), token));
      Assert.Equal(new byte[] { 5, 2, 0, 2, 1, 0, 1, 1 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.SelectAsync(258, token));
      Assert.Equal(new byte[] { 5, 9, 0, 1, 2, 1, 0, 0 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.SetPropertyAsync(7, 1, 2, PropertyValue.FromInt(5), token));
      Assert.Equal(new byte[] { 5, 9, 0, 6, 7, 0, 0, 0, 1, 0, 2, 0, 2, 5, 0, 0, 0 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.RequestThumbnailAsync(7, token));
      Assert.Equal(new byte[] { 5, 4, 0, 1, 7, 0, 0, 0 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.SaveSetAsync("a", "b", token));
      Assert.Equal(new byte[] { 5, 5, 0, 1, 1, 0, (byte)'a', 1, 0, (byte)'b' }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.SaveResetAllAsync(token));
      Assert.Equal(new byte[] { 5, 5, 0, 4 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.RequestSceneTreeAsync(token));
      Assert.Equal(new byte[] { 5, 6, 0, 1 }, await game.ReceiveBytesSkippingPingsAsync());

      Assert.True(await session.ClearLogsAsync(token));
      Assert.Equal(new byte[] { 5, 8, 0, 1 }, await game.ReceiveBytesSkippingPingsAsync());
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ActionsFollowAPanelWhenItsIndexChanges()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf((3, "controls", PanelKind.Controls)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1 && session.Panels[0].Index == 3);
      await game.SendAsync(ListOf((11, "controls", PanelKind.Controls)));
      await Deadline.UntilAsync(() => session.Panels[0].Index == 11);

      await session.StepAsync(Deadline.Token());

      Assert.Equal(new byte[] { 5, 11, 0, 2 }, await game.ReceiveBytesSkippingPingsAsync());
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ASendFromTheSessionIsDecodedAsTheRightAction()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf((1, "console", PanelKind.Console)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);

      await session.RunCommandAsync("freecam on", Deadline.Token());

      var bytes = await game.ReceiveBytesSkippingPingsAsync();
      var action = Assert.IsType<PanelAction>(game.Decode(bytes));
      Assert.Equal(1, action.Panel);
      Assert.Equal("freecam on", Assert.IsType<ConsoleRunAction>(action.Action).Line);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ARunLineOverFiveHundredCharactersIsRejected()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await Assert.ThrowsAsync<ArgumentException>(() => session.RunCommandAsync(new string('x', 501), Deadline.Token()));
    }
  }

  [Fact(Timeout = 20000)]
  public async Task ConcurrentSendsAreSerializedAndNeverInterleave()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(ListOf((3, "controls", PanelKind.Controls)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);

      var sends = Enumerable.Range(1, 40).Select(i => Task.Run(() => session.SetRateAsync((byte)i, Deadline.Token()), Deadline.Token())).ToArray();
      var received = new List<byte>();
      while (received.Count < 40)
      {
        var bytes = await game.ReceiveBytesSkippingPingsAsync();
        Assert.Equal(5, bytes.Length);
        Assert.Equal([5, 3, 0, 5], bytes[..4]);
        received.Add(bytes[4]);
      }

      Assert.All(await Task.WhenAll(sends), Assert.True);
      Assert.Equal(Enumerable.Range(1, 40).Select(i => (byte)i), received.Order());
    }
  }

  [Fact(Timeout = 15000)]
  public async Task PingsAreAnsweredWithPongsAndTheRoundTripIsMeasured()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      Assert.Null(session.PingMs);
      var pingUpdates = 0;
      session.PingUpdated += (_, _) => Interlocked.Increment(ref pingUpdates);

      var ping = await game.ReceiveAsync<Ping>();
      await game.SendAsync(new Pong(ping.Nonce));
      await Deadline.UntilAsync(() => session.PingMs is not null && Volatile.Read(ref pingUpdates) >= 1);

      Assert.True(session.PingMs >= 0);
      Assert.Equal(1, session.Pings.Count);
      Assert.Equal(session.PingMs!.Value, session.Pings.Latest);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task APongWithAnUnknownNonceIsIgnored()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await game.SendAsync(new Pong(123456789));
      await game.SendAsync(ListOf((0, "stats", PanelKind.Stats)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);

      Assert.Null(session.PingMs);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task EachPingCarriesADifferentNonce()
  {
    var (server, game, _) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      var first = await game.ReceiveAsync<Ping>();
      var second = await game.ReceiveAsync<Ping>();
      Assert.NotEqual(first.Nonce, second.Nonce);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task AGameClosingTheConnectionRaisesTheDisconnectEvent()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      var stateChanges = new List<SessionState>();
      session.StateChanged += (_, _) =>
      {
        lock (stateChanges)
        {
          stateChanges.Add(session.State);
        }
      };

      await game.CloseAsync();
      var gone = await server.NextDisconnectedAsync();

      Assert.Same(session, gone);
      Assert.Equal(SessionState.Closed, session.State);
      Assert.NotNull(session.CloseReason);
      Assert.Empty(server.Server.Sessions);
      lock (stateChanges)
      {
        Assert.Equal([SessionState.Closed], stateChanges);
      }

      Assert.False(await session.SetPausedAsync(true, Deadline.Token()));
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ALostConnectionRaisesTheDisconnectEventToo()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      game.Abort();

      var gone = await server.NextDisconnectedAsync();

      Assert.Same(session, gone);
      Assert.Equal(SessionState.Closed, session.State);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task SeveralGamesCanBeConnectedAtOnceAndStayIndependent()
  {
    await using var server = await TestServer.StartAsync();
    await using var first = await FakeGame.ConnectAsync(server.Port);
    await first.SendAsync(FakeGame.Hello(title: "First"));
    await first.ReceiveAsync<HelloAck>();
    await using var second = await FakeGame.ConnectAsync(server.Port);
    await second.SendAsync(FakeGame.Hello(title: "Second"));
    await second.ReceiveAsync<HelloAck>();
    var sessionOne = await server.NextConnectedAsync();
    var sessionTwo = await server.NextConnectedAsync(2);

    await first.SendAsync(ListOf((0, "stats", PanelKind.Stats)));
    await first.SendAsync(new PanelData(0, TestData.Stats(11)));
    await Deadline.UntilAsync(() => sessionOne.Stats.Count == 1);

    Assert.Equal(2, server.Server.Sessions.Count);
    Assert.NotEqual(sessionOne.Id, sessionTwo.Id);
    Assert.Equal(["First (desktop)", "Second (desktop)"], new[] { sessionOne.DisplayName, sessionTwo.DisplayName }.Order());
    Assert.Empty(sessionTwo.Panels);
    Assert.Equal(0, sessionTwo.Stats.Count);

    await first.CloseAsync();
    await server.NextDisconnectedAsync();
    Assert.Same(sessionTwo, Assert.Single(server.Server.Sessions));
  }

  [Fact(Timeout = 20000)]
  public async Task AMessageOverSixteenMegabytesClosesTheConnection()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      try
      {
        await game.SendRawAsync(new byte[GameSession.MaxMessageBytes + 1]);
      }
      catch (WebSocketException)
      {
        // The server may hang up while the rest of the message is still being sent.
      }

      var gone = await server.NextDisconnectedAsync();

      Assert.Same(session, gone);
      Assert.Contains("16 MB", session.CloseReason, StringComparison.Ordinal);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ALargeMessageIsReadInFull()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      // An unknown message type is ignored, but it must be read in full without closing the connection.
      var big = new byte[1024 * 1024];
      big[0] = 200;
      await game.SendRawAsync(big);
      await game.SendAsync(ListOf((0, "stats", PanelKind.Stats)));
      await Deadline.UntilAsync(() => session.Panels.Count == 1);

      Assert.Equal(SessionState.Live, session.State);
    }
  }

  [Fact(Timeout = 15000)]
  public async Task ClosingFromTheSessionSendsTheReasonToTheGame()
  {
    var (server, game, session) = await ConnectAsync();
    await using (server)
    await using (game)
    {
      await session.CloseAsync("Disconnected by the person.");
      while (await game.ReceiveBytesAsync() is not null)
      {
        // Skip pings until the close frame arrives.
      }

      var gone = await server.NextDisconnectedAsync();

      Assert.Same(session, gone);
      Assert.Equal("Disconnected by the person.", game.CloseDescription);
      Assert.Equal("Disconnected by the person.", session.CloseReason);
    }
  }
}
