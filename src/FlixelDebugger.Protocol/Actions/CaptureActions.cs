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

using A = FlixelDebugger.Protocol.ProtocolConstants.Actions;

namespace FlixelDebugger.Protocol.Actions;

/// <summary>
/// Asks for a picture of the next finished frame (capture feed).
/// </summary>
/// <param name="RequestId">A number the app chooses; it comes back with the picture.</param>
/// <param name="MaxSide">
/// The most pixels the longest side may have. The game clamps it to 64 through 2048.
/// </param>
public sealed record CaptureRequestAction(int RequestId, ushort MaxSide) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Capture.Request;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static CaptureRequestAction Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    return new CaptureRequestAction(id, reader.ReadU16());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(RequestId).WriteU16(MaxSide);
  }
}
