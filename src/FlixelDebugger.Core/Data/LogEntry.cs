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
/// One stored log line together with its one-line summary, which is built once when the line arrives.
/// </summary>
/// <param name="Line">The line as the game sent it.</param>
/// <param name="Summary">The one-line text <c>[time] [LEVEL] [Tag] first line of message</c>, used for display and filtering.</param>
public readonly record struct LogEntry(LogLine Line, string Summary)
{
    /// <summary>Gets the sequence number of the line.</summary>
    public long Seq => Line.Seq;

    /// <summary>Gets whether the debugger produced the line itself (see <see cref="LogStore.AddLocal"/>) instead of the game.</summary>
    public bool IsLocal => Line.Seq < 0;
}
