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

using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Data;

/// <summary>
/// One line of the save panel's table.
/// </summary>
/// <param name="Path">The raw dotted path, which is what an action must use.</param>
/// <param name="Label">The path as shown: unsaved values start with <c>* </c>.</param>
/// <param name="Display">The value as text, or <c>(removed)</c> for a removed path.</param>
/// <param name="Unsaved">Whether the value differs from the stored copy.</param>
/// <param name="Removed">Whether the path was removed and not yet saved.</param>
public readonly record struct SaveListRow(string Path, string Label, string Display, bool Unsaved, bool Removed);

/// <summary>
/// Turns a save payload into the rows of the save panel and applies the filter.
/// </summary>
public static class SaveListing
{
    /// <summary>The text shown in the value column of a removed path.</summary>
    public const string RemovedText = "(removed)";

    /// <summary>The prefix of the label of an unsaved value.</summary>
    public const string UnsavedPrefix = "* ";

    /// <summary>
    /// Builds the table rows: all values in the order the game sent them, then the removed paths.
    /// </summary>
    /// <param name="payload">The latest save payload, or <see langword="null"/> before the first one.</param>
    /// <param name="filter">Text to look for in the path, ignoring case. Empty text keeps every row.</param>
    /// <returns>The rows that match the filter.</returns>
    public static List<SaveListRow> Build(SavePayload? payload, string? filter)
    {
        var rows = new List<SaveListRow>();
        if (payload is null)
        {
            return rows;
        }

        var text = filter?.Trim() ?? string.Empty;
        foreach (var row in payload.Rows)
        {
            if (Matches(row.Path, text))
            {
                rows.Add(new SaveListRow(row.Path, row.Unsaved ? UnsavedPrefix + row.Path : row.Path, row.Display, row.Unsaved, false));
            }
        }

        foreach (var path in payload.Removed)
        {
            if (Matches(path, text))
            {
                rows.Add(new SaveListRow(path, path, RemovedText, false, true));
            }
        }

        return rows;
    }

    private static bool Matches(string path, string filter) =>
        filter.Length == 0 || path.Contains(filter, StringComparison.OrdinalIgnoreCase);
}
