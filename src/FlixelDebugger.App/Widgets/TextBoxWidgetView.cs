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

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using FlixelDebugger.Core.Completion;
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Draws a <see cref="WidgetType.TextBox"/> widget: a text field that can also act as a command
/// line with history and Tab completion.
/// </summary>
/// <remarks>
/// <para>
/// A plain text box sends <see cref="WidgetEvent.Submit"/> on Enter. A <i>live</i> box also sends
/// <see cref="WidgetEvent.Change"/> while the person types, at most every 150 ms; whatever is still
/// waiting is sent before any other event of the panel, so the game never sees a button press
/// before the text that belongs to it.
/// </para>
/// <para>
/// When the game lists completions, the box behaves like the old console: the first word is
/// matched against the names, a hint line under the box shows the matches or the usage of a single
/// match, Tab completes, and Up and Down walk through the history. All of that is display work, so
/// the app does it without asking the game; the game only receives the submitted line.
/// </para>
/// </remarks>
public sealed class TextBoxWidgetView
{
  private static readonly TimeSpan _liveDelay = TimeSpan.FromMilliseconds(150);

  private readonly UiWidget _widget;
  private readonly TextBox _box = new();
  private readonly TextBlock _hint = new() { FontSize = 12, IsVisible = false };
  private readonly CommandCompleter _completer = new();
  private readonly DispatcherTimer _timer = new() { Interval = _liveDelay };
  private string? _sentText;
  private bool _updating;
  private bool _pending;

  /// <summary>
  /// Builds the control and binds it to the widget.
  /// </summary>
  /// <param name="widget">The text box widget.</param>
  public TextBoxWidgetView(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    _widget = widget;
    _hint.Classes.Add("tone-muted");
    _hint.Classes.Add("mono");
    var panel = new StackPanel { Spacing = 2 };
    panel.Children.Add(_box);
    panel.Children.Add(_hint);
    Root = panel;

    // Tunnel, so Tab is seen before the text box would use it to move the focus.
    _box.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    _box.TextChanged += (_, _) => OnTyped();
    _box.LostFocus += (_, _) => _ = FlushAsync();
    _timer.Tick += (_, _) => _ = FlushAsync();
    widget.Panel.SetFlusher(widget.Id, FlushAsync);
    widget.Changed += (_, key) => Apply(key);
    foreach (var key in new[]
    {
      WidgetProp.Text, WidgetProp.Placeholder, WidgetProp.Multiline, WidgetProp.ReadOnly,
      WidgetProp.History, WidgetProp.Completions,
    })
    {
      Apply(key);
    }
  }

  /// <summary>Gets the control to put in the panel.</summary>
  public Control Root { get; }

  private bool IsLive => _widget.GetBool(WidgetProp.Live);

  private bool IsCommandLine => _completer.Commands.Count > 0;

  private void Apply(WidgetProp key)
  {
    switch (key)
    {
      case WidgetProp.Text:
        if (string.Equals(_box.Text ?? string.Empty, _widget.GetString(WidgetProp.Text),
            StringComparison.Ordinal))
        {
          break;
        }

        // The game set the text: it replaces whatever the person typed.
        _updating = true;
        _box.Text = _widget.GetString(WidgetProp.Text);
        _sentText = _box.Text;
        _pending = false;
        _box.CaretIndex = _box.Text.Length;
        _updating = false;
        UpdateHint();
        break;
      case WidgetProp.Placeholder:
        _box.PlaceholderText = _widget.GetString(WidgetProp.Placeholder);
        break;
      case WidgetProp.Multiline:
        var multiline = _widget.GetBool(WidgetProp.Multiline);
        _box.AcceptsReturn = multiline;
        _box.TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
        break;
      case WidgetProp.ReadOnly:
        _box.IsReadOnly = _widget.GetBool(WidgetProp.ReadOnly);
        break;
      case WidgetProp.History:
        _completer.HistoryLimit = _widget.GetInt(WidgetProp.History);
        break;
      case WidgetProp.Completions:
        var items = _widget.Get(WidgetProp.Completions).Items;
        var commands = new ConsoleCommand[items.Count];
        for (var i = 0; i < commands.Length; i++)
        {
          var item = items[i];
          commands[i] = new ConsoleCommand(
              item[0].AsString(), item[1].AsString(), item[2].AsString());
        }

        _completer.SetCommands(commands);
        UpdateHint();
        break;
    }
  }

