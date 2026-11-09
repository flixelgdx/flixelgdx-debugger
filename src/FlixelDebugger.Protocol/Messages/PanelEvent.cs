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
/// Something the person did with a widget, such as a click or a new value, sent to the game.
/// </summary>
/// <example>
/// <code>
/// var click = new PanelEvent(panelIndex, buttonId, WidgetEvent.Click, []);
/// var change = new PanelEvent(panelIndex, boxId, WidgetEvent.Change, [UiValue.FromBool(true)]);
/// </code>
/// </example>
/// <param name="Panel">The panel index from the latest <see cref="PanelList"/>.</param>
/// <param name="Widget">The id of the widget.</param>
/// <param name="Event">What happened.</param>
/// <param name="Args">The event's arguments, as <c>DEBUG_PROTOCOL.md</c> lists them.</param>
public sealed record PanelEvent(
    ushort Panel, int Widget, WidgetEvent Event, IReadOnlyList<UiValue> Args) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.PanelEvent;

  /// <summary>Reads the body of the message.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The message.</returns>
  public static PanelEvent Read(ref MessageReader reader)
  {
    var panel = reader.ReadU16();
    var widget = reader.ReadI32();
    var kind = (WidgetEvent)reader.ReadU8();
    return new PanelEvent(panel, widget, kind, reader.ReadArray(UiValue.Read));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.PanelEvent).WriteU16(Panel).WriteI32(Widget)
        .WriteU8((byte)Event).WriteArray(Args, static (w, arg) => arg.Write(w));
  }
}
