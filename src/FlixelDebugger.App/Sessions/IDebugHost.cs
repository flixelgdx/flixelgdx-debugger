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

namespace FlixelDebugger.App.Sessions;

/// <summary>
/// Describes a session that connected to or disconnected from the host.
/// </summary>
/// <param name="session">The session.</param>
public sealed class HostSessionEventArgs(IGameSession session) : EventArgs
{
  /// <summary>Gets the session.</summary>
  public IGameSession Session { get; } = session;
}

/// <summary>
/// The server the games connect to, as the main ViewModel sees it.
/// </summary>
/// <remarks>
/// The real implementation is <see cref="DebugHost"/>, which wraps Core's <c>DebugServer</c>. Tests use a fake
/// that raises the events by hand, so no socket is needed.
/// </remarks>
public interface IDebugHost : IAsyncDisposable
{
  /// <summary>Raised after a game finished its handshake. It may be raised on a background thread.</summary>
  public event EventHandler<HostSessionEventArgs>? SessionConnected;

  /// <summary>Raised after a connected game disconnected. It may be raised on a background thread.</summary>
  public event EventHandler<HostSessionEventArgs>? SessionDisconnected;

  /// <summary>Gets whether the server is listening.</summary>
  public bool IsRunning { get; }

  /// <summary>Gets the port the server listens on (or will listen on after the next start).</summary>
  public int Port { get; }

  /// <summary>Gets the address games connect to, for example <c>ws://127.0.0.1:6510/</c>.</summary>
  public string ListenAddress { get; }

  /// <summary>Gets the sessions that finished their handshake and are still connected.</summary>
  public IReadOnlyList<IGameSession> Sessions { get; }

  /// <summary>
  /// Sets the port and the network scope for the next start.
  /// </summary>
  /// <param name="port">The port to listen on.</param>
  /// <param name="listenOnAllInterfaces">Whether to accept connections from other devices on the network.</param>
  public void Configure(int port, bool listenOnAllInterfaces);

  /// <summary>
  /// Starts listening.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the start.</param>
  /// <returns>A task that completes when the server accepts connections.</returns>
  /// <exception cref="FlixelDebugger.Core.Server.PortInUseException">Another program already uses the port.</exception>
  public Task StartAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Stops listening and closes every session. Stopping a stopped server does nothing.
  /// </summary>
  /// <param name="cancellationToken">A token that stops waiting for a graceful shutdown.</param>
  /// <returns>A task that completes when the server is stopped.</returns>
  public Task StopAsync(CancellationToken cancellationToken = default);
}
