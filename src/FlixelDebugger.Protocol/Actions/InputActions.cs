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
/// Presses or releases a key in the game (input panel).
/// </summary>
/// <param name="Down"><see langword="true"/> to press, <see langword="false"/> to release.</param>
/// <param name="Key">The framework key code from 0 to 255.</param>
public sealed record InputKeyAction(bool Down, int Key) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Input.Key;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InputKeyAction Read(ref MessageReader reader)
  {
    var down = reader.ReadBool();
    return new InputKeyAction(down, reader.ReadI32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteBool(Down).WriteI32(Key);
  }
}

/// <summary>
/// Moves, presses, or releases a pointer in the game (input panel).
/// </summary>
/// <param name="Phase">0 moves, 1 presses, 2 releases.</param>
/// <param name="Button">0 left, 1 right, 2 middle, 3 and 4 the extra buttons.</param>
/// <param name="ViewX">The x position in the view space of the viewport panel's camera.</param>
/// <param name="ViewY">The y position in the view space of the viewport panel's camera.</param>
public sealed record InputPointerAction(byte Phase, byte Button, float ViewX, float ViewY)
    : ActionPayload
{
  /// <summary>The phase value that moves the pointer.</summary>
  public const byte Move = 0;

  /// <summary>The phase value that presses a button.</summary>
  public const byte Down = 1;

  /// <summary>The phase value that releases a button.</summary>
  public const byte Up = 2;

  /// <inheritdoc/>
  public override byte Code => A.Input.Pointer;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InputPointerAction Read(ref MessageReader reader)
  {
    var phase = reader.ReadU8();
    var button = reader.ReadU8();
    var x = reader.ReadF32();
    return new InputPointerAction(phase, button, x, reader.ReadF32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Phase).WriteU8(Button).WriteF32(ViewX).WriteF32(ViewY);
  }
}

/// <summary>
/// Turns the mouse wheel in the game (input panel).
/// </summary>
/// <param name="Amount">The scroll amount. Positive scrolls down.</param>
public sealed record InputScrollAction(float Amount) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Input.Scroll;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InputScrollAction Read(ref MessageReader reader) => new(reader.ReadF32());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteF32(Amount);
  }
}

/// <summary>
/// Types text in the game (input panel).
/// </summary>
/// <param name="Text">The text. The game cuts it at 1000 characters.</param>
public sealed record InputTextAction(string Text) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Input.Text;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InputTextAction Read(ref MessageReader reader) => new(reader.ReadString());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Text);
  }
}

/// <summary>
/// Clicks the center of an object's viewport box (input panel).
/// </summary>
/// <param name="Id">The object id from the viewport boxes or the scene tree.</param>
/// <param name="Button">The mouse button, 0 for left.</param>
public sealed record InputClickObjectAction(int Id, byte Button) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Input.ClickObject;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InputClickObjectAction Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    return new InputClickObjectAction(id, reader.ReadU8());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id).WriteU8(Button);
  }
}
