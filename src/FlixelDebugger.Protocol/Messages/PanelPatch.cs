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

using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.Protocol.Messages;

/// <summary>
/// One change of a <see cref="PanelPatch"/>: a property that was replaced or a list that grew.
/// </summary>
/// <param name="Widget">The id of the widget.</param>
/// <param name="Code">Whether the value replaces the property or is appended to it.</param>
/// <param name="Key">The property.</param>
/// <param name="Value">The new value, or for an append, a list of the new items.</param>
public readonly record struct PatchOp(int Widget, PatchCode Code, WidgetProp Key, UiValue Value)
{
  /// <summary>Reads one operation.</summary>
  /// <param name="reader">The reader positioned at the operation.</param>
  /// <returns>The operation.</returns>
  public static PatchOp Read(ref MessageReader reader)
  {
    var widget = reader.ReadI32();
    var code = (PatchCode)reader.ReadU8();
    var key = (WidgetProp)reader.ReadU8();
    return new PatchOp(widget, code, key, UiValue.Read(ref reader));
  }

  /// <summary>Writes this operation.</summary>
  /// <param name="writer">The writer.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Widget).WriteU8((byte)Code).WriteU8((byte)Key);
    Value.Write(writer);
  }
}

/// <summary>
/// Changes to the widgets of one panel, applied in order.
/// </summary>
/// <param name="Panel">The panel index from the latest <see cref="PanelList"/>.</param>
/// <param name="Ops">The operations.</param>
public sealed record PanelPatch(ushort Panel, IReadOnlyList<PatchOp> Ops) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.PanelPatch;

  /// <summary>Reads the body of the message.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The message.</returns>
  public static PanelPatch Read(ref MessageReader reader)
  {
    var panel = reader.ReadU16();
    return new PanelPatch(panel, reader.ReadArray(PatchOp.Read));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.PanelPatch).WriteU16(Panel)
        .WriteArray(Ops, static (w, op) => op.Write(w));
  }
}
