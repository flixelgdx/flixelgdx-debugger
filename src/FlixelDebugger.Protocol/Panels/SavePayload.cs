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
/// The state of the <c>save</c> panel (kind 10).
/// </summary>
/// <remarks>
/// A game may keep several saves (for example a settings file and a few profiles). The panel
/// edits one of them at a time: <see cref="Saves"/> lists every save the app can switch to, and
/// <see cref="Selected"/> tells which one the rows belong to. Games older than the save list do
/// not send those two fields; they decode as an empty <see cref="Saves"/> and a
/// <see cref="Selected"/> of 0, which means the rows belong to the game's only (main) save.
/// </remarks>
/// <param name="Rows">The save values.</param>
/// <param name="Removed">Paths that were removed and not yet saved.</param>
/// <param name="Status">A status line for the user.</param>
/// <param name="IsError">
/// <see langword="true"/> when <paramref name="Status"/> describes a failure.
/// </param>
/// <param name="Saves">
/// The label of every save the app can pick from. Entry 0 is always the game's main save. Empty
/// when the game did not send the list.
/// </param>
/// <param name="Selected">
/// The index in <paramref name="Saves"/> of the save the rows belong to.
/// </param>
public sealed record SavePayload(
    IReadOnlyList<SaveRow> Rows,
    IReadOnlyList<string> Removed,
    string Status,
    bool IsError,
    IReadOnlyList<string> Saves,
    ushort Selected) : PanelPayload
{
  /// <summary>
  /// Creates a payload without a save list, as games older than the save picker send it.
  /// </summary>
  /// <param name="rows">The save values.</param>
  /// <param name="removed">Paths that were removed and not yet saved.</param>
  /// <param name="status">A status line for the user.</param>
  /// <param name="isError"><see langword="true"/> when the status describes a failure.</param>
  public SavePayload(
      IReadOnlyList<SaveRow> rows, IReadOnlyList<string> removed, string status, bool isError)
      : this(rows, removed, status, isError, [], 0) { }

  /// <summary>
  /// Reads a save payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static SavePayload Read(ref MessageReader reader)
  {
    var rows = reader.ReadArray(SaveRow.Read);
    var removed = reader.ReadArray(static (ref MessageReader r) => r.ReadString());
    var status = reader.ReadString();
    var isError = reader.ReadBool();
    if (reader.Remaining == 0)
    {
      // An older game ends the payload here. Its rows belong to its one and only save.
      return new SavePayload(rows, removed, status, isError);
    }

    var saves = reader.ReadArray(static (ref MessageReader r) => r.ReadString());
    return new SavePayload(rows, removed, status, isError, saves, reader.ReadU16());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteArray(Rows, static (w, row) => row.Write(w))
        .WriteArray(Removed, static (w, path) => w.WriteString(path))
        .WriteString(Status)
        .WriteBool(IsError);

    // A payload without a save list is written the way an older game sends it, so decoding and
    // writing it again gives the same bytes.
    if (Saves.Count > 0 || Selected != 0)
    {
      writer.WriteArray(Saves, static (w, label) => w.WriteString(label)).WriteU16(Selected);
    }
  }
}

/// <summary>
/// One value in the save panel.
/// </summary>
/// <param name="Path">The key path of the value.</param>
/// <param name="Display">The value as text, ready to show.</param>
/// <param name="ValueKind">The kind the game uses to parse text typed for this row.</param>
/// <param name="Unsaved"><see langword="true"/> if the value changed and is not saved yet.</param>
public readonly record struct SaveRow(
    string Path, string Display, ValueKind ValueKind, bool Unsaved)
{
  /// <summary>
  /// Reads one save row.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the row.</param>
  /// <returns>The row that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static SaveRow Read(ref MessageReader reader)
  {
    var path = reader.ReadString();
    var display = reader.ReadString();
    var kind = (ValueKind)reader.ReadU8();
    return new SaveRow(path, display, kind, reader.ReadBool());
  }

  /// <summary>
  /// Writes this row.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Path).WriteString(Display).WriteU8((byte)ValueKind).WriteBool(Unsaved);
  }
}
