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

using System.Globalization;
using System.Text;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Data;

/// <summary>
/// Keeps the newest 500 log lines of one game and produces a filtered view of them.
/// </summary>
/// <remarks>
/// <para>
/// The store is a ring: when line 501 arrives, line 1 is dropped. Every line keeps the sequence number the game
/// gave it, so the UI can remember a selection by number instead of by position. If the selected line is
/// dropped, the selection clears by itself.
/// </para>
/// <para>
/// The game sends a <i>backlog</i> batch right after connecting, which replaces everything, and then normal
/// batches, which are appended. <see cref="Apply"/> understands both.
/// </para>
/// <para>
/// Filters never remove lines from the store, they only change the view. A line passes when its level is
/// switched on (or, for an uncaught exception, when <see cref="ShowUncaught"/> is on, whatever its level is)
/// and its summary contains <see cref="Search"/>, ignoring case.
/// </para>
/// <para>This class is thread safe: the receive loop writes while the UI reads.</para>
/// </remarks>
/// <example>
/// <code>
/// store.SetLevelShown(LogLevel.Debug, false);        // hide debug lines
/// store.Search = "audio";                            // only lines whose summary mentions audio
/// store.CopyView(reusableList);                      // refill a list the UI already owns
/// </code>
/// </example>
public sealed class LogStore
{
  /// <summary>The most lines the store keeps.</summary>
  public const int Capacity = 500;

  /// <summary>The tag of lines added with <see cref="AddLocal"/>.</summary>
  public const string LocalTag = "debugger";

  private const string LocalCallSite = "console";

  private readonly Lock _gate = new();
  private readonly LogEntry[] _ring = new LogEntry[Capacity];
  private readonly bool[] _levels = [true, true, true, true];
  private string _search = string.Empty;
  private long? _selectedSeq;
  private long _version;
  private long _localSeq;
  private int _head;
  private int _count;
  private bool _showUncaught = true;

  /// <summary>Gets the number of lines stored.</summary>
  public int Count
  {
    get
    {
      lock (_gate)
      {
        return _count;
      }
    }
  }

  /// <summary>Gets a number that changes whenever the lines, the filters, or the selection change.</summary>
  public long Version
  {
    get
    {
      lock (_gate)
      {
        return _version;
      }
    }
  }

  /// <summary>Gets or sets whether uncaught exceptions and stderr lines pass the filter.</summary>
  public bool ShowUncaught
  {
    get
    {
      lock (_gate)
      {
        return _showUncaught;
      }
    }

    set
    {
      lock (_gate)
      {
        _showUncaught = value;
        _version++;
      }
    }
  }

  /// <summary>Gets or sets the text a line summary must contain (ignoring case). Empty means no text filter.</summary>
  public string Search
  {
    get
    {
      lock (_gate)
      {
        return _search;
      }
    }

    set
    {
      lock (_gate)
      {
        _search = value ?? string.Empty;
        _version++;
      }
    }
  }

  /// <summary>Gets the sequence number of the selected line, or <see langword="null"/> if none is selected.</summary>
  public long? SelectedSeq
  {
    get
    {
      lock (_gate)
      {
        return _selectedSeq;
      }
    }
  }

  /// <summary>Gets the selected line, or <see langword="null"/> if none is selected.</summary>
  public LogEntry? SelectedEntry
  {
    get
    {
      lock (_gate)
      {
        return _selectedSeq is { } seq && TryFind(seq, out var entry) ? entry : null;
      }
    }
  }

  /// <summary>Gets the full details text of the selected line, or <see langword="null"/> if none is selected.</summary>
  public string? SelectedDetails => SelectedEntry is { } entry ? FormatDetails(entry.Line) : null;

  /// <summary>
  /// Builds the one-line summary of a log line: <c>[time] [LEVEL] [Tag] first line of message</c>.
  /// </summary>
  /// <param name="line">The line to describe.</param>
  /// <returns>The summary text. The time has three decimals, in seconds since the game started.</returns>
  public static string FormatSummary(LogLine line)
  {
    var message = line.Message;
    var end = message.AsSpan().IndexOfAny('\r', '\n');
    var first = end < 0 ? message : message[..end];
    return string.Create(
        CultureInfo.InvariantCulture,
        $"[{line.Time:0.000}] [{LevelName(line.Level)}] [{line.Tag}] {first}");
  }

