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
/// A request from the game to the app: show or hide a panel, reset the layout, or show a message.
/// </summary>
/// <param name="Request">What the game asks for.</param>
/// <param name="PanelId">
/// The panel id of <c>SHOW_PANEL</c> and <c>HIDE_PANEL</c>, otherwise empty.
/// </param>
/// <param name="Tone">The tone of a <c>NOTIFY</c> message.</param>
/// <param name="Text">The text of a <c>NOTIFY</c> message, otherwise empty.</param>
public sealed record AppRequest(
    AppRequestKind Request, string PanelId, Tone Tone, string Text) : ProtocolMessage
{
  /// <inheritdoc/>
  public override MessageType Type => MessageType.AppRequest;

  /// <summary>Reads the body of the message. Unknown requests keep only their kind.</summary>
  /// <param name="reader">The reader positioned after the message type.</param>
  /// <returns>The message.</returns>
  public static AppRequest Read(ref MessageReader reader)
  {
    var request = (AppRequestKind)reader.ReadU8();
    return request switch
    {
      AppRequestKind.ShowPanel or AppRequestKind.HidePanel =>
          new AppRequest(request, reader.ReadString(), Tone.Normal, string.Empty),
      AppRequestKind.Notify => ReadNotify(ref reader),
      _ => new AppRequest(request, string.Empty, Tone.Normal, string.Empty),
    };
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.BeginMessage(MessageType.AppRequest).WriteU8((byte)Request);
    switch (Request)
    {
      case AppRequestKind.ShowPanel:
      case AppRequestKind.HidePanel:
        writer.WriteString(PanelId);
        break;
      case AppRequestKind.Notify:
        writer.WriteU8((byte)Tone).WriteString(Text);
        break;
      default:
        break;
    }
  }

  private static AppRequest ReadNotify(ref MessageReader reader)
  {
    var tone = (Tone)reader.ReadU8();
    return new AppRequest(AppRequestKind.Notify, string.Empty, tone, reader.ReadString());
  }
}
