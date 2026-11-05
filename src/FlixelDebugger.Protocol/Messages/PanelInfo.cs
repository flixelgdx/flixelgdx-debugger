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

namespace FlixelDebugger.Protocol.Messages;

/// <summary>
/// One entry of a <see cref="PanelList"/>: a panel that currently exists in the game.
/// </summary>
/// <param name="Index">The number all later messages use to refer to this panel.</param>
/// <param name="Id">A stable identifier such as <c>stats</c> or <c>custom.player</c>.</param>
/// <param name="Name">The display name.</param>
/// <param name="Kind">The panel kind, which decides how its payloads are encoded.</param>
public readonly record struct PanelInfo(ushort Index, string Id, string Name, PanelKind Kind)
{
    /// <summary>
    /// Reads one panel entry.
    /// </summary>
    /// <param name="reader">The reader, positioned at the start of the entry.</param>
    /// <returns>The entry that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static PanelInfo Read(ref MessageReader reader)
    {
        var index = reader.ReadU16();
        var id = reader.ReadString();
        var name = reader.ReadString();
        return new PanelInfo(index, id, name, (PanelKind)reader.ReadU8());
    }

    /// <summary>
    /// Writes this entry.
    /// </summary>
    /// <param name="writer">The writer to append to.</param>
    public void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU16(Index).WriteString(Id).WriteString(Name).WriteU8((byte)Kind);
    }
}
