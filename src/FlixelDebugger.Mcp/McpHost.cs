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
using System.Net.Sockets;
using System.Text.Json;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Mcp.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp;

/// <summary>
/// The built-in MCP server that lets AI agents debug the connected games.
/// </summary>
/// <remarks>
/// <para>
/// MCP (the Model Context Protocol) is the standard way for agents such as Claude Code, Cursor,
/// and Codex to call tools. This host runs its <b>own</b> small web server, separate from the
/// game server, because the two have different audiences: games connect over a WebSocket on every
/// network interface if the person wants, while agents connect over HTTP and must only ever come
/// from this PC. So the MCP server always listens on the loopback address, even when the game
/// server does not, and it refuses requests whose <c>Origin</c> or <c>Host</c> header is not a
/// loopback name (the DNS rebinding protection the MCP specification asks for).
/// </para>
/// <para>
/// The endpoint is <c>http://127.0.0.1:port/mcp</c> (Streamable HTTP). The tools read the
/// latest state each <see cref="GameSession"/> already holds and send the same actions the
/// debugger's panels send. The SDK finds the tools with reflection, which is fine here because
/// tool calls are rare and not on any hot path.
/// </para>
/// <para>
/// Every tool call raises <see cref="Activity"/>, so the app can show that an agent is at work.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var mcp = new McpHost(server);
/// await mcp.StartAsync(6511);
/// Console.WriteLine(mcp.Url);   // http://127.0.0.1:6511/mcp
/// </code>
/// </example>
public sealed class McpHost : IAsyncDisposable
{
  private static readonly string[] _viewTools = ["viewport_pick"];

  private readonly DebugServer _server;
  private readonly SessionResolver _resolver;
  private readonly Lock _gate = new();
  private WebApplication? _app;
  private int _port;

  /// <summary>
  /// Creates a host. It does not listen until <see cref="StartAsync"/> is called.
  /// </summary>
  /// <param name="server">The game server whose connected games the tools act on.</param>
  public McpHost(DebugServer server)
  {
    ArgumentNullException.ThrowIfNull(server);
    _server = server;
    _resolver = new SessionResolver(server);
  }

  /// <summary>
  /// Raised for every tool call, on a background thread.
  /// </summary>
  public event EventHandler<AgentActivityEventArgs>? Activity;

  /// <summary>Gets whether the host is listening.</summary>
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

  /// <summary>
  /// Gets the port the host listens on, which is the real port when 0 was requested, or 0 when
  /// it is not running.
  /// </summary>
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

  /// <summary>
  /// Gets or sets how long the screenshot tool waits for the game's picture. The game gives up
  /// after 10 seconds and then sends nothing, so the default is a little longer than that.
  /// </summary>
  public TimeSpan CaptureTimeout { get; set; } = TimeSpan.FromSeconds(12);

  /// <summary>Gets the address agents connect to, or an empty string when not running.</summary>
  public string Url => Port > 0 ? $"http://127.0.0.1:{Port}/mcp" : string.Empty;

  /// <summary>
  /// Starts listening on the loopback address.
  /// </summary>
  /// <param name="port">The port to listen on. Use 0 to let the system pick a free one.</param>
  /// <param name="cancellationToken">A token that cancels the start.</param>
  /// <exception cref="PortInUseException">Another program already uses the port.</exception>
  /// <exception cref="InvalidOperationException">
  /// The host is already running, or the port is out of range.
  /// </exception>
  public async Task StartAsync(int port, CancellationToken cancellationToken = default)
  {
    if (port is < 0 or > 65535)
    {
      throw new InvalidOperationException(
          $"The port must be between 0 and 65535, but it is {port}.");
    }

    WebApplication app;
    lock (_gate)
    {
      if (_app is not null)
      {
        throw new InvalidOperationException("The MCP server is already running.");
      }

      app = Build(port);
      _app = app;
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
      }

      await app.DisposeAsync().ConfigureAwait(false);
      if (IsAddressInUse(e))
      {
        throw new PortInUseException(port, e);
      }

      throw;
    }

