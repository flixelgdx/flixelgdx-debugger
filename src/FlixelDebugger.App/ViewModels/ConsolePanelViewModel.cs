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
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Completion;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The ViewModel of the console panel: a text field with suggestions, Tab completion, and history.
/// </summary>
/// <remarks>
/// <para>
/// <b>All logic is Core's.</b> <see cref="CommandCompleter"/> decides what to suggest, what Tab
/// inserts, what Up and Down recall, and whether a line runs in the game or locally. This class
/// only passes the text of the field in, puts the returned text back, and prints the results.
/// </para>
/// <para>
/// <b>Output.</b> What a game command prints arrives through the log panel. The echo
/// (<c>&gt; line</c>) and the debugger's own replies are added to the active game's log as <i>local
/// lines</i> (<see cref="LogStore.AddLocal"/>), which the log panel draws in italics.
/// </para>
/// <para>
/// <b>Local commands.</b> <c>clear</c> empties the log view, <c>show &lt;panel&gt;</c> and
/// <c>close &lt;panel&gt;</c> show or hide a panel by id or title, and <c>layout.reset</c> restores
/// the default arrangement.
/// </para>
/// </remarks>
public sealed partial class ConsolePanelViewModel : PanelViewModel
{
  /// <summary>The hint line above the text field.</summary>
  public const string HintText = "Type a command. Tab completes, Up/Down history, Enter runs.";

  private readonly CommandCompleter _completer = new();
  private readonly IUiScheduler _scheduler;
  private readonly IPanelRegistry _registry;
  private readonly IPanelLayout _layout;
  private IGameSession? _subscribed;
  private ConsoleResultPayload? _lastResult;
  private LogLevel _replyLevel = LogLevel.Info;
  private bool _echoSuppressed;

  [ObservableProperty]
  private string _text = string.Empty;

  [ObservableProperty]
  private CompletionSuggestion _suggestion = CompletionSuggestion.Blank;

  /// <summary>
  /// Creates the console.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Moves work from the server's threads to the UI thread.</param>
  /// <param name="registry">Resolves the panel names used by <c>show</c> and <c>close</c>.</param>
  /// <param name="layout">Shows and hides panels and resets the layout.</param>
  public ConsolePanelViewModel(IActiveSession active, IUiScheduler scheduler, IPanelRegistry registry, IPanelLayout layout)
      : base(BuiltInPanels.Console, "Console", active)
  {
    ArgumentNullException.ThrowIfNull(scheduler);
    ArgumentNullException.ThrowIfNull(registry);
    ArgumentNullException.ThrowIfNull(layout);
    _scheduler = scheduler;
    _registry = registry;
    _layout = layout;
    _completer.LocalHandler = HandleLocal;
    Subscribe(active.Current);
  }

  /// <summary>
  /// Handles the Tab key (or Shift+Tab).
  /// </summary>
  /// <param name="backward"><see langword="true"/> for Shift+Tab.</param>
  /// <returns>
  /// <see langword="true"/> if the text changed. The view keeps focus in the field either way.
  /// </returns>
  public bool Tab(bool backward)
  {
    var next = _completer.Tab(Text, backward);
    if (next is null)
    {
      return false;
    }

    Text = next;
    return true;
  }

  /// <summary>
  /// Recalls the previous line from the history (the Up key).
  /// </summary>
  /// <returns><see langword="true"/> if the text changed.</returns>
  public bool HistoryUp()
  {
    var line = _completer.HistoryUp();
    if (line is null)
    {
      return false;
    }

    Text = line;
    return true;
  }

  /// <summary>
  /// Recalls the next line from the history (the Down key), or an empty line after the newest.
  /// </summary>
  /// <returns><see langword="true"/> if the text changed.</returns>
  public bool HistoryDown()
  {
    var line = _completer.HistoryDown();
    if (line is null)
    {
      return false;
    }

    Text = line;
    return true;
  }

  /// <summary>
  /// Submits the text of the field: runs it, clears the field, and prints the result.
  /// </summary>
  /// <remarks>
  /// A line that is too long is not sent and stays in the field so it can be shortened.
  /// </remarks>
  /// <returns>A task that completes when the line was handled.</returns>
  [RelayCommand]
  public async Task RunAsync()
  {
    var line = Text;
    var session = Session;
    _echoSuppressed = false;
    _replyLevel = LogLevel.Info;
    var result = await _completer.SubmitAsync(line, session);
    if (result.Kind == SubmitKind.Ignored)
    {
      Text = string.Empty;
      return;
    }

    if (result.Kind != SubmitKind.TooLong)
    {
      Text = string.Empty;
    }

    // The session may have changed while the line was being sent, so print to the one that ran it.
    var log = session?.Logs;
    if (!_echoSuppressed && result.Echo is not null)
    {
      log?.AddLocal(result.Echo);
    }

    if (result.Message is { Length: > 0 } message)
    {
      var level = result.Kind == SubmitKind.Local ? _replyLevel : LogLevel.Warn;
      log?.AddLocal(message, level);
    }
  }

  /// <inheritdoc/>
  protected override void OnSessionChanged(IGameSession? oldSession, IGameSession? newSession) => Subscribe(newSession);

  partial void OnTextChanged(string value) => Suggestion = _completer.GetSuggestion(value);

  private string? HandleLocal(string name, string arguments)
  {
    switch (name)
    {
      case "clear":
        Session?.Logs.Clear();
        _echoSuppressed = true;
        return null;
      case "layout.reset":
        _layout.Reset();
        return "The layout was reset.";
      default:
        return ShowOrClose(name == "show", arguments);
    }
  }

  private string ShowOrClose(bool show, string arguments)
  {
    if (arguments.Length == 0)
    {
      _replyLevel = LogLevel.Warn;
      return show ? "Usage: show <panel>" : "Usage: close <panel>";
    }

    if (!_registry.TryFind(arguments, out var panel))
    {
      _replyLevel = LogLevel.Warn;
      return $"Unknown panel '{arguments}'. Known panels: {string.Join(", ", _registry.Definitions.Select(d => d.Id))}.";
    }

    _layout.SetVisible(panel.Id, show);
    return show ? $"Showing {panel.Title}." : $"Hiding {panel.Title}.";
  }

  private void Subscribe(IGameSession? session)
  {
    if (_subscribed is { } old)
    {
      old.PanelUpdated -= OnPanelUpdated;
    }

    _subscribed = session;
    _lastResult = session?.ConsoleResult;
    if (session is not null)
    {
      session.PanelUpdated += OnPanelUpdated;
    }

    _completer.SetGameCommands(session?.ConsoleCommands?.Commands);
    Suggestion = _completer.GetSuggestion(Text);
  }

  private void OnPanelUpdated(object? sender, SessionPanelEventArgs e)
  {
    if (e.Kind != PanelKind.Console || sender is not IGameSession session)
    {
      return;
    }

    // Events arrive on the server's thread, but the completer and the controls belong to the UI
    // thread.
    _scheduler.Post(() =>
    {
      if (!ReferenceEquals(session, _subscribed))
      {
        return;
      }

      _completer.SetGameCommands(session.ConsoleCommands?.Commands);
      Suggestion = _completer.GetSuggestion(Text);
      var result = session.ConsoleResult;
      if (result is not null && !ReferenceEquals(result, _lastResult))
      {
        _lastResult = result;
        if (result.Status is ConsoleStatus.Failed or ConsoleStatus.UnknownCommand)
        {
          session.Logs.AddLocal(result.Message, LogLevel.Warn);
        }
      }
    });
  }
}
