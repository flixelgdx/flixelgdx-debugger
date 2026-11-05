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
/// Sets one save value from text (save panel).
/// </summary>
/// <param name="Path">The key path of the value.</param>
/// <param name="Text">The new value as text. The game parses it using the row's value kind.</param>
public sealed record SaveSetAction(string Path, string Text) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Save.Set;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static SaveSetAction Read(ref MessageReader reader)
  {
    var path = reader.ReadString();
    return new SaveSetAction(path, reader.ReadString());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Path).WriteString(Text);
  }
}

/// <summary>
/// Resets one save key to its default (save panel).
/// </summary>
/// <param name="Path">The key path to reset.</param>
public sealed record SaveResetKeyAction(string Path) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Save.ResetKey;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static SaveResetKeyAction Read(ref MessageReader reader) => new(reader.ReadString());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Path);
  }
}

/// <summary>
/// Writes unsaved changes now (save panel).
/// </summary>
public sealed record SaveSaveNowAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Save.SaveNow;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}

/// <summary>
/// Resets every save key (save panel).
/// </summary>
public sealed record SaveResetAllAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Save.ResetAll;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}

/// <summary>
/// Switches the save panel to another save (save panel).
/// </summary>
/// <remarks>
/// The game answers with the rows of the chosen save. An index past the end of the list is
/// ignored.
/// </remarks>
/// <param name="Index">The position of the save in <see cref="Panels.SavePayload.Saves"/>.</param>
public sealed record SaveSelectAction(ushort Index) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Save.Select;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static SaveSelectAction Read(ref MessageReader reader) => new(reader.ReadU16());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU16(Index);
  }
}
