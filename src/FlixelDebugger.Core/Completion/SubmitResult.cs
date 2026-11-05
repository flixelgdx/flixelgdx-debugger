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
/// Tells what happened to a line the person submitted in the console.
/// </summary>
public enum SubmitKind
{
  /// <summary>The line was empty, so nothing happened.</summary>
  Ignored,

  /// <summary>The line is longer than the game accepts, so it was not sent.</summary>
  TooLong,

  /// <summary>The line is a debugger command and was given to the local handler.</summary>
  Local,

  /// <summary>The line is a game command and was sent to the game with a RUN action.</summary>
  Run,
}

/// <summary>
/// The outcome of submitting a console line.
/// </summary>
/// <param name="Kind">What happened to the line.</param>
/// <param name="Line">The cleaned line: tabs and line breaks removed, then trimmed. Empty for <see cref="SubmitKind.Ignored"/>.</param>
/// <param name="Echo">The text to print in the console, <c>&gt; line</c>, or <see langword="null"/> when nothing should be echoed.</param>
/// <param name="Message">An extra line to print: the local handler's reply, or an error. It may be <see langword="null"/>.</param>
/// <param name="Sent">For <see cref="SubmitKind.Run"/>: whether the line really went to a game.</param>
public sealed record SubmitResult(SubmitKind Kind, string Line, string? Echo, string? Message, bool Sent);
