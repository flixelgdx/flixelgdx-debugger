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

using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// One row of a table widget, read from the game's <see cref="WidgetProp.Items"/> list.
/// </summary>
public sealed class TableRow : IEquatable<TableRow>
{
  private TableRow(long id, int style, string[] cells, UiValue[] sortKeys, string detail)
  {
    Id = id;
    Style = style;
    Cells = cells;
    SortKeys = sortKeys;
    Detail = detail;
  }

  /// <summary>Gets the row id the game uses in events.</summary>
  public long Id { get; }

  /// <summary>Gets the style bits of the whole row.</summary>
  public int Style { get; }

  /// <summary>Gets the text of every cell.</summary>
  public string[] Cells { get; }

  /// <summary>Gets the value every cell sorts by.</summary>
  public UiValue[] SortKeys { get; }

  /// <summary>Gets the extra text shown as a tooltip, or an empty string.</summary>
  public string Detail { get; }

  /// <summary>
  /// Reads a row: <c>[LONG id, INT style, LIST cells, STRING detail]</c>. A cell is any value, or
  /// <c>[STRING text, value sortKey]</c> to show one thing and sort by another.
  /// </summary>
  /// <param name="value">The row as the game sent it.</param>
  /// <returns>The row.</returns>
  public static TableRow Read(UiValue value)
  {
    ArgumentNullException.ThrowIfNull(value);
    var cells = value[2].Items;
    var texts = new string[cells.Count];
    var keys = new UiValue[cells.Count];
    for (var i = 0; i < cells.Count; i++)
    {
      var cell = cells[i];
      if (cell.Kind == ValueKind.List)
      {
        texts[i] = cell[0].ToDisplay();
        keys[i] = cell[1];
      }
      else
      {
        texts[i] = cell.ToDisplay();
        keys[i] = cell;
      }
    }

    return new TableRow(value[0].AsLong(-1), value[1].AsInt(), texts, keys, value[3].AsString());
  }

  /// <summary>
  /// Tells whether any cell contains a text, ignoring case.
  /// </summary>
  /// <param name="filter">The text to look for.</param>
  /// <returns><see langword="true"/> if a cell contains it.</returns>
  public bool Matches(string filter)
  {
    ArgumentNullException.ThrowIfNull(filter);
    foreach (var cell in Cells)
    {
      if (cell.Contains(filter, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  /// <inheritdoc/>
  public bool Equals(TableRow? other)
  {
    if (other is null || other.Id != Id || other.Style != Style ||
        !string.Equals(other.Detail, Detail, StringComparison.Ordinal) ||
        other.Cells.Length != Cells.Length)
    {
      return false;
    }

    for (var i = 0; i < Cells.Length; i++)
    {
      if (!string.Equals(other.Cells[i], Cells[i], StringComparison.Ordinal))
      {
        return false;
      }
    }

    return true;
  }

  /// <inheritdoc/>
  public override bool Equals(object? obj) => Equals(obj as TableRow);

  /// <inheritdoc/>
  public override int GetHashCode() => HashCode.Combine(Id, Style, Cells.Length);

  /// <summary>
  /// Orders rows by one column: numbers by value, everything else by text, ignoring case.
  /// </summary>
  /// <param name="column">The column index.</param>
  public sealed class Comparer(int column) : IComparer<TableRow>
  {
    /// <inheritdoc/>
    public int Compare(TableRow? x, TableRow? y)
    {
      if (x is null || y is null)
      {
        return x is null ? (y is null ? 0 : -1) : 1;
      }

      var a = column < x.SortKeys.Length ? x.SortKeys[column] : UiValue.Null;
      var b = column < y.SortKeys.Length ? y.SortKeys[column] : UiValue.Null;
      if (a.IsNumber && b.IsNumber)
      {
        return a.AsDouble().CompareTo(b.AsDouble());
      }

      return string.Compare(a.ToDisplay(), b.ToDisplay(), StringComparison.OrdinalIgnoreCase);
    }
  }
}
