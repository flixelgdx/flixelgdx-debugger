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
/// One entry of a <see cref="FeedList"/>: a typed data feed the game offers.
/// </summary>
/// <param name="Index">The number all later feed messages use to refer to this feed.</param>
/// <param name="Id">A stable identifier such as <c>stats</c> or <c>scene</c>.</param>
/// <param name="Kind">The feed kind, which decides how its payloads are encoded.</param>
public readonly record struct FeedInfo(ushort Index, string Id, FeedKind Kind)
{
  /// <summary>Reads one feed entry.</summary>
  /// <param name="reader">The reader positioned at the entry.</param>
  /// <returns>The entry.</returns>
  public static FeedInfo Read(ref MessageReader reader)
  {
    var index = reader.ReadU16();
    var id = reader.ReadString();
    return new FeedInfo(index, id, (FeedKind)reader.ReadU8());
  }

  /// <summary>Writes this entry.</summary>
  /// <param name="writer">The writer.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU16(Index).WriteString(Id).WriteU8((byte)Kind);
  }
}
