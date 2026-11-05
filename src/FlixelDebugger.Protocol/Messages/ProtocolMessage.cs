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

namespace FlixelDebugger.Protocol.Messages;

/// <summary>
/// A whole protocol message, decoded.
/// </summary>
/// <remarks>
/// Use <see cref="MessageDecoder"/> to turn received bytes into one of these, and
/// <see cref="Write(MessageWriter)"/> or <see cref="ToArray"/> to turn one back into bytes.
/// </remarks>
public abstract record ProtocolMessage
{
  /// <summary>
  /// Gets the message type byte that starts this message.
  /// </summary>
  public abstract MessageType Type { get; }

  /// <summary>
  /// Starts a new message on the writer and writes the type byte and every field.
  /// </summary>
  /// <remarks>
  /// Call <see cref="MessageWriter.Finish"/> afterwards to get the bytes.
  /// </remarks>
  /// <param name="writer">The writer to use. Anything it held before is discarded.</param>
  public abstract void Write(MessageWriter writer);

  /// <summary>
  /// Encodes this message into a new byte array.
  /// </summary>
  /// <returns>The encoded message, including the type byte.</returns>
  public byte[] ToArray()
  {
    var writer = new MessageWriter();
    Write(writer);
    return writer.Finish().ToArray();
  }
}