  /// <summary>
  /// Builds the full details text of a log line: <c>[LEVEL] [Tag] [call site] full message</c>, followed by the stack trace.
  /// </summary>
  /// <param name="line">The line to describe.</param>
  /// <returns>The details text. The stack trace follows on its own lines when the line has one.</returns>
  public static string FormatDetails(LogLine line)
  {
    var text = new StringBuilder()
        .Append('[').Append(LevelName(line.Level)).Append("] [")
        .Append(line.Tag).Append("] [")
        .Append(line.CallSite).Append("] ")
        .Append(line.Message);
    if (line.Stack.Length > 0)
    {
      text.Append('\n').Append(line.Stack);
    }

    return text.ToString();
  }

  /// <summary>
  /// Gets the upper-case name of a log level, as shown in summaries.
  /// </summary>
  /// <param name="level">The level.</param>
  /// <returns><c>DEBUG</c>, <c>INFO</c>, <c>WARN</c>, <c>ERROR</c>, or the number for other values.</returns>
  public static string LevelName(LogLevel level) => level switch
  {
    LogLevel.Debug => "DEBUG",
    LogLevel.Info => "INFO",
    LogLevel.Warn => "WARN",
    LogLevel.Error => "ERROR",
    _ => ((byte)level).ToString(CultureInfo.InvariantCulture),
  };

  /// <summary>
  /// Applies a logs payload: a backlog batch replaces the contents and a normal batch appends.
  /// </summary>
  /// <param name="payload">The payload to apply.</param>
  public void Apply(LogsPayload payload)
  {
    ArgumentNullException.ThrowIfNull(payload);
    lock (_gate)
    {
      if (payload.Backlog)
      {
        _head = 0;
        _count = 0;
        Array.Clear(_ring);
      }

      foreach (var line in payload.Lines)
      {
        AddLocked(line);
      }

      if (payload.Backlog && _selectedSeq is { } seq && !TryFind(seq, out _))
      {
        _selectedSeq = null;
      }

      _version++;
    }
  }

  /// <summary>
  /// Appends one line, dropping the oldest when the store is full.
  /// </summary>
  /// <param name="line">The line to add.</param>
  public void Add(LogLine line)
  {
    lock (_gate)
    {
      AddLocked(line);
      _version++;
    }
  }

  /// <summary>
  /// Appends a line that the debugger itself produced, such as the echo of a console command.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Local lines live in the same ring as the game's lines, so they stay in the order they happened and the
  /// selection, the search, and the eviction rules work for them too. They get negative sequence numbers
  /// (-1, -2, ...), which never clash with the game's numbers, and <see cref="LogEntry.IsLocal"/> is
  /// <see langword="true"/> for them so a UI can draw them differently. They carry the time of the newest stored
  /// line, so they sort naturally between game lines.
  /// </para>
  /// <para>
  /// The level and uncaught filters never hide local lines, because the person typed the command and expects
  /// to see its echo. The text search still applies.
  /// </para>
  /// </remarks>
  /// <param name="message">The text to show.</param>
  /// <param name="level">The severity: <see cref="LogLevel.Info"/> for echoes and <see cref="LogLevel.Warn"/> for problems.</param>
  public void AddLocal(string message, LogLevel level = LogLevel.Info)
  {
    ArgumentNullException.ThrowIfNull(message);
    lock (_gate)
    {
      var time = _count > 0 ? _ring[(_head + _count - 1) % Capacity].Line.Time : 0.0;
      _localSeq--;
      AddLocked(new LogLine(_localSeq, time, level, false, LocalTag, LocalCallSite, message, string.Empty));
      _version++;
    }
  }

  /// <summary>
  /// Removes every line and clears the selection.
  /// </summary>
  public void Clear()
  {
    lock (_gate)
    {
      _head = 0;
      _count = 0;
      _selectedSeq = null;
      Array.Clear(_ring);
      _version++;
    }
  }

