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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using FlixelDebugger.Core.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlixelDebugger.Core.Server;

/// <summary>
/// The WebSocket server that games connect to.
/// </summary>
/// <remarks>
/// <para>
/// The debugger is the <i>server</i> and every game is a <i>client</i>. Think of a reception desk: the desk
/// (this class) stays open, and each game that walks up gets its own clerk (a <see cref="GameSession"/>) who
/// handles the conversation with that game only. Several games can be connected at the same time.
/// </para>
/// <para>
/// The server listens on <c>127.0.0.1</c> by default, so only programs on this PC (and phones reached through
/// <c>adb reverse</c>) can connect. Set <see cref="DebugServerOptions.ListenOnAllInterfaces"/> to accept
/// connections from other devices on the network.
/// </para>
/// <para>
/// WebSocket connections are accepted at <c>/</c>. Any other HTTP request receives a short text page (HTTP 426 for
/// a plain request to <c>/</c>, HTTP 404 for other paths), so opening the address in a browser explains itself.
/// </para>
/// <para>
/// <b>Threads.</b> Events are raised on background threads. The UI must move to its own thread before touching a
/// control. Core never does that for you, because it does not know which UI toolkit you use.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var server = new DebugServer(new DebugServerOptions { Port = 6510 });
/// server.SessionConnected += (_, e) => Console.WriteLine($"{e.Session.DisplayName} connected");
/// await server.StartAsync();
/// </code>
/// </example>
public sealed class DebugServer : IAsyncDisposable
{
  private static readonly TimeSpan _stopGrace = TimeSpan.FromSeconds(3);

  private readonly Lock _gate = new();
  private readonly List<GameSession> _sessions = [];
  private WebApplication? _app;
  private CancellationTokenSource? _stopping;
  private int _port;

  /// <summary>
  /// Creates a server with the default options (port 6510, this PC only).
  /// </summary>
  public DebugServer()
      : this(new DebugServerOptions()) { }

  /// <summary>
  /// Creates a server.
  /// </summary>
  /// <param name="options">
  /// The options. They are read when <see cref="StartAsync"/> is called, so change them before starting.
  /// </param>
  public DebugServer(DebugServerOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    Options = options;
  }

  /// <summary>Raised after a game finished its handshake. It is raised on a background thread.</summary>
  public event EventHandler<SessionEventArgs>? SessionConnected;

  /// <summary>Raised after a game that had finished its handshake disconnected. It is raised on a background thread.</summary>
  public event EventHandler<SessionEventArgs>? SessionDisconnected;

  /// <summary>Raised when a connection ended before its handshake finished, for example on a protocol version mismatch.</summary>
  public event EventHandler<HandshakeFailedEventArgs>? HandshakeFailed;

  /// <summary>Gets the options. Changes take effect the next time the server starts.</summary>
  public DebugServerOptions Options { get; }

  /// <summary>Gets whether the server is listening.</summary>
  public bool IsRunning
  {
    get
    {
      lock (_gate)
      {
        return _app is not null;
      }
    }
  }

  /// <summary>Gets the port the running server listens on, or 0 when it is stopped. This is the real port even if the options asked for port 0.</summary>
  public int Port
  {
    get
    {
      lock (_gate)
      {
        return _port;
      }
    }
  }

  /// <summary>Gets the sessions that finished their handshake and are still connected.</summary>
  public IReadOnlyList<GameSession> Sessions
  {
    get
    {
      lock (_gate)
      {
        return [.. _sessions];
      }
    }
  }

  /// <summary>
  /// Starts listening.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the start.</param>
  /// <returns>A task that completes when the server accepts connections.</returns>
  /// <exception cref="InvalidOperationException">The server is already running.</exception>
  /// <exception cref="PortInUseException">Another program already uses the port.</exception>
  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    var options = Options.Clone();
    if (options.Port is < 0 or > 65535)
    {
      throw new InvalidOperationException($"The port must be between 0 and 65535, but it is {options.Port}.");
    }

    var stopping = new CancellationTokenSource();
    WebApplication app;
    lock (_gate)
    {
      if (_app is not null)
      {
        stopping.Dispose();
        throw new InvalidOperationException("The server is already running.");
      }

      app = Build(options, stopping.Token);
      _app = app;
      _stopping = stopping;
    }

    try
    {
      await app.StartAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (Exception e)
    {
      lock (_gate)
      {
        _app = null;
        _stopping = null;
      }

      stopping.Dispose();
      await app.DisposeAsync().ConfigureAwait(false);
      if (IsAddressInUse(e))
      {
        throw new PortInUseException(options.Port, e);
      }

      throw;
    }

    lock (_gate)
    {
      _port = FindBoundPort(app, options.Port);
    }
  }

