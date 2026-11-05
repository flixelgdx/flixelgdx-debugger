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

namespace FlixelDebugger.Core.Completion;

/// <summary>
/// What the suggestion line under the console field should show.
/// </summary>
public enum SuggestionKind
{
  /// <summary>Nothing is typed yet, so the line is blank.</summary>
  Blank,

  /// <summary>The usage and description of one command.</summary>
  Usage,

  /// <summary>A list of command names that match what was typed.</summary>
  Names,

  /// <summary>No command matches what was typed.</summary>
  NoMatch,
}

/// <summary>
/// The suggestion line under the console field, plus the details a UI needs to draw it nicely.
/// </summary>
/// <remarks>
/// <see cref="Line"/> is the complete plain text. The other members describe its parts, so a UI can
/// draw the highlighted name in bold: characters <see cref="HighlightStart"/> up to
/// <see cref="HighlightStart"/> + <see cref="HighlightLength"/> of <see cref="Line"/> are the
/// highlighted name.
/// </remarks>
/// <param name="Kind">The kind of line.</param>
/// <param name="Line">
/// The complete line text. It is empty for <see cref="SuggestionKind.Blank"/>.
/// </param>
/// <param name="Names">The command names that are visible in the window, in order.</param>
/// <param name="HiddenBefore">
/// How many names are cut off before the window; shown as <c>(+k)</c>.
/// </param>
/// <param name="HiddenAfter">
/// How many names are cut off after the window; shown as <c>(+k more)</c>.
/// </param>
/// <param name="HighlightIndex">
/// The position of the highlighted name inside <paramref name="Names"/>, or -1.
/// </param>
/// <param name="HighlightStart">
/// The character offset of the highlighted name in <paramref name="Line"/>, or -1.
/// </param>
/// <param name="HighlightLength">The length of the highlighted name, or 0.</param>
public sealed record CompletionSuggestion(
    SuggestionKind Kind,
    string Line,
    IReadOnlyList<string> Names,
    int HiddenBefore,
    int HiddenAfter,
    int HighlightIndex,
    int HighlightStart,
    int HighlightLength)
{
  /// <summary>The blank suggestion.</summary>
  public static CompletionSuggestion Blank { get; } = new(
      SuggestionKind.Blank, string.Empty, [], 0, 0, -1, -1, 0);

  /// <summary>The suggestion shown when no command matches.</summary>
  public static CompletionSuggestion NoMatch { get; } = new(
      SuggestionKind.NoMatch, "No matching command", [], 0, 0, -1, -1, 0);
}
