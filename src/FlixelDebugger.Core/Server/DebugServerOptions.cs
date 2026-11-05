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

using FlixelDebugger.Protocol;

namespace FlixelDebugger.Core.Server;

/// <summary>
/// The settings of a <see cref="DebugServer"/>. Change them before calling <see cref="DebugServer.StartAsync"/>.
/// </summary>
public sealed class DebugServerOptions
{
    /// <summary>Gets or sets the port to listen on. Use 0 to let the operating system pick a free one.</summary>
    public int Port { get; set; } = ProtocolConstants.DefaultPort;

    /// <summary>
    /// Gets or sets whether the server listens on all network interfaces instead of only on this PC.
    /// </summary>
    /// <remarks>
    /// Turn this on to debug a phone over Wi-Fi. Anyone on the same network can then connect to the debugger,
    /// so keep it off on networks you do not trust.
    /// </remarks>
    public bool ListenOnAllInterfaces { get; set; }

    /// <summary>Gets or sets how often each session sends a PING. The protocol suggests about once a second.</summary>
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets how long a new connection may take to send HELLO before it is closed.</summary>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates an independent copy, so a running server is not affected by later changes.
    /// </summary>
    /// <returns>A new options object with the same values.</returns>
    public DebugServerOptions Clone() => new()
    {
        Port = Port,
        ListenOnAllInterfaces = ListenOnAllInterfaces,
        PingInterval = PingInterval,
        HandshakeTimeout = HandshakeTimeout,
    };
}