  /// <summary>
  /// Switches one log level on or off in the view.
  /// </summary>
  /// <param name="level">The level to change. Values other than the four real levels are ignored.</param>
  /// <param name="shown">Whether lines of that level pass the filter.</param>
  public void SetLevelShown(LogLevel level, bool shown)
  {
    var index = (int)level;
    if (index >= _levels.Length)
    {
      return;
    }

    lock (_gate)
    {
      _levels[index] = shown;
      _version++;
    }
  }

  /// <summary>
  /// Tells whether a log level is switched on in the view.
  /// </summary>
  /// <param name="level">The level to check.</param>
  /// <returns><see langword="true"/> if lines of that level pass the filter. Unknown levels always pass.</returns>
  public bool IsLevelShown(LogLevel level)
  {
    var index = (int)level;
    if (index >= _levels.Length)
    {
      return true;
    }

    lock (_gate)
    {
      return _levels[index];
    }
  }

  /// <summary>
  /// Selects a line by its sequence number.
  /// </summary>
  /// <param name="seq">The sequence number.</param>
  /// <returns><see langword="true"/> if the line exists and is now selected; otherwise the selection is cleared and the result is <see langword="false"/>.</returns>
  public bool Select(long seq)
  {
    lock (_gate)
    {
      var found = TryFind(seq, out _);
      _selectedSeq = found ? seq : null;
      _version++;
      return found;
    }
  }

  /// <summary>
  /// Clears the selection.
  /// </summary>
  public void ClearSelection()
  {
    lock (_gate)
    {
      _selectedSeq = null;
      _version++;
    }
  }

  /// <summary>
  /// Looks up a stored line by its sequence number.
  /// </summary>
  /// <param name="seq">The sequence number.</param>
  /// <param name="entry">The line, when found.</param>
  /// <returns><see langword="true"/> if the line is still stored.</returns>
  public bool TryGet(long seq, out LogEntry entry)
  {
    lock (_gate)
    {
      return TryFind(seq, out entry);
    }
  }

  /// <summary>
  /// Refills a list with the lines that pass the filters, oldest first.
  /// </summary>
  /// <remarks>
  /// Pass the same list every time and the view is rebuilt without allocating.
  /// </remarks>
  /// <param name="destination">The list to clear and fill.</param>
  public void CopyView(List<LogEntry> destination)
  {
    ArgumentNullException.ThrowIfNull(destination);
    destination.Clear();
    lock (_gate)
    {
      for (var i = 0; i < _count; i++)
      {
        var entry = _ring[(_head + i) % Capacity];
        if (Passes(entry))
        {
          destination.Add(entry);
        }
      }
    }
  }

  /// <summary>
  /// Returns the lines that pass the filters as a new list. This allocates; prefer <see cref="CopyView"/> for repeated use.
  /// </summary>
  /// <returns>The filtered lines, oldest first.</returns>
  public IReadOnlyList<LogEntry> GetView()
  {
    var view = new List<LogEntry>();
    CopyView(view);
    return view;
  }

  // The caller holds the lock.
  private void AddLocked(LogLine line)
  {
    if (_count == Capacity)
    {
      var evicted = _ring[_head].Seq;
      if (_selectedSeq == evicted)
      {
        _selectedSeq = null;
      }

      _ring[_head] = new LogEntry(line, FormatSummary(line));
      _head = (_head + 1) % Capacity;
    }
    else
    {
      _ring[(_head + _count) % Capacity] = new LogEntry(line, FormatSummary(line));
      _count++;
    }
  }

  // The caller holds the lock.
  private bool TryFind(long seq, out LogEntry entry)
  {
    for (var i = 0; i < _count; i++)
    {
      var candidate = _ring[(_head + i) % Capacity];
      if (candidate.Seq == seq)
      {
        entry = candidate;
        return true;
      }
    }

    entry = default;
    return false;
  }

  // The caller holds the lock.
  private bool Passes(LogEntry entry)
  {
    // Local lines are the echo of what the person typed, so only the text search can hide them.
    if (!entry.IsLocal && !PassesLevel(entry.Line))
    {
      return false;
    }

    return _search.Length == 0 || entry.Summary.Contains(_search, StringComparison.OrdinalIgnoreCase);
  }

  // The caller holds the lock.
  private bool PassesLevel(LogLine line)
  {
    if (line.Uncaught)
    {
      return _showUncaught;
    }

    var index = (int)line.Level;
    return index >= _levels.Length || _levels[index];
  }
}
