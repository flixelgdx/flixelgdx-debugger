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
/// The panels the game currently offers, in menu order. Sent after the handshake and again
/// whenever panels are registered, replaced, or removed.
/// </summary>
/// <remarks>
/// After every list the game treats every panel as closed, so the app answers with a
/// <see cref="PanelOpen"/> for each panel it shows.
/// </remarks>
/// <param name="Panels">The panels, in order.</param>
public sealed record PanelList(IReadOnlyList<PanelInfo> Panels) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.PanelList;

  /// <summary>Reads the body of a panel list.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The list.</returns>
  public static PanelList Read(ref MessageReader reader) => new(reader.ReadArray(PanelInfo.Read));

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.PanelList)
        .WriteArray(Panels, static (w, panel) => panel.Write(w));
  }
}
