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
using FlixelDebugger.Core.Tests;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Messages;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace FlixelDebugger.Mcp.Tests;

/// <summary>
/// A fake game that records every action it receives and can answer them like the real one.
/// </summary>
internal sealed class ScriptedGame : IAsyncDisposable
{
  public const ushort Stats = 0;
  public const ushort Logs = 1;
  public const ushort Watches = 2;
  public const ushort Console = 3;
  public const ushort Scene = 4;
  public const ushort Inspector = 5;
  public const ushort Save = 6;
  public const ushort Viewport = 7;
  public const ushort Controls = 8;
  public const ushort Capture = 9;
  public const ushort Input = 10;

  private readonly FakeGame _game;
  private readonly SemaphoreSlim _send = new(1, 1);
  private readonly List<FeedAction> _actions = [];
  private readonly List<ProtocolMessage> _panelMessages = [];
  private readonly Task _loop;

  private ScriptedGame(FakeGame game)
  {
    _game = game;
    _loop = Task.Run(RunAsync);
  }

  /// <summary>
  /// Called for every action the debugger sends; may answer with <see cref="SendAsync"/>.
  /// </summary>
  public Func<FeedAction, Task>? OnAction { get; set; }

  public static FeedList Feeds() => new(
  [
    new FeedInfo(Stats, "stats", FeedKind.Stats),
    new FeedInfo(Logs, "logs", FeedKind.Logs),
    new FeedInfo(Watches, "watches", FeedKind.Watches),
    new FeedInfo(Console, "console", FeedKind.Console),
    new FeedInfo(Scene, "scene", FeedKind.Scene),
    new FeedInfo(Inspector, "inspector", FeedKind.Inspector),
    new FeedInfo(Save, "save", FeedKind.Save),
    new FeedInfo(Viewport, "viewport", FeedKind.Viewport),
    new FeedInfo(Controls, "controls", FeedKind.Controls),
    new FeedInfo(Capture, "capture", FeedKind.Capture),
    new FeedInfo(Input, "input", FeedKind.Input),
  ]);

  public static async Task<ScriptedGame> ConnectAsync(
      TestServer server, string title = "Test Game", bool withPanels = true)
  {
    var fake = await FakeGame.ConnectAsync(server.Port);
    await fake.SendAsync(FakeGame.Hello(title: title));
    await fake.ReceiveAsync<HelloAck>();
    var game = new ScriptedGame(fake);
    if (withPanels)
    {
      await game.SendAsync(Feeds());
    }

    return game;
  }

  /// <summary>Gets every PANEL_OPEN and PANEL_EVENT the debugger sent, in order.</summary>
  public IReadOnlyList<ProtocolMessage> PanelMessages
  {
    get
    {
      lock (_panelMessages)
      {
        return [.. _panelMessages];
      }
    }
  }

  /// <summary>
  /// Called for every panel event the debugger sends; may answer with <see cref="SendAsync"/>.
  /// </summary>
  public Func<PanelEvent, Task>? OnPanelEvent { get; set; }

  public IReadOnlyList<FeedAction> Actions
  {
    get
    {
      lock (_actions)
      {
        return [.. _actions];
      }
    }
  }

  public async Task SendAsync(ProtocolMessage message)
  {
    await _send.WaitAsync(Deadline.Token());
    try
    {
      await _game.SendAsync(message);
    }
    finally
    {
      _send.Release();
    }
  }

  public Task Data(ushort panel, FeedPayload payload) => SendAsync(new FeedData(panel, payload));

  public async Task<FeedAction> NextActionAsync(Func<FeedAction, bool> match)
  {
    FeedAction? found = null;
    await Deadline.UntilAsync(() =>
    {
      lock (_actions)
      {
        found = _actions.FirstOrDefault(match);
      }

      return found is not null;
    });
    return found!;
  }

  public async ValueTask DisposeAsync()
  {
    _game.Abort();
    try
    {
      await _loop;
    }
    catch (Exception)
    {
      // The loop ends with an error when the socket is aborted, which is what happens here.
    }

    await _game.DisposeAsync();
  }

  private async Task RunAsync()
  {
    while (true)
    {
      var message = await _game.ReceiveAsync();
      switch (message)
      {
        case null:
          return;
        case Ping ping:
          await SendAsync(new Pong(ping.Nonce));
          break;
        case FeedAction action:
          lock (_actions)
          {
            _actions.Add(action);
          }

          if (OnAction is { } handler)
          {
            await handler(action);
          }

          break;
        case PanelOpen or PanelEvent:
          lock (_panelMessages)
          {
            _panelMessages.Add(message);
          }

          if (message is PanelEvent e && OnPanelEvent is { } onEvent)
          {
            await onEvent(e);
          }

          break;
      }
    }
  }
}

/// <summary>
/// A debug server, an MCP host on a free port, and an MCP client connected to it.
/// </summary>
internal sealed class McpSetup : IAsyncDisposable
{
  private McpSetup(TestServer server, McpHost host, McpClient client)
  {
    Server = server;
    Host = host;
    Client = client;
  }

  public TestServer Server { get; }

  public McpHost Host { get; }

  public McpClient Client { get; }

  public static async Task<McpSetup> StartAsync(string clientName = "test-agent")
  {
    var server = await TestServer.StartAsync();
    var host = new McpHost(server.Server) { CaptureTimeout = TimeSpan.FromMilliseconds(400) };
    await host.StartAsync(0, Deadline.Token());
    var client = await ConnectClientAsync(host, clientName);
    return new McpSetup(server, host, client);
  }

  public static Task<McpClient> ConnectClientAsync(McpHost host, string name) =>
      McpClient.CreateAsync(
          new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(host.Url) }),
          new McpClientOptions { ClientInfo = new Implementation { Name = name, Version = "1.0" } },
          cancellationToken: Deadline.Token());

  /// <summary>Calls a tool and returns the text of its first content block.</summary>
  public async Task<string> TextAsync(string tool, Dictionary<string, object?>? args = null)
  {
    var result = await Client.CallToolAsync(tool, args, cancellationToken: Deadline.Token());
    Assert.NotEqual(true, result.IsError);
    return Assert.IsType<TextContentBlock>(result.Content[0]).Text;
  }

  /// <summary>Calls a tool that must fail and returns its error text.</summary>
  public async Task<string> ErrorAsync(string tool, Dictionary<string, object?>? args = null)
  {
    var result = await Client.CallToolAsync(tool, args, cancellationToken: Deadline.Token());
    Assert.True(result.IsError);
    return Assert.IsType<TextContentBlock>(result.Content[0]).Text;
  }

  public async ValueTask DisposeAsync()
  {
    await Client.DisposeAsync();
    await Host.DisposeAsync();
    await Server.DisposeAsync();
  }
}
