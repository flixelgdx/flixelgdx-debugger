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

using System.Text;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Completion;

/// <summary>
/// The brain of the console field: suggestions, Tab completion, command history, and submitting a
/// line.
/// </summary>
/// <remarks>
/// <para>
/// The class is pure logic. It does not touch a window, a session, or a clock, so every rule can be
/// unit tested. The UI passes the current text of the field in and puts the returned text back.
/// Think of it as a very polite assistant sitting next to the keyboard: it never types by itself,
/// it only tells you what it would type.
/// </para>
/// <para>
/// <b>Commands.</b> The command list is the game's commands plus the debugger's own (<c>clear</c>,
/// <c>show &lt;panel&gt;</c>, <c>close &lt;panel&gt;</c>, <c>layout.reset</c>). It is sorted by
/// name, ignoring case. A debugger command wins over a game command with the same name.
/// </para>
/// <para>
/// <b>Matching.</b> Only the first word of the line is matched, as a case-insensitive prefix.
/// </para>
/// <para>
/// <b>Tab.</b> Tab completes only the command name and does nothing once arguments have started.
/// One match is inserted with a trailing space. Several matches start a cycle through them; every
/// Tab (or Shift+Tab) moves to the next (or previous) name and wraps around. A cycle is active only
/// while the field still holds exactly the text the last Tab put there, so any edit ends it.
/// </para>
/// <para>This class is not thread safe; use it from the UI thread.</para>
/// </remarks>
/// <example>
/// <code>
/// var completer = new CommandCompleter();
/// completer.SetGameCommands(session.ConsoleCommands?.Commands);
/// var next = completer.Tab(field.Text);            // null means "do nothing"
/// if (next is not null) field.Text = next;
/// var hint = completer.GetSuggestion(field.Text);  // draw hint.Line under the field
/// </code>
/// </example>
public sealed class CommandCompleter
{
  /// <summary>The most command names shown in the suggestion line at once.</summary>
  public const int MaxVisibleNames = 6;

  /// <summary>The number of submitted lines remembered for Up and Down.</summary>
  public const int HistoryCapacity = 64;

  /// <summary>The longest line the game accepts, in characters.</summary>
  public const int MaxLineChars = ProtocolConstants.MaxCommandChars;

  private const string NameSeparator = "   ";

  private static readonly ConsoleCommand[] _localCommands =
  [
      new("clear", "clear", "Clears the console output."),
      new("show", "show <panel>", "Shows a panel by its id or name."),
      new("close", "close <panel>", "Hides a panel by its id or name."),
      new("layout.reset", "layout.reset", "Restores the default window layout."),
  ];

  private readonly List<string> _history = new(HistoryCapacity);
  private ConsoleCommand[] _commands = [];
  private ConsoleCommand[] _cycleMatches = [];
  private string? _cycleText;
  private int _cycleIndex;
  private int _windowStart;
  private int _historyPos;

  /// <summary>
  /// Creates a completer that knows only the debugger's own commands until
  /// <see cref="SetGameCommands"/> is called.
  /// </summary>
  public CommandCompleter()
  {
    SetGameCommands(null);
  }

  /// <summary>
  /// Gets the debugger's own commands: <c>clear</c>, <c>show</c>, <c>close</c>, and
  /// <c>layout.reset</c>.
  /// </summary>
  public static IReadOnlyList<ConsoleCommand> LocalCommands => _localCommands;

  /// <summary>
  /// Gets or sets the handler that runs the debugger's own commands. Without one, they do nothing.
  /// </summary>
  public LocalCommandHandler? LocalHandler { get; set; }

  /// <summary>Gets the merged command list (game and debugger commands), sorted by name.</summary>
  public IReadOnlyList<ConsoleCommand> Commands => _commands;

  /// <summary>Gets the remembered lines, oldest first.</summary>
  public IReadOnlyList<string> History => _history;

