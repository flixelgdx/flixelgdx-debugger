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

namespace FlixelDebugger.Protocol.Ui;

/// <summary>
/// The bits of the <c>STYLE</c>, <c>INTERACT</c>, and column flag values of widgets, and helpers to
/// read them.
/// </summary>
public static class UiStyle
{
  /// <summary>The bits that hold the <see cref="Tone"/>.</summary>
  public const int ToneMask = 0xF;

  /// <summary>Bold text.</summary>
  public const int Bold = 1 << 4;

  /// <summary>A monospaced font.</summary>
  public const int Mono = 1 << 5;

  /// <summary>A heading font.</summary>
  public const int Heading = 1 << 6;

  /// <summary>Italic text.</summary>
  public const int Italic = 1 << 7;

  /// <summary>Tree nodes only: the node starts expanded.</summary>
  public const int Expanded = 1 << 8;

  /// <summary>Canvas shapes: a thick outline.</summary>
  public const int ShapeSelected = 1 << 4;

  /// <summary>Canvas shapes: a dashed outline.</summary>
  public const int ShapeDashed = 1 << 5;

  /// <summary>Canvas shapes: dimmed.</summary>
  public const int ShapeDim = 1 << 6;

  /// <summary>Canvas shapes: filled.</summary>
  public const int ShapeFilled = 1 << 7;

  /// <summary>Canvas gesture: left click picks.</summary>
  public const int InteractPick = 1;

  /// <summary>Canvas gesture: left drag moves a shape.</summary>
  public const int InteractDrag = 2;

  /// <summary>Canvas gesture: right or middle drag pans.</summary>
  public const int InteractPan = 4;

  /// <summary>Canvas gesture: the wheel zooms.</summary>
  public const int InteractZoom = 8;

  /// <summary>Table column: the person can sort by it.</summary>
  public const int ColumnSortable = 1;

  /// <summary>Table column: aligned to the right.</summary>
  public const int ColumnRight = 2;

  /// <summary>Table column: a monospaced font.</summary>
  public const int ColumnMono = 4;

  /// <summary>Returns the tone of a style value.</summary>
  /// <param name="style">The style bits.</param>
  /// <returns>The tone; unknown tones count as <see cref="Tone.Normal"/>.</returns>
  public static Tone ToneOf(int style)
  {
    var tone = style & ToneMask;
    return tone <= (int)Tone.Error ? (Tone)tone : Tone.Normal;
  }

  /// <summary>Returns whether a flag is set.</summary>
  /// <param name="value">The bits.</param>
  /// <param name="flag">The flag to test.</param>
  /// <returns><see langword="true"/> when every bit of the flag is set.</returns>
  public static bool Has(int value, int flag) => (value & flag) == flag;
}
