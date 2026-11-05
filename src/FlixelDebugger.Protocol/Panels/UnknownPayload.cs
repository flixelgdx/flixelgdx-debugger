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
/// A panel payload this library does not understand, kept as raw bytes.
/// </summary>
/// <remarks>
/// This happens for panel kinds and subtypes added by a newer protocol minor version, and for panel
/// indices that are not in the last panel list. Readers must ignore such payloads.
/// </remarks>
/// <param name="Kind">
/// The panel kind, or <see langword="null"/> if the panel index was not known.
/// </param>
/// <param name="Raw">Every byte after the panel index (including the subtype byte, if any).</param>
public sealed record UnknownPayload(PanelKind? Kind, ReadOnlyMemory<byte> Raw) : PanelPayload
{
  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteRaw(Raw.Span);
  }
}
