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

using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One read-only row of a name and value table: a stats row, a watch, or a tracker row.
/// </summary>
public sealed partial class ValueRowViewModel : ObservableObject
{
  [ObservableProperty]
  private string _name = string.Empty;

  [ObservableProperty]
  private string _value = string.Empty;

  [ObservableProperty]
  private bool _isError;

  [ObservableProperty]
  private bool _isGood;

  [ObservableProperty]
  private bool _isWarning;

  /// <summary>
  /// Sets the row from a protocol value, formatted with the shared rules (see
  /// <see cref="ValueFormat"/>).
  /// </summary>
  /// <param name="name">The label.</param>
  /// <param name="value">The value. An error value shows as red <c>error: message</c>.</param>
  public void Set(string name, PropertyValue value)
  {
    Name = name;
    Value = ValueFormat.Display(value);
    IsError = ValueFormat.IsError(value);
  }

  /// <summary>
  /// Sets the row from a stats entry.
  /// </summary>
  /// <param name="entry">The entry to show.</param>
  public void Set(StatsEntry entry)
  {
    Name = entry.Name;
    Value = entry.Value;
    IsGood = entry.Tone == StatsTone.Good;
    IsWarning = entry.Tone == StatsTone.Warning;
  }

  /// <summary>
  /// Brings a list of rows in line with a list of named values, changing as little as possible.
  /// </summary>
  /// <param name="rows">The rows to update.</param>
  /// <param name="source">The values the rows should show, in order.</param>
  public static void Sync(RangeObservableCollection<ValueRowViewModel> rows, IReadOnlyList<NamedValue> source)
  {
    ArgumentNullException.ThrowIfNull(rows);
    ArgumentNullException.ThrowIfNull(source);
    for (var i = 0; i < source.Count; i++)
    {
      if (i >= rows.Count)
      {
        rows.Add(new ValueRowViewModel());
      }

      rows[i].Set(source[i].Name, source[i].Value);
    }

    while (rows.Count > source.Count)
    {
      rows.RemoveAt(rows.Count - 1);
    }
  }
}
