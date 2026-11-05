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

using System.Collections.Concurrent;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;

namespace FlixelDebugger.App.Sessions;

/// <summary>
/// The <see cref="IDebugHost"/> of the real app: it wraps Core's <see cref="DebugServer"/> and wraps every Core
/// session in a <see cref="GameSessionAdapter"/>.
/// </summary>
/// <remarks>
/// The same Core session always maps to the same adapter, so the app can compare sessions by reference.
/// </remarks>
public sealed class DebugHost : IDebugHost
{
    private readonly DebugServer _server;
    private readonly ConcurrentDictionary<GameSession, GameSessionAdapter> _adapters = new();

    /// <summary>
    /// Creates a host around a server.
    /// </summary>
    /// <param name="server">The server to wrap. Its options are read from its <see cref="DebugServer.Options"/>.</param>
    public DebugHost(DebugServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        _server = server;
        server.SessionConnected += (_, e) => SessionConnected?.Invoke(this, new HostSessionEventArgs(Wrap(e.Session)));
        server.SessionDisconnected += (_, e) =>
        {
            var adapter = Wrap(e.Session);
            _adapters.TryRemove(new KeyValuePair<GameSession, GameSessionAdapter>(e.Session, adapter));
            SessionDisconnected?.Invoke(this, new HostSessionEventArgs(adapter));
        };
    }

    /// <inheritdoc/>
    public event EventHandler<HostSessionEventArgs>? SessionConnected;

    /// <inheritdoc/>
    public event EventHandler<HostSessionEventArgs>? SessionDisconnected;

    /// <inheritdoc/>
    public bool IsRunning => _server.IsRunning;

    /// <inheritdoc/>
    public int Port => _server.IsRunning ? _server.Port : _server.Options.Port;

    /// <inheritdoc/>
    public string ListenAddress =>
        $"ws://{(_server.Options.ListenOnAllInterfaces ? "0.0.0.0" : "127.0.0.1")}:{Port}/";

    /// <inheritdoc/>
    public IReadOnlyList<IGameSession> Sessions
    {
        get
        {
            var sessions = _server.Sessions;
            var list = new IGameSession[sessions.Count];
            for (var i = 0; i < list.Length; i++)
            {
                list[i] = Wrap(sessions[i]);
            }

            return list;
        }
    }

    /// <inheritdoc/>
    public void Configure(int port, bool listenOnAllInterfaces)
    {
        _server.Options.Port = port;
        _server.Options.ListenOnAllInterfaces = listenOnAllInterfaces;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default) => _server.StartAsync(cancellationToken);

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default) => _server.StopAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private GameSessionAdapter Wrap(GameSession session) => _adapters.GetOrAdd(session, static s => new GameSessionAdapter(s));
}
