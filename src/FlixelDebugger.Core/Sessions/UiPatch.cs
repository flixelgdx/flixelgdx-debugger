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

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// The shared rules for applying <c>APPEND</c> operations, so every reader of a panel keeps the
/// same
/// items.
/// </summary>
public static class UiPatch
{
  /// <summary>How many samples a graph keeps when the game sets no limit.</summary>
  public const int DefaultGraphLimit = 120;

  /// <summary>
  /// Returns the most items a widget keeps after an append: its <c>LIMIT</c>, or the graph default.
  /// </summary>
  /// <param name="type">The widget type.</param>
  /// <param name="props">The widget's current properties.</param>
  /// <returns>The limit, or 0 for no limit.</returns>
  public static int LimitOf(WidgetType type, IReadOnlyDictionary<WidgetProp, UiValue> props)
  {
    ArgumentNullException.ThrowIfNull(props);
    if (props.TryGetValue(WidgetProp.Limit, out var limit) && limit.IsNumber)
    {
      return Math.Max(0, limit.AsInt());
    }

    return type == WidgetType.Graph ? DefaultGraphLimit : 0;
  }

  /// <summary>
  /// Appends the items of a list to another list and drops items from the front beyond the limit.
  /// </summary>
  /// <param name="old">The current list, or <see langword="null"/> for none.</param>
  /// <param name="added">The list of new items.</param>
  /// <param name="limit">The most items to keep, or 0 for no limit.</param>
  /// <returns>The new list.</returns>
  public static UiValue Append(UiValue? old, UiValue added, int limit)
  {
    ArgumentNullException.ThrowIfNull(added);
    var current = old?.Kind == ValueKind.List ? old.Items : [];
    var total = current.Count + added.Count;
    var skip = limit > 0 && total > limit ? total - limit : 0;
    var items = new List<UiValue>(total - skip);
    for (var i = skip; i < current.Count; i++)
    {
      items.Add(current[i]);
    }

    for (var i = Math.Max(0, skip - current.Count); i < added.Count; i++)
    {
      items.Add(added.Items[i]);
    }

    return UiValue.FromList(items);
  }
}
