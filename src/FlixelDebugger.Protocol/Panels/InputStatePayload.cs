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
/// What kinds of input the game accepts, from the <c>input</c> panel (kind 13).
/// </summary>
/// <remarks>
/// Sent on connect and whenever it changes. The panel has a single payload, so there is no
/// subtype byte.
/// </remarks>
/// <param name="Flags">The raw support bits, see <see cref="InputSupport"/>.</param>
public sealed record InputStatePayload(InputSupport Flags) : PanelPayload
{
  /// <summary>Gets a value indicating whether key actions work.</summary>
  public bool Key => Flags.HasFlag(InputSupport.Key);

  /// <summary>Gets a value indicating whether pointer and click-object actions work.</summary>
  public bool Pointer => Flags.HasFlag(InputSupport.Pointer);

  /// <summary>Gets a value indicating whether scroll actions work.</summary>
  public bool Scroll => Flags.HasFlag(InputSupport.Scroll);

  /// <summary>Gets a value indicating whether text actions work.</summary>
  public bool Text => Flags.HasFlag(InputSupport.Text);

  /// <summary>Gets a value indicating whether touch input is on.</summary>
  public bool Touch => Flags.HasFlag(InputSupport.Touch);

  /// <summary>
  /// Reads an input state payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the flags byte.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static InputStatePayload Read(ref MessageReader reader) =>
      new((InputSupport)reader.ReadU8());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8((byte)Flags);
  }
}

/// <summary>
/// The support bits of an <see cref="InputStatePayload"/>.
/// </summary>
[Flags]
public enum InputSupport : byte
{
  /// <summary>No input works.</summary>
  None = 0,

  /// <summary>Key actions work.</summary>
  Key = 1,

  /// <summary>Pointer and click-object actions work.</summary>
  Pointer = 2,

  /// <summary>Scroll actions work.</summary>
  Scroll = 4,

  /// <summary>Text actions work.</summary>
  Text = 8,

  /// <summary>Touch input is on.</summary>
  Touch = 16,
}