  private void OnTyped()
  {
    if (_updating)
    {
      return;
    }

    // The widget always holds what the box shows, so a table that filters by this box can read it.
    _updating = true;
    _widget.Set(WidgetProp.Text, UiValue.FromString(_box.Text ?? string.Empty));
    _updating = false;
    UpdateHint();
    if (IsLive)
    {
      _pending = true;
      _timer.Stop();
      _timer.Start();
    }
  }

  private async Task FlushAsync()
  {
    _timer.Stop();
    if (!_pending)
    {
      return;
    }

    _pending = false;
    var text = _box.Text ?? string.Empty;
    if (string.Equals(text, _sentText, StringComparison.Ordinal))
    {
      return;
    }

    _sentText = text;
    var value = UiValue.FromString(text);
    await _widget.Panel.SendRawAsync(_widget.Id, WidgetEvent.Change, [value]);
  }

  private void OnKeyDown(object? sender, KeyEventArgs e)
  {
    var text = _box.Text ?? string.Empty;
    string? replacement = null;
    switch (e.Key)
    {
      case Key.Enter when !_widget.GetBool(WidgetProp.Multiline):
        _ = SubmitAsync(text);
        e.Handled = true;
        return;
      case Key.Tab when IsCommandLine:
        replacement = _completer.Tab(text, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Handled = true;
        break;
      case Key.Up when _completer.HistoryLimit > 0 && !_widget.GetBool(WidgetProp.Multiline):
        replacement = _completer.HistoryUp();
        e.Handled = true;
        break;
      case Key.Down when _completer.HistoryLimit > 0 && !_widget.GetBool(WidgetProp.Multiline):
        replacement = _completer.HistoryDown();
        e.Handled = true;
        break;
      default:
        return;
    }

    if (replacement is not null)
    {
      _box.Text = replacement;
      _box.CaretIndex = replacement.Length;
    }
  }

  private async Task SubmitAsync(string text)
  {
    var line = _completer.Submit(text);
    await FlushAsync();
    if (_widget.GetBool(WidgetProp.ClearOnSubmit))
    {
      _updating = true;
      _box.Text = string.Empty;
      _sentText = string.Empty;
      _widget.Set(WidgetProp.Text, UiValue.FromString(string.Empty));
      _updating = false;
      UpdateHint();
    }

    // Only empty lines are skipped; the game decides what any other text means.
    if (line.Length > 0 || !IsCommandLine)
    {
      await _widget.Send(WidgetEvent.Submit, UiValue.FromString(IsCommandLine ? line : text));
    }
  }

  // Draws "usage  -  description", or the matching names with the picked one in bold.
  private void UpdateHint()
  {
    if (!IsCommandLine)
    {
      _hint.IsVisible = false;
      return;
    }

    var suggestion = _completer.GetSuggestion(_box.Text ?? string.Empty);
    _hint.IsVisible = suggestion.Kind != SuggestionKind.Blank;
    _hint.Inlines?.Clear();
    _hint.Text = null;
    var line = suggestion.Line;
    if (suggestion.Kind == SuggestionKind.Names && suggestion.HighlightStart >= 0 &&
        _hint.Inlines is { } inlines)
    {
      var start = suggestion.HighlightStart;
      var end = start + suggestion.HighlightLength;
      inlines.Add(new Run(line[..start]));
      inlines.Add(new Run(line[start..end]) { FontWeight = FontWeight.Bold });
      inlines.Add(new Run(line[end..]));
    }
    else
    {
      _hint.Text = line;
    }
  }
}
