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
/// Selects an object by id (inspector panel).
/// </summary>
/// <param name="Id">The object id.</param>
public sealed record InspectorSelectAction(int Id) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.Select;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorSelectAction Read(ref MessageReader reader) => new(reader.ReadI32());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id);
  }
}

/// <summary>
/// Selects an object by path (inspector panel).
/// </summary>
/// <param name="Path">The object path.</param>
public sealed record InspectorSelectPathAction(string Path) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.SelectPath;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorSelectPathAction Read(ref MessageReader reader) =>
      new(reader.ReadString());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Path);
  }
}

/// <summary>
/// Selects the parent of the current selection (inspector panel).
/// </summary>
public sealed record InspectorSelectParentAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.SelectParent;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}

/// <summary>
/// Clears the selection (inspector panel).
/// </summary>
public sealed record InspectorClearAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.Clear;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}

/// <summary>
/// Moves the game camera to the selection (inspector panel).
/// </summary>
public sealed record InspectorGoToAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.GoTo;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}

/// <summary>
/// Edits one property of an object (inspector panel).
/// </summary>
/// <param name="Id">The object id.</param>
/// <param name="SectionIndex">The position of the section in the state payload.</param>
/// <param name="PropertyIndex">The position of the property inside that section.</param>
/// <param name="Value">The new value. The app never sends <see cref="ValueKind.Error"/>.</param>
public sealed record InspectorSetPropertyAction(
    int Id, ushort SectionIndex, ushort PropertyIndex, PropertyValue Value) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.SetProperty;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorSetPropertyAction Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    var section = reader.ReadU16();
    var property = reader.ReadU16();
    return new InspectorSetPropertyAction(id, section, property, PropertyValue.Read(ref reader));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id).WriteU16(SectionIndex).WriteU16(PropertyIndex);
    Value.Write(writer);
  }
}

/// <summary>
/// Asks for a thumbnail of one object (inspector panel). The game answers with a thumbnail payload.
/// </summary>
/// <param name="Id">The object id.</param>
public sealed record InspectorRequestThumbnailAction(int Id) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Inspector.RequestThumbnail;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorRequestThumbnailAction Read(ref MessageReader reader) =>
      new(reader.ReadI32());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id);
  }
}
