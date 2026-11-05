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
using FlixelDebugger.Core.Data;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One row of the log list.
/// </summary>
/// <remarks>
/// The row only wraps a <see cref="LogEntry"/> and answers the questions the view asks (which color class, is it
/// selected). The text comes from Core, which built it once when the line arrived.
/// </remarks>
public sealed partial class LogRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Creates a row.
    /// </summary>
    /// <param name="entry">The stored line.</param>
    public LogRowViewModel(LogEntry entry)
    {
        Entry = entry;
    }

    /// <summary>Gets the stored line.</summary>
    public LogEntry Entry { get; }

    /// <summary>Gets the sequence number of the line.</summary>
    public long Seq => Entry.Seq;

    /// <summary>Gets the one-line text of the row.</summary>
    public string Summary => Entry.Summary;

    /// <summary>Gets whether the debugger itself produced the line (a console echo) instead of the game.</summary>
    public bool IsLocal => Entry.IsLocal;

    /// <summary>Gets whether the line is an uncaught exception or stderr output.</summary>
    public bool IsUncaught => Entry.Line.Uncaught;

    /// <summary>Gets whether the line is a debug line.</summary>
    public bool IsDebug => !IsUncaught && Entry.Line.Level == LogLevel.Debug;

    /// <summary>Gets whether the line is a warning.</summary>
    public bool IsWarn => !IsUncaught && Entry.Line.Level == LogLevel.Warn;

    /// <summary>Gets whether the line is an error.</summary>
    public bool IsError => !IsUncaught && Entry.Line.Level == LogLevel.Error;
}
