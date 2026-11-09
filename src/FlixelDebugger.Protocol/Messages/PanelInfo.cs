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
/// One entry of a <see cref="PanelList"/>: a panel the game built and wants the app to show.
/// </summary>
/// <param name="Index">The number all later panel messages use to refer to this panel.</param>
/// <param name="Id">A stable identifier such as <c>logs</c> or <c>game.enemies</c>.</param>
/// <param name="Title">The name on the panel's tab.</param>
/// <param name="Dock">Where the panel goes the first time the app sees its id.</param>
/// <param name="Shown">Whether the panel is shown the first time the app sees its id.</param>
public readonly record struct PanelInfo(
    ushort Index, string Id, string Title, PanelDock Dock, bool Shown)
{
  /// <summary>Reads one panel entry.</summary>
  /// <param name="reader">The reader positioned at the entry.</param>
  /// <returns>The entry.</returns>
  public static PanelInfo Read(ref MessageReader reader)
  {
    var index = reader.ReadU16();
    var id = reader.ReadString();
    var title = reader.ReadString();
    var dock = (PanelDock)reader.ReadU8();
    return new PanelInfo(index, id, title, dock, reader.ReadBool());
  }

  /// <summary>Writes this entry.</summary>
  /// <param name="writer">The writer.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU16(Index).WriteString(Id).WriteString(Title).WriteU8((byte)Dock)
        .WriteBool(Shown);
  }
}
