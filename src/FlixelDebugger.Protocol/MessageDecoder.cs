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

using FlixelDebugger.Protocol.Messages;

namespace FlixelDebugger.Protocol;

/// <summary>
/// Turns received bytes into decoded message models.
/// </summary>
/// <remarks>
/// <para>
/// Panel payloads and actions only carry a panel index, and the index alone does not say how to
/// read the rest. The kind comes from the game's <c>PANEL_LIST</c> message, so a decoder instance
/// remembers the last list it decoded and uses it for every later message. Think of it as a
/// receptionist who keeps the latest staff directory and looks people up in it before sending mail
/// to them.
/// </para>
/// <para>
/// If you do not want that bookkeeping, use the static
/// <see cref="Decode(ReadOnlySpan{byte}, PanelKindLookup)"/> and supply your own lookup. Bytes
/// after the last field are ignored, and unknown message types, panel kinds, subtypes and action
/// codes decode to the <c>Unknown...</c> models instead of throwing, as the protocol requires.
/// Truncated input throws <see cref="ProtocolException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var decoder = new MessageDecoder();
/// foreach (var bytes in receivedMessages)
/// {
///   switch (decoder.Decode(bytes))
///   {
///     case Hello hello:
///       Console.WriteLine($"{hello.GameTitle} connected from {hello.Platform}");
///       break;
///     case PanelData { Payload: StatsPayload stats }:
///       Console.WriteLine($"{stats.Fps} FPS");
///       break;
///   }
/// }
/// </code>
/// </example>
public sealed class MessageDecoder
{
  private readonly Dictionary<ushort, PanelKind> _kinds = [];

  /// <summary>
  /// Gets the number of panels currently known from the last panel list.
  /// </summary>
  public int PanelCount => _kinds.Count;

  /// <summary>
  /// Decodes one whole message using the static overload and the given lookup.
  /// </summary>
  /// <param name="message">The received message, including the type byte.</param>
  /// <param name="kindOf">Finds the kind of a panel from its index.</param>
  /// <returns>The decoded message.</returns>
  /// <exception cref="ProtocolException">The message is empty or too short.</exception>
  public static ProtocolMessage Decode(ReadOnlySpan<byte> message, PanelKindLookup kindOf)
  {
    ArgumentNullException.ThrowIfNull(kindOf);
    var reader = new MessageReader(message);
    var type = reader.ReadU8();
    switch ((MessageType)type)
    {
      case MessageType.Hello:
        return Hello.Read(ref reader);
      case MessageType.HelloAck:
        return HelloAck.Read(ref reader);
      case MessageType.PanelList:
        return PanelList.Read(ref reader);
      case MessageType.PanelData:
        return PanelData.Read(ref reader, kindOf);
      case MessageType.PanelAction:
        return PanelAction.Read(ref reader, kindOf);
      case MessageType.Ping:
        return Ping.Read(ref reader);
      case MessageType.Pong:
        return Pong.Read(ref reader);
      default:
        return new UnknownMessage(type, reader.ReadRemaining().ToArray());
    }
  }

  /// <summary>
  /// Decodes one whole message, using the panel kinds remembered from the last panel list.
  /// </summary>
  /// <remarks>
  /// When the message is a <see cref="PanelList"/>, the remembered kinds are replaced by the new
  /// list.
  /// </remarks>
  /// <param name="message">The received message, including the type byte.</param>
  /// <returns>The decoded message.</returns>
  /// <exception cref="ProtocolException">The message is empty or too short.</exception>
  public ProtocolMessage Decode(ReadOnlySpan<byte> message)
  {
    var decoded = Decode(message, GetKind);
    if (decoded is PanelList list)
    {
      SetPanels(list.Panels);
    }

    return decoded;
  }

  /// <summary>
  /// Replaces the remembered panels, for example when decoding actions on the game's side of a
  /// test.
  /// </summary>
  /// <param name="panels">The panels to remember.</param>
  public void SetPanels(IEnumerable<PanelInfo> panels)
  {
    ArgumentNullException.ThrowIfNull(panels);
    _kinds.Clear();
    foreach (var panel in panels)
    {
      _kinds[panel.Index] = panel.Kind;
    }
  }

  /// <summary>
  /// Looks up the kind of a remembered panel.
  /// </summary>
  /// <param name="panelIndex">The panel index.</param>
  /// <returns>
  /// The panel kind, or <see langword="null"/> if no remembered panel has that index.
  /// </returns>
  public PanelKind? GetKind(ushort panelIndex) =>
      _kinds.TryGetValue(panelIndex, out var kind) ? kind : null;
}
