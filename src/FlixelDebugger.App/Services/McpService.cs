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

using FlixelDebugger.Mcp;

namespace FlixelDebugger.App.Services;

/// <summary>
/// The <see cref="IMcpService"/> of the real app: it wraps the <see cref="McpHost"/>.
/// </summary>
public sealed class McpService : IMcpService, IAsyncDisposable
{
  private readonly McpHost _host;

  /// <summary>
  /// Creates the service around a host.
  /// </summary>
  /// <param name="host">The MCP host to start and stop.</param>
  public McpService(McpHost host)
  {
    ArgumentNullException.ThrowIfNull(host);
    _host = host;
    host.Activity += (_, e) => Activity?.Invoke(this, e);
  }

  /// <inheritdoc/>
  public event EventHandler<AgentActivityEventArgs>? Activity;

  /// <inheritdoc/>
  public bool IsRunning => _host.IsRunning;

  /// <inheritdoc/>
  public string Url => _host.Url;

  /// <inheritdoc/>
  public Task StartAsync(int port, CancellationToken cancellationToken = default) =>
      _host.StartAsync(port, cancellationToken);

  /// <inheritdoc/>
  public Task StopAsync(CancellationToken cancellationToken = default) =>
      _host.StopAsync(cancellationToken);

  /// <inheritdoc/>
  public ValueTask DisposeAsync() => _host.DisposeAsync();
}
