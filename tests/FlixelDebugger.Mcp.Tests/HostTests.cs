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

using System.Net;
using System.Text;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Tests;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Mcp.Tests;

public class HostTests
{
  private const string InitializeBody = """
      {"jsonrpc":"2.0","id":1,"method":"initialize","params":{
      "protocolVersion":"2025-03-26","capabilities":{},
      "clientInfo":{"name":"t","version":"1"}}}
      """;

  private static async Task<HttpStatusCode> PostAsync(
      McpHost host, Action<HttpRequestMessage>? configure = null)
  {
    using var http = new HttpClient();
    using var request = new HttpRequestMessage(HttpMethod.Post, host.Url)
    {
      Content = new StringContent(InitializeBody, Encoding.UTF8, "application/json"),
    };
    request.Headers.Accept.ParseAdd("application/json");
    request.Headers.Accept.ParseAdd("text/event-stream");
    configure?.Invoke(request);
    using var response = await http.SendAsync(request, Deadline.Token());
    return response.StatusCode;
  }

  [Fact(Timeout = 30000)]
  public async Task ARequestFromAWebPageOnAnotherSiteIsRefused()
  {
    await using var setup = await McpSetup.StartAsync();

    var evil = await PostAsync(setup.Host, r => r.Headers.Add("Origin", "https://evil.example"));
    var spoofedHost = await PostAsync(setup.Host, r => r.Headers.Host = "evil.example");
    var local = await PostAsync(setup.Host, r => r.Headers.Add("Origin", "http://localhost:3000"));
    var plain = await PostAsync(setup.Host);

    Assert.Equal(HttpStatusCode.Forbidden, evil);
    Assert.Equal(HttpStatusCode.Forbidden, spoofedHost);
    Assert.Equal(HttpStatusCode.OK, local);
    Assert.Equal(HttpStatusCode.OK, plain);
  }

  [Fact(Timeout = 30000)]
  public async Task HostListensOnLoopbackAndReportsItsAddress()
  {
    await using var server = await TestServer.StartAsync();
    await using var host = new McpHost(server.Server);
    Assert.False(host.IsRunning);
    Assert.Equal(string.Empty, host.Url);

    await host.StartAsync(0, Deadline.Token());

    Assert.True(host.IsRunning);
    Assert.True(host.Port > 0);
    Assert.Equal($"http://127.0.0.1:{host.Port}/mcp", host.Url);
    await host.StopAsync(TestContext.Current.CancellationToken);
    Assert.False(host.IsRunning);
    Assert.Equal(0, host.Port);
  }

  [Fact(Timeout = 30000)]
  public async Task HostRefusesToStartOnAUsedPort()
  {
    await using var server = await TestServer.StartAsync();
    await using var first = new McpHost(server.Server);
    await first.StartAsync(0, Deadline.Token());
    await using var second = new McpHost(server.Server);

    var error = await Assert.ThrowsAsync<PortInUseException>(
        () => second.StartAsync(first.Port, Deadline.Token()));

    Assert.Equal(first.Port, error.Port);
    Assert.False(second.IsRunning);
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => first.StartAsync(0, Deadline.Token()));
  }

  [Fact(Timeout = 30000)]
  public async Task HostCanBeRestarted()
  {
    await using var server = await TestServer.StartAsync();
    await using var host = new McpHost(server.Server);

    await host.StartAsync(0, Deadline.Token());
    await host.StopAsync(TestContext.Current.CancellationToken);
    await host.StartAsync(0, Deadline.Token());

    Assert.True(host.IsRunning);
  }

  [Fact(Timeout = 30000)]
  public async Task GamesSendFeedsOnlyWhileTheHostRuns()
  {
    await using var server = await TestServer.StartAsync(o => o.SubscribeFeeds = false);
    await using var host = new McpHost(server.Server);
    Assert.False(server.Server.FeedsWanted);

    await host.StartAsync(0, Deadline.Token());
    Assert.True(server.Server.FeedsWanted);
    await host.StopAsync(TestContext.Current.CancellationToken);

    Assert.False(server.Server.FeedsWanted);
  }

  [Fact(Timeout = 30000)]
  public async Task EveryToolCallRaisesActivityWithTheClientNameAndTheTouchedObject()
  {
    await using var setup = await McpSetup.StartAsync("my-agent");
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Inspector, new InspectorStatePayload(3, "state.0", [], false));
    await Deadline.UntilAsync(() => session.Inspector is not null);
    var events = new List<AgentActivityEventArgs>();
    setup.Host.Activity += (_, e) =>
    {
      lock (events)
      {
        events.Add(e);
      }
    };

    await setup.TextAsync("inspect", new() { ["target"] = "3" });
    await setup.TextAsync("click", new() { ["x"] = 4.0, ["y"] = 5.0 });
    await setup.TextAsync("list_games");

    AgentActivityEventArgs[] seen;
    lock (events)
    {
      seen = [.. events];
    }

    Assert.Equal(["inspect", "click", "list_games"], seen.Select(e => e.Tool));
    Assert.All(seen, e => Assert.Equal("my-agent", e.Client));
    Assert.Equal(session.Id, seen[0].SessionId);
    Assert.Equal(3, seen[0].ObjectId);
    Assert.Equal(4f, seen[1].ViewX);
    Assert.Equal(5f, seen[1].ViewY);
    Assert.Null(seen[2].ObjectId);
  }

  [Fact(Timeout = 30000)]
  public async Task APickReportsTheObjectItSelected()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    game.OnAction = async action =>
    {
      if (action.Action is ViewportPickAction)
      {
        await game.Data(ScriptedGame.Inspector, new InspectorStatePayload(9, "state.1", [], false));
        await game.Data(ScriptedGame.Viewport, new ViewportPayload(
            0, 1, 10f, 10f, 1f, 0f, 0f, false, false, false, 9, [], []));
      }
    };
    var events = new List<AgentActivityEventArgs>();
    setup.Host.Activity += (_, e) =>
    {
      lock (events)
      {
        events.Add(e);
      }
    };

    await setup.TextAsync("viewport_pick", new() { ["x"] = 1.0, ["y"] = 1.0 });

    lock (events)
    {
      Assert.Equal(2, events.Count);
      Assert.Null(events[0].ObjectId);
      Assert.Equal(9, events[1].ObjectId);
    }
  }
}
