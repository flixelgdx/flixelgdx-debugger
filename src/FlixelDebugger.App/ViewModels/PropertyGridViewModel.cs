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

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// A list of editable property rows that follows a list of rows from the game.
/// </summary>
/// <remarks>
/// <see cref="Update"/> changes the existing rows in place whenever the game's rows have the same names and kinds, so
/// controls keep their focus and the row being edited keeps its text. Only a row that really changed is replaced.
/// </remarks>
/// <param name="commit">Sends an edit: it receives the row's index and the new value.</param>
public sealed class PropertyGridViewModel(Func<int, PropertyValue, Task> commit)
{
    private readonly Func<int, PropertyValue, Task> _commit = commit ?? throw new ArgumentNullException(nameof(commit));

    /// <summary>Gets the rows in the order the game sent them.</summary>
    public RangeObservableCollection<PropertyRowViewModel> Rows { get; } = [];

    /// <summary>Gets whether the person is editing one of the rows.</summary>
    public bool IsEditing => Rows.Any(static r => r.IsEditing);

    /// <summary>
    /// Brings the rows in line with the game's rows.
    /// </summary>
    /// <param name="source">The rows from the game.</param>
    public void Update(IReadOnlyList<PropertyRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (var i = 0; i < source.Count; i++)
        {
            if (i >= Rows.Count)
            {
                Rows.Add(new PropertyRowViewModel(i, source[i], _commit));
            }
            else if (Rows[i].Matches(source[i]) || (Rows[i].IsEditing && Rows[i].Name == source[i].Name))
            {
                Rows[i].Update(source[i]);
            }
            else
            {
                Rows[i] = new PropertyRowViewModel(i, source[i], _commit);
            }
        }

        while (Rows.Count > source.Count)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }
    }
}
