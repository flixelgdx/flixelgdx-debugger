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
using System.Net.WebSockets;
using FlixelDebugger.Core.Server;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace FlixelDebugger.Core.Tests;

public class DebugServerTests
{
  [Fact(Timeout = 15000)]
  public async Task StartsOnAFreePortAndStops()
  {
    var server = new DebugServer(new DebugServerOptions { Port = 0 });
    Assert.False(server.IsRunning);
    Assert.Equal(0, server.Port);

    await server.StartAsync(Deadline.Token());
    Assert.True(server.IsRunning);
    Assert.InRange(server.Port, 1, 65535);

    await server.StopAsync(Deadline.Token());
    Assert.False(server.IsRunning);
    Assert.Equal(0, server.Port);
  }

  [Fact(Timeout = 15000)]
  public async Task CanBeStartedAgainAfterStopping()
  {
    var server = new DebugServer(new DebugServerOptions { Port = 0 });
    await server.StartAsync(Deadline.Token());
    await server.StopAsync(Deadline.Token());

    await server.StartAsync(Deadline.Token());
    await using var game = await FakeGame.ConnectAsync(server.Port);
    await game.HandshakeAsync();
    await server.StopAsync(Deadline.Token());
  }

  [Fact(Timeout = 15000)]
  public async Task StartingTwiceThrows()
  {
    await using var server = await TestServer.StartAsync();
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => server.Server.StartAsync(Deadline.Token()));
  }

  [Fact(Timeout = 15000)]
  public async Task StoppingAServerThatIsNotRunningDoesNothing()
  {
    await new DebugServer().StopAsync(Deadline.Token());
  }

  [Fact(Timeout = 15000)]
  public async Task ARejectedPortThrows()
  {
    var server = new DebugServer(new DebugServerOptions { Port = 70000 });
    await Assert.ThrowsAsync<InvalidOperationException>(() => server.StartAsync(Deadline.Token()));
    Assert.False(server.IsRunning);
  }

  [Fact(Timeout = 15000)]
  public async Task ReportsWhenThePortIsAlreadyInUse()
  {
    await using var first = await TestServer.StartAsync();
    var second = new DebugServer(new DebugServerOptions { Port = first.Port });

    var error = await Assert.ThrowsAsync<PortInUseException>(
        () => second.StartAsync(Deadline.Token()));

    Assert.Equal(first.Port, error.Port);
    Assert.Contains(
        first.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        error.Message,
        StringComparison.Ordinal);
    Assert.False(second.IsRunning);
  }

  [Fact(Timeout = 15000)]
  public async Task AcceptsGamesOnAllInterfacesWhenAsked()
  {
    await using var server = await TestServer.StartAsync(o => o.ListenOnAllInterfaces = true);
    await using var game = await FakeGame.ConnectAsync(server.Port);
    await game.HandshakeAsync();
    Assert.NotNull(await server.NextConnectedAsync());
  }

  [Fact(Timeout = 15000)]
  public async Task APlainHttpRequestToTheRootGetsHttp426AndATextPage()
  {
    await using var server = await TestServer.StartAsync();
    using var http = new HttpClient();

    using var response = await http.GetAsync(
        new Uri($"http://127.0.0.1:{server.Port}/"), Deadline.Token());
    var body = await response.Content.ReadAsStringAsync(Deadline.Token());

    Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
    Assert.Contains(
        "websocket",
        string.Join(',', response.Headers.GetValues("Upgrade")),
        StringComparison.OrdinalIgnoreCase);
    Assert.Contains("WebSocket", body, StringComparison.Ordinal);
    Assert.StartsWith(
        "text/plain", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
  }

  [Fact(Timeout = 15000)]
  public async Task OtherPathsGetHttp404()
  {
    await using var server = await TestServer.StartAsync();
    using var http = new HttpClient();

    using var response = await http.GetAsync(
        new Uri($"http://127.0.0.1:{server.Port}/nothing"), Deadline.Token());

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact(Timeout = 15000)]
  public async Task WebSocketsOnOtherPathsAreRefused()
  {
    await using var server = await TestServer.StartAsync();
    using var socket = new ClientWebSocket();

    await Assert.ThrowsAsync<WebSocketException>(
        () =>
            socket.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/other"), Deadline.Token()));
  }

  [Fact(Timeout = 15000)]
  public async Task StoppingClosesEveryGameWithAReason()
  {
    var server = await TestServer.StartAsync();
    await using var game = await FakeGame.ConnectAsync(server.Port);
    await game.HandshakeAsync();
    await server.NextConnectedAsync();

    var stopping = server.Server.StopAsync(Deadline.Token());
    while (await game.ReceiveBytesAsync() is not null)
    {
      // Skip pings until the close frame arrives.
    }

    await stopping;
    Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, game.CloseStatus);
    Assert.Contains("shutting down", game.CloseDescription, StringComparison.Ordinal);
  }

  [Fact]
  public void KestrelSocketTransportHasNoDelayOnByDefault()
  {
    // The server still sets NoDelay explicitly. This test records that the default is on, as
    // checked when it was written.
    Assert.True(new SocketTransportOptions().NoDelay);
  }
}
