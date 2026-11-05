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
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Messages;

namespace FlixelDebugger.Core.Tests;

/// <summary>
/// Gives every awaited step of an end-to-end test a deadline, so a bug fails the test instead of
/// hanging it.
/// </summary>
internal static class Deadline
{
  public static readonly TimeSpan Default = TimeSpan.FromSeconds(5);

  public static CancellationToken Token() =>
      CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, new CancellationTokenSource(Default).Token).Token;

  public static async Task UntilAsync(Func<bool> condition)
  {
    var until = DateTime.UtcNow + Default;
    while (!condition())
    {
      Assert.True(DateTime.UtcNow < until, "The condition was not met in time.");
      await Task.Delay(5, TestContext.Current.CancellationToken);
    }
  }
}

/// <summary>
/// A debug server on a free port with fast pings, plus tasks that complete when sessions connect.
/// </summary>
internal sealed class TestServer : IAsyncDisposable
{
  private readonly List<GameSession> _connected = [];
  private readonly List<GameSession> _disconnected = [];
  private readonly List<string> _failed = [];

  private TestServer(DebugServer server)
  {
    Server = server;
    server.SessionConnected += (_, e) =>
    {
      lock (_connected)
      {
        _connected.Add(e.Session);
      }
    };
    server.SessionDisconnected += (_, e) =>
    {
      lock (_disconnected)
      {
        _disconnected.Add(e.Session);
      }
    };
    server.HandshakeFailed += (_, e) =>
    {
      lock (_failed)
      {
        _failed.Add(e.Reason);
      }
    };
  }

  public DebugServer Server { get; }

  public int Port => Server.Port;

  public static async Task<TestServer> StartAsync(Action<DebugServerOptions>? configure = null)
  {
    var options = new DebugServerOptions { Port = 0, PingInterval = TimeSpan.FromMilliseconds(50) };
    configure?.Invoke(options);
    var server = new TestServer(new DebugServer(options));
    await server.Server.StartAsync(Deadline.Token());
    return server;
  }

  public async Task<GameSession> NextConnectedAsync(int count = 1)
  {
    await Deadline.UntilAsync(() => { lock (_connected) { return _connected.Count >= count; } });
    lock (_connected)
    {
      return _connected[count - 1];
    }
  }

  public async Task<GameSession> NextDisconnectedAsync()
  {
    await Deadline.UntilAsync(() => { lock (_disconnected) { return _disconnected.Count >= 1; } });
    lock (_disconnected)
    {
      return _disconnected[0];
    }
  }

  public async Task<string> NextFailureAsync()
  {
    await Deadline.UntilAsync(() => { lock (_failed) { return _failed.Count >= 1; } });
    lock (_failed)
    {
      return _failed[0];
    }
  }

  public int ConnectedCount()
  {
    lock (_connected)
    {
      return _connected.Count;
    }
  }

  public ValueTask DisposeAsync() => Server.DisposeAsync();
}

/// <summary>
/// A stand-in for a game: a WebSocket client that speaks the protocol with the Protocol library's
/// writer.
/// </summary>
internal sealed class FakeGame : IAsyncDisposable
{
  private readonly ClientWebSocket _socket = new();
  private readonly MessageWriter _writer = new();
  private readonly MessageDecoder _decoder = new();

  public WebSocketState State => _socket.State;

  public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

  public string? CloseDescription => _socket.CloseStatusDescription;

  public static async Task<FakeGame> ConnectAsync(int port)
  {
    var game = new FakeGame();
    await game._socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), Deadline.Token());
    return game;
  }

  public static Hello Hello(ushort major = ProtocolConstants.VersionMajor, string title = "Test Game") =>
      new(major, 0, "desktop", "Test OS", 1280, 720, title, "1.2.3");

  public async Task HandshakeAsync()
  {
    await SendAsync(Hello());
    Assert.IsType<HelloAck>(await ReceiveAsync());
  }

  public async Task SendAsync(ProtocolMessage message)
  {
    message.Write(_writer);
    await _socket.SendAsync(_writer.Finish(), WebSocketMessageType.Binary, true, Deadline.Token());
    if (message is PanelList list)
    {
      _decoder.SetPanels(list.Panels);
    }
  }

  public Task SendRawAsync(byte[] bytes) =>
      _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, Deadline.Token());

  public ProtocolMessage Decode(byte[] bytes) => _decoder.Decode(bytes);

  public Task SendTextAsync(string text) =>
      _socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, Deadline.Token());

  // Returns the raw bytes of the next binary message, or null when the server closed the
  // connection.
  public async Task<byte[]?> ReceiveBytesAsync()
  {
    var buffer = new byte[64 * 1024];
    var count = 0;
    var token = Deadline.Token();
    while (true)
    {
      var result = await _socket.ReceiveAsync(buffer.AsMemory(count), token);
      if (result.MessageType == WebSocketMessageType.Close)
      {
        if (_socket.State == WebSocketState.CloseReceived)
        {
          await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, token);
        }

        return null;
      }

      count += result.Count;
      if (result.EndOfMessage)
      {
        return buffer[..count];
      }
    }
  }

  public async Task<ProtocolMessage?> ReceiveAsync()
  {
    var bytes = await ReceiveBytesAsync();
    return bytes is null ? null : _decoder.Decode(bytes);
  }

  public async Task<T> ReceiveAsync<T>()
      where T : ProtocolMessage
  {
    // Pings arrive in the background on a fast timer, so skip them when the test wants something
    // else.
    while (true)
    {
      var message = await ReceiveAsync();
      if (message is T wanted)
      {
        return wanted;
      }

      Assert.True(message is Ping, $"Expected {typeof(T).Name} but received {message?.GetType().Name ?? "a close frame"}.");
    }
  }

  // Reads raw messages until one does not start with the PING type byte.
  public async Task<byte[]> ReceiveBytesSkippingPingsAsync()
  {
    while (true)
    {
      var bytes = await ReceiveBytesAsync();
      Assert.NotNull(bytes);
      if (bytes[0] != (byte)MessageType.Ping)
      {
        return bytes;
      }
    }
  }

  public async Task CloseAsync()
  {
    if (_socket.State == WebSocketState.Open)
    {
      await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", Deadline.Token());
    }
  }

  public void Abort() => _socket.Abort();

  public ValueTask DisposeAsync()
  {
    _socket.Dispose();
    return ValueTask.CompletedTask;
  }
}