    lock (_gate)
    {
      _port = FindBoundPort(app, port);
    }
  }

  /// <summary>
  /// Stops listening. Does nothing when the host is not running.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the wait for open requests.</param>
  /// <returns>A task that completes when the host has stopped.</returns>
  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    WebApplication? app;
    lock (_gate)
    {
      app = _app;
      _app = null;
      _port = 0;
    }

    if (app is null)
    {
      return;
    }

    try
    {
      await app.StopAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      await app.DisposeAsync().ConfigureAwait(false);
    }
  }

  /// <inheritdoc/>
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

    var addresses = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()?.Addresses;
    foreach (var address in addresses ?? [])
    {
      if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Port > 0)
      {
        return uri.Port;
      }
    }

    return requested;
  }

  // The Host header names the address the client typed, and the Origin header names the web page
  // that made a browser send the request. A page on another site can make a browser reach this
  // loopback server, so both must be loopback names.
  private static bool IsLoopbackHost(string host)
  {
    var name = host;
    if (name.StartsWith('['))
    {
      var end = name.IndexOf(']', StringComparison.Ordinal);
      name = end > 0 ? name[1..end] : name;
    }
    else
    {
      var colon = name.LastIndexOf(':');
      name = colon >= 0 ? name[..colon] : name;
    }

    return name.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(name, out var ip) && IPAddress.IsLoopback(ip));
  }

  private static bool IsLoopbackOrigin(string origin) =>
      Uri.TryCreate(origin, UriKind.Absolute, out var uri) && IsLoopbackHost(uri.Authority);

  private WebApplication Build(int port)
  {
    var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
    {
      ContentRootPath = AppContext.BaseDirectory,
      EnvironmentName = Environments.Production,
    });
    builder.Logging.ClearProviders();
    builder.Services.AddSingleton<IHostLifetime, DetachedLifetime>();
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(2));
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
      kestrel.AddServerHeader = false;
      kestrel.Listen(IPAddress.Loopback, port, listen => listen.Protocols = HttpProtocols.Http1);
    });

    builder.Services.AddSingleton(_resolver);
    builder.Services.AddSingleton(
        _ => new ToolContext(_resolver) { CaptureTimeout = CaptureTimeout });
    builder.Services.AddMcpServer()
        .WithHttpTransport(options =>
        {
          // Sessions keep the client name from the initialize handshake for the whole session.
          // Newer clients carry it in every request instead, and are served without a session.
          options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients;
        })
        .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, ct) =>
        {
          // The SDK only says "An error occurred invoking the tool" when an argument has the wrong
          // type, and that is all a language model would see. Say which argument is wrong.
          if (context.MatchedPrimitive is McpServerTool tool &&
              ArgumentCheck.Find(tool.ProtocolTool.InputSchema, context.Params?.Arguments)
                  is { } problem)
          {
            return new CallToolResult
            {
              IsError = true,
              Content = [new TextContentBlock { Text = $"Invalid arguments: {problem}" }],
            };
          }

          Report(context.Params, context.Server.ClientInfo?.Name, after: false);
          var result = await next(context, ct).ConfigureAwait(false);
          Report(context.Params, context.Server.ClientInfo?.Name, after: true);
          return result;
        }))
        .WithTools<GameTools>()
        .WithTools<SceneTools>()
        .WithTools<ViewportTools>()
        .WithTools<DataTools>()
        .WithTools<ScreenTools>()
        .WithTools<InputTools>();

    var app = builder.Build();
    app.Use(async (context, next) =>
    {
      var origin = context.Request.Headers.Origin.ToString();
      var host = context.Request.Host.ToString();
      if ((origin.Length > 0 && !IsLoopbackOrigin(origin)) ||
          (host.Length > 0 && !IsLoopbackHost(host)))
      {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync(
            "Forbidden. The debugger's MCP server only answers requests from this PC.\n")
            .ConfigureAwait(false);
        return;
      }

      await next(context).ConfigureAwait(false);
    });
    app.MapMcp("/mcp");
    return app;
  }

  // Works out what a call touched from its own arguments, so the tools need no extra code.
  // "after" is true when the call finished, which lets a pick report the object it selected.
  private void Report(
      ModelContextProtocol.Protocol.CallToolRequestParams? call, string? client, bool after)
  {
    var handler = Activity;
    if (handler is null || call is null || (after && !_viewTools.Contains(call.Name)))
    {
      return;
    }

    try
    {
      var args = call.Arguments;
      string? sessionText = null;
      if (args is not null && args.TryGetValue("session", out var s) &&
          s.ValueKind == JsonValueKind.String)
      {
        sessionText = s.GetString();
      }

      Guid? sessionId = null;
      GameSession? game = null;
      if (_resolver.TryResolve(sessionText, out var found))
      {
        game = found;
        sessionId = found.Id;
      }

      var objectId = TouchedObject(game, call.Name, args, after);
      float? viewX = null;
      float? viewY = null;
      if (args is not null && args.TryGetValue("x", out var x) &&
          args.TryGetValue("y", out var y) &&
          x.ValueKind == JsonValueKind.Number && y.ValueKind == JsonValueKind.Number)
      {
        viewX = x.GetSingle();
        viewY = y.GetSingle();
      }

      handler(this, new AgentActivityEventArgs(
          string.IsNullOrWhiteSpace(client) ? "Agent" : client,
          call.Name,
          sessionId,
          objectId,
          viewX,
          viewY,
          DateTimeOffset.UtcNow));
    }
    catch (Exception e) when (e is InvalidOperationException or FormatException)
    {
      // Activity is only a courtesy to the person watching. A bad argument must not fail the call.
      System.Diagnostics.Debug.WriteLine($"Could not report agent activity: {e.Message}");
    }
  }

  private static int? TouchedObject(
      GameSession? game,
      string tool,
      IDictionary<string, JsonElement>? args,
      bool after)
  {
    if (game is null)
    {
      return null;
    }

    if (after)
    {
      return game.Inspector is { SelectedId: >= 0 } selected ? selected.SelectedId : null;
    }

    if (args is null)
    {
      return null;
    }

    foreach (var key in (ReadOnlySpan<string>)["target", "id", "root"])
    {
      if (!args.TryGetValue(key, out var value))
      {
        continue;
      }

      var text = value.ValueKind switch
      {
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.String => value.GetString(),
        _ => null,
      };
      if (tool is not "get_thumbnail" and not "get_scene" &&
          SceneAddress.TryResolveId(game.Scene, text, out var id))
      {
        return id;
      }
    }

    return null;
  }
}
