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

namespace FlixelDebugger.Core.Server;

/// <summary>
/// The exception thrown when the server cannot start because another program already uses its port.
/// </summary>
/// <remarks>
/// The usual cause is a second copy of the debugger. Close it, or choose another port in the
/// settings.
/// </remarks>
public sealed class PortInUseException : InvalidOperationException
{
  /// <summary>
  /// Creates an exception with a default message.
  /// </summary>
  public PortInUseException()
      : base("The port is already in use.") { }

  /// <summary>
  /// Creates an exception with the given description.
  /// </summary>
  /// <param name="message">What went wrong.</param>
  public PortInUseException(string message)
      : base(message) { }

  /// <summary>
  /// Creates an exception with the given description and a wrapped cause.
  /// </summary>
  /// <param name="message">What went wrong.</param>
  /// <param name="innerException">The exception that caused this one.</param>
  public PortInUseException(string message, Exception innerException)
      : base(message, innerException) { }

  /// <summary>
  /// Creates an exception for a specific port.
  /// </summary>
  /// <param name="port">The port that is taken.</param>
  /// <param name="innerException">The exception that Kestrel threw.</param>
  public PortInUseException(int port, Exception innerException)
      : base($"Port {port} is already in use. Another copy of the debugger may be running; close it or choose another port.", innerException)
  {
    Port = port;
  }

  /// <summary>Gets the port that could not be used, or 0 if it is not known.</summary>
  public int Port { get; }
}
