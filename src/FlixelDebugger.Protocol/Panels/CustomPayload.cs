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

namespace FlixelDebugger.Protocol.Panels;

/// <summary>
/// The payload of a custom panel (kind 0), registered by game code.
/// </summary>
/// <param name="Rows">
/// The property rows. A row's position is the index used by the set-property action.
/// </param>
/// <param name="Buttons">The buttons the user can press.</param>
public sealed record CustomPayload(
    IReadOnlyList<PropertyRow> Rows, IReadOnlyList<CustomButton> Buttons) : PanelPayload
{
  /// <summary>
  /// Reads a custom panel payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static CustomPayload Read(ref MessageReader reader)
  {
    var rows = reader.ReadArray(PropertyRow.Read);
    return new CustomPayload(rows, reader.ReadArray(CustomButton.Read));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteArray(Rows, static (w, row) => row.Write(w))
        .WriteArray(Buttons, static (w, button) => button.Write(w));
  }
}

/// <summary>
/// A button on a custom panel.
/// </summary>
/// <param name="ActionIndex">The number to send back when the button is pressed.</param>
/// <param name="Label">The button text.</param>
public readonly record struct CustomButton(ushort ActionIndex, string Label)
{
  /// <summary>
  /// Reads one button.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the button.</param>
  /// <returns>The button that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static CustomButton Read(ref MessageReader reader)
  {
    var index = reader.ReadU16();
    return new CustomButton(index, reader.ReadString());
  }

  /// <summary>
  /// Writes this button.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU16(ActionIndex).WriteString(Label);
  }
}
