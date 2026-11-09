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
/// A round-trip probe from the app. The game answers with a <see cref="Pong"/> carrying the same
/// nonce.
/// </summary>
/// <param name="Nonce">Any number; a timestamp works well.</param>
/// <param name="RttMs">
/// The round trip the app measured for the previous ping, in milliseconds, or -1 when it has none
/// yet. The game shows it in its panels.
/// </param>
public sealed record Ping(long Nonce, float RttMs = -1f) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.Ping;

  /// <summary>Reads the body of a ping.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The ping.</returns>
  public static Ping Read(ref MessageReader reader)
  {
    var nonce = reader.ReadI64();
    return new Ping(nonce, reader.ReadF32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.Ping).WriteI64(Nonce).WriteF32(RttMs);
  }
}
