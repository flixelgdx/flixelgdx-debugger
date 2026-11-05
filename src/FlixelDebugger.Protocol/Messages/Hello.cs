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
/// The first message of every connection, sent by the game to introduce itself.
/// </summary>
/// <param name="ProtocolMajor">The protocol major version the game speaks.</param>
/// <param name="ProtocolMinor">The protocol minor version the game speaks.</param>
/// <param name="Platform">The platform: <c>desktop</c>, <c>android</c>, or <c>web</c>.</param>
/// <param name="Device">A device or operating system description.</param>
/// <param name="ScreenWidth">The screen width in pixels.</param>
/// <param name="ScreenHeight">The screen height in pixels.</param>
/// <param name="GameTitle">The title of the game.</param>
/// <param name="FrameworkVersion">The FlixelGDX version the game runs on.</param>
public sealed record Hello(
    ushort ProtocolMajor,
    ushort ProtocolMinor,
    string Platform,
    string Device,
    int ScreenWidth,
    int ScreenHeight,
    string GameTitle,
    string FrameworkVersion) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.Hello;

  /// <summary>
  /// Reads the fields of a HELLO message.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the type byte.</param>
  /// <returns>The message that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static Hello Read(ref MessageReader reader)
  {
    var major = reader.ReadU16();
    var minor = reader.ReadU16();
    var platform = reader.ReadString();
    var device = reader.ReadString();
    var width = reader.ReadI32();
    var height = reader.ReadI32();
    var title = reader.ReadString();
    return new Hello(major, minor, platform, device, width, height, title, reader.ReadString());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.Hello)
        .WriteU16(ProtocolMajor)
        .WriteU16(ProtocolMinor)
        .WriteString(Platform)
        .WriteString(Device)
        .WriteI32(ScreenWidth)
        .WriteI32(ScreenHeight)
        .WriteString(GameTitle)
        .WriteString(FrameworkVersion);
  }
}
