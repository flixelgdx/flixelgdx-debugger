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

using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Protocol.Messages;

/// <summary>
/// A payload for one panel, sent by the game.
/// </summary>
/// <param name="Panel">The panel index from the last <see cref="PanelList"/>.</param>
/// <param name="Payload">The decoded payload. Its type depends on the panel kind.</param>
public sealed record PanelData(ushort Panel, PanelPayload Payload) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.PanelData;

  /// <summary>
  /// Reads the fields of a PANEL_DATA message.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the type byte.</param>
  /// <param name="kindOf">Finds the kind of a panel from its index.</param>
  /// <returns>The message that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static PanelData Read(ref MessageReader reader, PanelKindLookup kindOf)
  {
    ArgumentNullException.ThrowIfNull(kindOf);
    var panel = reader.ReadU16();
    return new PanelData(panel, PanelPayload.Read(kindOf(panel), ref reader));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.PanelData).WriteU16(Panel);
    Payload.Write(writer);
  }
}
