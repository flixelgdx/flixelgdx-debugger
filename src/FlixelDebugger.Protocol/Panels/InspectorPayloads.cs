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
/// The current selection of the <c>inspector</c> panel (kind 9, subtype 1).
/// </summary>
/// <param name="SelectedId">The selected object id, or -1 if nothing is selected.</param>
/// <param name="Path">The path of the selected object.</param>
/// <param name="Sections">The property sections, one per type in the object's hierarchy.</param>
/// <param name="HasTexture">
/// <see langword="true"/> if a thumbnail can be requested for the selection.
/// </param>
public sealed record InspectorStatePayload(
    int SelectedId,
    string Path,
    IReadOnlyList<InspectorSection> Sections,
    bool HasTexture) : PanelPayload
{
  /// <summary>
  /// Reads an inspector state payload.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the subtype byte.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorStatePayload Read(ref MessageReader reader)
  {
    var selectedId = reader.ReadI32();
    var path = reader.ReadString();
    var sections = reader.ReadArray(InspectorSection.Read);
    return new InspectorStatePayload(selectedId, path, sections, reader.ReadBool());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Subtypes.Inspector.State)
        .WriteI32(SelectedId)
        .WriteString(Path)
        .WriteArray(Sections, static (w, section) => section.Write(w))
        .WriteBool(HasTexture);
  }
}

/// <summary>
/// A group of properties that belong to one type of the selected object.
/// </summary>
/// <param name="TypeName">The type name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Properties">The properties of this section.</param>
public sealed record InspectorSection(string TypeName, string DisplayName, IReadOnlyList<PropertyRow> Properties)
{
  /// <summary>
  /// Reads one section.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the section.</param>
  /// <returns>The section that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorSection Read(ref MessageReader reader)
  {
    var typeName = reader.ReadString();
    var displayName = reader.ReadString();
    return new InspectorSection(typeName, displayName, reader.ReadArray(PropertyRow.Read));
  }

  /// <summary>
  /// Writes this section.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(TypeName)
        .WriteString(DisplayName)
        .WriteArray(Properties, static (w, row) => row.Write(w));
  }
}

/// <summary>
/// An object thumbnail from the <c>inspector</c> panel (kind 9, subtype 2).
/// </summary>
/// <param name="Id">The object id.</param>
/// <param name="Width">The thumbnail width in pixels.</param>
/// <param name="Height">The thumbnail height in pixels.</param>
/// <param name="Png">The PNG image bytes.</param>
public sealed record InspectorThumbnailPayload(int Id, int Width, int Height, ReadOnlyMemory<byte> Png) : PanelPayload
{
  /// <summary>
  /// Reads an inspector thumbnail payload.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the subtype byte.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InspectorThumbnailPayload Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    var width = reader.ReadI32();
    var height = reader.ReadI32();
    return new InspectorThumbnailPayload(id, width, height, reader.ReadBytes().ToArray());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Subtypes.Inspector.Thumbnail)
        .WriteI32(Id)
        .WriteI32(Width)
        .WriteI32(Height)
        .WriteBytes(Png.Span);
  }
}
