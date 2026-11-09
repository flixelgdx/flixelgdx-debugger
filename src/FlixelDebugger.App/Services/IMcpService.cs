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
/// The app's view of the built-in MCP server, so ViewModels can start it and follow what agents do
/// without depending on the web host.
/// </summary>
/// <remarks>
/// The real implementation is <see cref="McpService"/>. Tests use a fake that raises
/// <see cref="Activity"/> by hand.
/// </remarks>
public interface IMcpService
{
  /// <summary>Raised for every tool call of an agent, on a background thread.</summary>
  public event EventHandler<AgentActivityEventArgs>? Activity;

  /// <summary>Gets whether the MCP server is listening.</summary>
  public bool IsRunning { get; }

  /// <summary>Gets the address agents connect to, or an empty string when not running.</summary>
  public string Url { get; }

  /// <summary>
  /// Starts the MCP server on the loopback address.
  /// </summary>
  /// <param name="port">The port to listen on.</param>
  /// <param name="cancellationToken">A token that cancels the start.</param>
  /// <exception cref="Core.Server.PortInUseException">Another program uses the port.</exception>
  /// <returns>A task that completes when the server listens.</returns>
  public Task StartAsync(int port, CancellationToken cancellationToken = default);

  /// <summary>
  /// Stops the MCP server. Does nothing when it is not running.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the wait for open requests.</param>
  /// <returns>A task that completes when the server has stopped.</returns>
  public Task StopAsync(CancellationToken cancellationToken = default);
}
