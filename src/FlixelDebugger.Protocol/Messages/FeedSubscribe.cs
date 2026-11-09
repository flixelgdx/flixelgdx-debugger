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
/// Starts or stops the data of one feed. Subscribing makes the game send the feed's whole state.
/// </summary>
/// <param name="Feed">The feed index from the <see cref="FeedList"/>.</param>
/// <param name="On"><see langword="true"/> to subscribe.</param>
public sealed record FeedSubscribe(ushort Feed, bool On) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.FeedSubscribe;

  /// <summary>Reads the body of the message.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The message.</returns>
  public static FeedSubscribe Read(ref MessageReader reader)
  {
    var feed = reader.ReadU16();
    return new FeedSubscribe(feed, reader.ReadBool());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.FeedSubscribe).WriteU16(Feed).WriteBool(On);
  }
}