  /// <summary>
  /// Replaces the game's commands. The debugger's own commands are always added.
  /// </summary>
  /// <param name="gameCommands">
  /// The commands the game reported, or <see langword="null"/> for none.
  /// </param>
  public void SetGameCommands(IEnumerable<ConsoleCommand>? gameCommands)
  {
    var merged = new List<ConsoleCommand>(_localCommands);
    if (gameCommands is not null)
    {
      foreach (var command in gameCommands)
      {
        if (!merged.Exists(c => string.Equals(c.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
        {
          merged.Add(command);
        }
      }
    }

    merged.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
    _commands = [.. merged];
    EndCycle();
  }

  /// <summary>
  /// Finds the commands whose name starts with the first word of the text.
  /// </summary>
  /// <param name="text">The text of the field. An empty text matches every command.</param>
  /// <returns>The matching commands, sorted by name.</returns>
  public IReadOnlyList<ConsoleCommand> FindMatches(string text)
  {
    ParseText(text, out var word, out _);
    return FindByPrefix(word);
  }

  /// <summary>
  /// Tells whether a Tab cycle is active for the given field text.
  /// </summary>
  /// <param name="text">The current text of the field.</param>
  /// <returns>
  /// <see langword="true"/> while the text equals what the last Tab put into the field.
  /// </returns>
  public bool IsCycling(string text) => _cycleText is not null && string.Equals(text, _cycleText, StringComparison.Ordinal);

  /// <summary>
  /// Gets the position of the highlighted name in the full list of matches, so the UI can draw it
  /// in bold.
  /// </summary>
  /// <param name="text">The current text of the field.</param>
  /// <returns>The index, or -1 when no cycle is active.</returns>
  public int GetHighlightedIndex(string text) => IsCycling(text) ? _cycleIndex : -1;

  /// <summary>
  /// Builds the suggestion line for the current text of the field.
  /// </summary>
  /// <param name="text">The current text of the field.</param>
  /// <returns>
  /// A blank line when no word is typed; <c>usage  -  description</c> for exactly one match, or for
  /// any match once a space follows the word; the names joined by three spaces for several matches;
  /// and <c>No matching command</c> for no match.
  /// </returns>
  public CompletionSuggestion GetSuggestion(string text)
  {
    ArgumentNullException.ThrowIfNull(text);
    if (IsCycling(text))
    {
      return BuildNames(_cycleMatches, _cycleIndex, _windowStart);
    }

    ParseText(text, out var word, out var argsStarted);
    if (word.Length == 0)
    {
      return CompletionSuggestion.Blank;
    }

    var matches = FindByPrefix(word);
    if (matches.Length == 0)
    {
      return CompletionSuggestion.NoMatch;
    }

    if (matches.Length == 1 || argsStarted)
    {
      return BuildUsage(matches[0]);
    }

    return BuildNames(matches, -1, 0);
  }

  /// <summary>
  /// Handles the Tab key (or Shift+Tab) and returns the new text of the field.
  /// </summary>
  /// <param name="text">The current text of the field.</param>
  /// <param name="backward"><see langword="true"/> for Shift+Tab.</param>
  /// <returns>
  /// The text to put into the field, or <see langword="null"/> if nothing should happen.
  /// </returns>
  public string? Tab(string text, bool backward = false)
  {
    ArgumentNullException.ThrowIfNull(text);
    if (IsCycling(text))
    {
      var count = _cycleMatches.Length;
      _cycleIndex = backward ? (_cycleIndex - 1 + count) % count : (_cycleIndex + 1) % count;
      SlideWindow();
      _cycleText = _cycleMatches[_cycleIndex].Name;
      return _cycleText;
    }

    EndCycle();
    ParseText(text, out var word, out var argsStarted);
    if (argsStarted)
    {
      return null;
    }

    var matches = FindByPrefix(word);
    if (matches.Length == 0)
    {
      return null;
    }

    if (matches.Length == 1)
    {
      return matches[0].Name + " ";
    }

    _cycleMatches = matches;
    if (backward)
    {
      _cycleIndex = matches.Length - 1;
    }
    else
    {
      _cycleIndex = string.Equals(word, matches[0].Name, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    _windowStart = 0;
    SlideWindow();
    _cycleText = matches[_cycleIndex].Name;
    return _cycleText;
  }

  /// <summary>
  /// Moves to the previous (older) remembered line, for the Up key.
  /// </summary>
  /// <returns>
  /// The text to put into the field, or <see langword="null"/> if there is nothing older.
  /// </returns>
  public string? HistoryUp()
  {
    if (_historyPos == 0)
    {
      return null;
    }

    _historyPos--;
    return _history[_historyPos];
  }

  /// <summary>
  /// Moves to the next (newer) remembered line, for the Down key. After the newest line comes an
  /// empty line.
  /// </summary>
  /// <returns>
  /// The text to put into the field, or <see langword="null"/> if the field is already at the empty
  /// line.
  /// </returns>
  public string? HistoryDown()
  {
    if (_historyPos >= _history.Count)
    {
      return null;
    }

    _historyPos++;
    return _historyPos == _history.Count ? string.Empty : _history[_historyPos];
  }

  /// <summary>
  /// Submits a line: cleans it, remembers it, and either handles it locally or sends it to the
  /// game.
  /// </summary>
  /// <remarks>
  /// Tabs and line breaks are removed and the line is trimmed. An empty line is ignored. A line
  /// over <see cref="MaxLineChars"/> characters is rejected. A line that starts with one of the
  /// debugger's own commands goes to <see cref="LocalHandler"/>; everything else is sent with
  /// <see cref="ICommandRunner.RunCommandAsync"/>.
  /// </remarks>
  /// <param name="line">The text of the field.</param>
  /// <param name="runner">
  /// Where game commands go. Pass <see langword="null"/> when no game is connected.
  /// </param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>What happened, including the echo text <c>&gt; line</c>.</returns>
  public async Task<SubmitResult> SubmitAsync(string line, ICommandRunner? runner, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(line);
    var clean = Clean(line);
    _historyPos = _history.Count;
    if (clean.Length == 0)
    {
      return new SubmitResult(SubmitKind.Ignored, string.Empty, null, null, false);
    }

    if (clean.Length > MaxLineChars)
    {
      var message = $"The line is too long ({clean.Length} characters). The limit is {MaxLineChars}.";
      return new SubmitResult(SubmitKind.TooLong, clean, null, message, false);
    }

    Remember(clean);
    EndCycle();
    var echo = "> " + clean;
    SplitFirstWord(clean, out var word, out var arguments);
    var local = Array.Find(_localCommands, c => string.Equals(c.Name, word, StringComparison.OrdinalIgnoreCase));
    if (local.Name is not null)
    {
      var reply = LocalHandler?.Invoke(local.Name, arguments);
      return new SubmitResult(SubmitKind.Local, clean, echo, reply, false);
    }

    var sent = runner is not null && await runner.RunCommandAsync(clean, cancellationToken).ConfigureAwait(false);
    return new SubmitResult(SubmitKind.Run, clean, echo, sent ? null : "Not connected to a game.", sent);
  }

  private static string Clean(string line)
  {
    var builder = new StringBuilder(line.Length);
    foreach (var c in line)
    {
      if (c is not ('\t' or '\r' or '\n'))
      {
        builder.Append(c);
      }
    }

    return builder.ToString().Trim();
  }

  private static void SplitFirstWord(string text, out string word, out string rest)
  {
    var span = text.AsSpan();
    var space = span.IndexOfAny(' ', '\t');
    if (space < 0)
    {
      word = text;
      rest = string.Empty;
      return;
    }

    word = text[..space];
    rest = text[(space + 1)..].Trim();
  }

  // The word is the first word after leading whitespace. Arguments have started once any space
  // follows it.
  private static void ParseText(string text, out string word, out bool argsStarted)
  {
    ArgumentNullException.ThrowIfNull(text);
    var trimmed = text.TrimStart();
    var space = trimmed.AsSpan().IndexOfAny(' ', '\t');
    argsStarted = space >= 0;
    word = space < 0 ? trimmed : trimmed[..space];
  }

  private static CompletionSuggestion BuildUsage(ConsoleCommand command)
  {
    var line = command.Description.Length == 0 ? command.Usage : $"{command.Usage}  -  {command.Description}";
    return new CompletionSuggestion(SuggestionKind.Usage, line, [], 0, 0, -1, -1, 0);
  }

  private static CompletionSuggestion BuildNames(ConsoleCommand[] matches, int pick, int windowStart)
  {
    var count = matches.Length;
    var start = Math.Clamp(windowStart, 0, Math.Max(0, count - MaxVisibleNames));
    var end = Math.Min(start + MaxVisibleNames, count);
    var names = new string[end - start];
    var line = new StringBuilder();
    var highlightStart = -1;
    var highlightLength = 0;
    if (start > 0)
    {
      line.Append("(+").Append(start).Append(')').Append(NameSeparator);
    }

    for (var i = start; i < end; i++)
    {
      var name = matches[i].Name;
      names[i - start] = name;
      if (i > start)
      {
        line.Append(NameSeparator);
      }

      if (i == pick)
      {
        highlightStart = line.Length;
        highlightLength = name.Length;
      }

      line.Append(name);
    }

    if (end < count)
    {
      line.Append(NameSeparator).Append("(+").Append(count - end).Append(" more)");
    }

    var highlightIndex = pick >= start && pick < end ? pick - start : -1;
    return new CompletionSuggestion(
        SuggestionKind.Names, line.ToString(), names, start, count - end, highlightIndex, highlightStart, highlightLength);
  }

  private ConsoleCommand[] FindByPrefix(string word)
  {
    var found = new List<ConsoleCommand>();
    foreach (var command in _commands)
    {
      if (command.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase))
      {
        found.Add(command);
      }
    }

    return [.. found];
  }

  // Moves the window the least amount that keeps the picked name visible.
  private void SlideWindow()
  {
    var count = _cycleMatches.Length;
    var start = Math.Min(_windowStart, _cycleIndex);
    start = Math.Max(start, _cycleIndex - (MaxVisibleNames - 1));
    _windowStart = Math.Clamp(start, 0, Math.Max(0, count - MaxVisibleNames));
  }

  private void EndCycle()
  {
    _cycleText = null;
    _cycleMatches = [];
    _cycleIndex = 0;
    _windowStart = 0;
  }

  private void Remember(string line)
  {
    if (_history.Count > 0 && string.Equals(_history[^1], line, StringComparison.Ordinal))
    {
      _historyPos = _history.Count;
      return;
    }

    if (_history.Count == HistoryCapacity)
    {
      _history.RemoveAt(0);
    }

    _history.Add(line);
    _historyPos = _history.Count;
  }
}
