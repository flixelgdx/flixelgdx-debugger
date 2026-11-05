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
/// The list of loaded textures from the <c>textures</c> panel (kind 5, subtype 1).
/// </summary>
/// <param name="Entries">One entry per texture.</param>
/// <param name="TextureCount">The total number of textures.</param>
/// <param name="TotalGpuBytes">The total GPU memory used by all textures, in bytes.</param>
/// <param name="DuplicateCount">How many textures were loaded more than once.</param>
public sealed record TexturesListPayload(
    IReadOnlyList<TextureEntry> Entries,
    int TextureCount,
    long TotalGpuBytes,
    int DuplicateCount) : PanelPayload
{
    /// <summary>
    /// Reads a textures list payload.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the subtype byte.</param>
    /// <returns>The payload that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static TexturesListPayload Read(ref MessageReader reader)
    {
        var entries = reader.ReadArray(TextureEntry.Read);
        var count = reader.ReadI32();
        var bytes = reader.ReadI64();
        return new TexturesListPayload(entries, count, bytes, reader.ReadI32());
    }

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU8(Subtypes.Textures.List)
            .WriteArray(Entries, static (w, entry) => entry.Write(w))
            .WriteI32(TextureCount)
            .WriteI64(TotalGpuBytes)
            .WriteI32(DuplicateCount);
    }
}

/// <summary>
/// One loaded texture in a <see cref="TexturesListPayload"/>.
/// </summary>
/// <param name="Id">The texture id, used to request a thumbnail.</param>
/// <param name="Name">The texture name or path.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Format">The pixel format, for example <c>RGBA8</c>.</param>
/// <param name="GpuBytes">The GPU memory used, in bytes.</param>
/// <param name="Users">How many objects use this texture.</param>
/// <param name="Duplicate"><see langword="true"/> if the same file was loaded more than once.</param>
public readonly record struct TextureEntry(
    int Id,
    string Name,
    int Width,
    int Height,
    string Format,
    long GpuBytes,
    int Users,
    bool Duplicate)
{
    /// <summary>
    /// Reads one texture entry.
    /// </summary>
    /// <param name="reader">The reader, positioned at the start of the entry.</param>
    /// <returns>The entry that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static TextureEntry Read(ref MessageReader reader)
    {
        var id = reader.ReadI32();
        var name = reader.ReadString();
        var width = reader.ReadI32();
        var height = reader.ReadI32();
        var format = reader.ReadString();
        var gpuBytes = reader.ReadI64();
        var users = reader.ReadI32();
        return new TextureEntry(id, name, width, height, format, gpuBytes, users, reader.ReadBool());
    }

    /// <summary>
    /// Writes this entry.
    /// </summary>
    /// <param name="writer">The writer to append to.</param>
    public void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteI32(Id)
            .WriteString(Name)
            .WriteI32(Width)
            .WriteI32(Height)
            .WriteString(Format)
            .WriteI64(GpuBytes)
            .WriteI32(Users)
            .WriteBool(Duplicate);
    }
}

/// <summary>
/// A texture thumbnail from the <c>textures</c> panel (kind 5, subtype 2).
/// </summary>
/// <param name="Id">The texture id.</param>
/// <param name="Width">The thumbnail width in pixels.</param>
/// <param name="Height">The thumbnail height in pixels.</param>
/// <param name="Png">The PNG image bytes.</param>
public sealed record TexturesThumbnailPayload(int Id, int Width, int Height, ReadOnlyMemory<byte> Png) : PanelPayload
{
    /// <summary>
    /// Reads a textures thumbnail payload.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the subtype byte.</param>
    /// <returns>The payload that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static TexturesThumbnailPayload Read(ref MessageReader reader)
    {
        var id = reader.ReadI32();
        var width = reader.ReadI32();
        var height = reader.ReadI32();
        return new TexturesThumbnailPayload(id, width, height, reader.ReadBytes().ToArray());
    }

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU8(Subtypes.Textures.Thumbnail)
            .WriteI32(Id)
            .WriteI32(Width)
            .WriteI32(Height)
            .WriteBytes(Png.Span);
    }
}
