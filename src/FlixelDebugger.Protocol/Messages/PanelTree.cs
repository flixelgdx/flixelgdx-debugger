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
/// The whole widget tree of one panel. It replaces everything the app knew about the panel.
/// </summary>
/// <param name="Panel">The panel index from the latest <see cref="PanelList"/>.</param>
/// <param name="Root">The root widget.</param>
public sealed record PanelTree(ushort Panel, WidgetNode Root) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.PanelTree;

  /// <summary>Reads the body of the message.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The message.</returns>
  public static PanelTree Read(ref MessageReader reader)
  {
    var panel = reader.ReadU16();
    return new PanelTree(panel, WidgetNode.Read(ref reader));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.PanelTree).WriteU16(Panel);
    Root.Write(writer);
  }
}
