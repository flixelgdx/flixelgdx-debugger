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
/// The blocks of the <c>trackers</c> panel (kind 4). This panel has no actions.
/// </summary>
/// <param name="Blocks">The tracker blocks, each a named group of rows.</param>
public sealed record TrackersPayload(IReadOnlyList<TrackerBlock> Blocks) : PanelPayload
{
    /// <summary>
    /// Reads a trackers payload.
    /// </summary>
    /// <param name="reader">The reader, positioned at the first field.</param>
    /// <returns>The payload that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static TrackersPayload Read(ref MessageReader reader) => new(reader.ReadArray(TrackerBlock.Read));

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteArray(Blocks, static (w, block) => block.Write(w));
    }
}

/// <summary>
/// A named group of rows in the trackers panel.
/// </summary>
/// <param name="Name">The block name.</param>
/// <param name="Rows">The rows of this block.</param>
public sealed record TrackerBlock(string Name, IReadOnlyList<NamedValue> Rows)
{
    /// <summary>
    /// Reads one block.
    /// </summary>
    /// <param name="reader">The reader, positioned at the start of the block.</param>
    /// <returns>The block that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static TrackerBlock Read(ref MessageReader reader)
    {
        var name = reader.ReadString();
        return new TrackerBlock(name, reader.ReadArray(NamedValue.Read));
    }

    /// <summary>
    /// Writes this block.
    /// </summary>
    /// <param name="writer">The writer to append to.</param>
    public void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString(Name).WriteArray(Rows, static (w, row) => row.Write(w));
    }
}