  /// <summary>
  /// Stops listening and closes every session.
  /// </summary>
  /// <remarks>Stopping a server that is not running does nothing.</remarks>
  /// <param name="cancellationToken">A token that stops waiting for a graceful shutdown.</param>
  /// <returns>A task that completes when the server is stopped.</returns>
  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    WebApplication? app;
    CancellationTokenSource? stopping;
    GameSession[] sessions;
    lock (_gate)
    {
      app = _app;
      stopping = _stopping;
      sessions = [.. _sessions];
      _app = null;
      _stopping = null;
      _port = 0;
    }

    if (app is null)
    {
      return;
    }

    try
    {
      // Ask politely first, so the games see a normal close. Then force the rest.
      await Task.WhenAll(sessions.Select(s => s.CloseAsync("The debugger is shutting down.", WebSocketCloseStatus.EndpointUnavailable))).ConfigureAwait(false);
      using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      grace.CancelAfter(_stopGrace);
      while (!grace.IsCancellationRequested && sessions.Any(s => s.State != SessionState.Closed))
      {
        await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
      }
    }
    finally
    {
      await (stopping?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
      await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
      await app.DisposeAsync().ConfigureAwait(false);
      stopping?.Dispose();
    }
  }

  /// <summary>
  /// Stops the server and releases its resources.
  /// </summary>
  /// <returns>A task that completes when the server is stopped.</returns>
  public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

  private static bool IsAddressInUse(Exception exception)
  {
    for (var e = exception; e is not null; e = e.InnerException)
    {
      if (e is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } ||
          e.GetType().Name == "AddressInUseException" ||
          e.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  private static int FindBoundPort(WebApplication app, int requested)
  {
    if (requested != 0)
    {
      return requested;
    }

    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
    foreach (var address in addresses ?? [])
    {
      if (Uri.TryCreate(address.Replace("[::]", "localhost", StringComparison.Ordinal), UriKind.Absolute, out var uri) && uri.Port > 0)
      {
        return uri.Port;
      }
    }

    return requested;
  }

  private void Raise<T>(EventHandler<T>? handler, T args)
      where T : EventArgs
  {
    try
    {
      handler?.Invoke(this, args);
    }
    catch (Exception e)
    {
      // A faulty subscriber must not break the connection. Leave a trace for the developer instead.
      Debug.WriteLine($"A DebugServer event handler threw: {e}");
    }
  }

  private static Task WriteTextAsync(HttpContext context, int status, string text)
  {
    context.Response.StatusCode = status;
    context.Response.ContentType = "text/plain; charset=utf-8";
    return context.Response.WriteAsync(text, context.RequestAborted);
  }

  private WebApplication Build(DebugServerOptions options, CancellationToken stopping)
  {
    var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
    {
      ContentRootPath = AppContext.BaseDirectory,
      EnvironmentName = Environments.Production,
    });
    builder.Logging.ClearProviders();

    // The default console lifetime would grab Ctrl+C and print messages. The app decides when the server stops.
    builder.Services.AddSingleton<IHostLifetime, DetachedLifetime>();
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(2));

    // Messages are small and latency matters, so never wait to batch them (Nagle's algorithm off). Kestrel
    // already turns this on by default; it is set explicitly so a change of that default cannot slow us down.
    builder.Services.Configure<SocketTransportOptions>(o => o.NoDelay = true);

    var address = options.ListenOnAllInterfaces ? IPAddress.Any : IPAddress.Loopback;
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
      kestrel.AddServerHeader = false;
      kestrel.Listen(address, options.Port, listen => listen.Protocols = HttpProtocols.Http1);
    });

    var app = builder.Build();
    app.UseWebSockets();
    app.Run(context => HandleAsync(context, options, stopping));
    return app;
  }

  private async Task HandleAsync(HttpContext context, DebugServerOptions options, CancellationToken stopping)
  {
    var path = context.Request.Path;
    if (path.HasValue && path.Value != "/")
    {
      await WriteTextAsync(context, StatusCodes.Status404NotFound, "Not found. Games connect to this debugger at ws://host:port/.\n")
          .ConfigureAwait(false);
      return;
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
      context.Response.Headers.Upgrade = "websocket";
      await WriteTextAsync(
          context,
          StatusCodes.Status426UpgradeRequired,
          "FlixelGDX Debugger\nThis address accepts WebSocket connections from a FlixelGDX game running in debug mode.\n")
          .ConfigureAwait(false);
      return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    var remote = $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}";
    await using var session = new GameSession(socket, remote, options);
    var connected = false;
    session.StateChanged += (_, _) =>
    {
      if (!connected && session.State == SessionState.Live)
      {
        connected = true;
        lock (_gate)
        {
          _sessions.Add(session);
        }

        Raise(SessionConnected, new SessionEventArgs(session));
      }
    };

    try
    {
      await session.RunAsync(stopping).ConfigureAwait(false);
    }
    finally
    {
      lock (_gate)
      {
        _sessions.Remove(session);
      }

      if (connected)
      {
        Raise(SessionDisconnected, new SessionEventArgs(session));
      }
      else
      {
        Raise(HandshakeFailed, new HandshakeFailedEventArgs(remote, session.CloseReason ?? "The connection closed before HELLO."));
      }
    }
  }
}
