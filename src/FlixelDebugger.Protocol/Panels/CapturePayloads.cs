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

using Subtypes = FlixelDebugger.Protocol.ProtocolConstants.Subtypes;

namespace FlixelDebugger.Protocol.Panels;

/// <summary>
/// Whether the game can take pictures of its screen, from the <c>capture</c> panel (kind 12,
/// subtype 1).
/// </summary>
/// <remarks>Sent on connect and whenever it changes. Needs protocol 1.1.</remarks>
/// <param name="Supported">
/// <see langword="false"/> when the graphics backend cannot read the finished frame back.
/// </param>
public sealed record CaptureStatePayload(bool Supported) : PanelPayload
{
  /// <summary>
  /// Reads a capture state payload.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the subtype byte.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static CaptureStatePayload Read(ref MessageReader reader) => new(reader.ReadBool());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Subtypes.Capture.State).WriteBool(Supported);
  }
}

/// <summary>
/// A picture of the game's screen from the <c>capture</c> panel (kind 12, subtype 2).
/// </summary>
/// <param name="RequestId">The id the app chose in the matching request.</param>
/// <param name="Width">The picture width in pixels.</param>
/// <param name="Height">The picture height in pixels.</param>
/// <param name="Png">The PNG image bytes.</param>
public sealed record CaptureImagePayload(
    int RequestId, int Width, int Height, ReadOnlyMemory<byte> Png) : PanelPayload
{
  /// <summary>
  /// Reads a capture image payload.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the subtype byte.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static CaptureImagePayload Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    var width = reader.ReadI32();
    var height = reader.ReadI32();
    return new CaptureImagePayload(id, width, height, reader.ReadBytes().ToArray());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Subtypes.Capture.Image)
        .WriteI32(RequestId)
        .WriteI32(Width)
        .WriteI32(Height)
        .WriteBytes(Png.Span);
  }
}
