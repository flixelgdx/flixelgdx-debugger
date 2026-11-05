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

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Completion;

namespace FlixelDebugger.App.Views;

/// <summary>
/// The view of the console panel. It wires the keys and draws the suggestion line; every decision
/// is Core's.
/// </summary>
public sealed partial class ConsolePanelView : UserControl
{
  private ConsolePanelViewModel? _viewModel;

  /// <summary>
  /// Creates the view.
  /// </summary>
  public ConsolePanelView()
  {
    InitializeComponent();

    // Tunnel, so Tab is seen before the text box would use it to move focus.
    Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
  }

  /// <inheritdoc/>
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    _viewModel = DataContext as ConsolePanelViewModel;
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged += OnViewModelPropertyChanged;
      RenderSuggestion(_viewModel.Suggestion);
    }
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(ConsolePanelViewModel.Suggestion) && _viewModel is not null)
    {
      RenderSuggestion(_viewModel.Suggestion);
    }
  }

  private void OnInputKeyDown(object? sender, KeyEventArgs e)
  {
    if (_viewModel is null)
    {
      return;
    }

    switch (e.Key)
    {
      case Key.Tab:
        _viewModel.Tab(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        break;
      case Key.Up:
        _viewModel.HistoryUp();
        break;
      case Key.Down:
        _viewModel.HistoryDown();
        break;
      case Key.Enter:
        _viewModel.RunCommand.Execute(null);
        break;
      default:
        return;
    }

    // Handled keeps the focus in the box. The caret goes to the end of whatever text was put there.
    e.Handled = true;
    Input.CaretIndex = Input.Text?.Length ?? 0;
  }

  // Draws "usage  -  description", or the names with the picked one in bold accent color.
  private void RenderSuggestion(CompletionSuggestion suggestion)
  {
    var block = SuggestionText;
    block.Inlines?.Clear();
    block.Text = null;
    block.ClearValue(TextBlock.ForegroundProperty);
    var line = suggestion.Line;
    switch (suggestion.Kind)
    {
      case SuggestionKind.NoMatch:
        block.Text = line;
        if (this.TryFindResource("MutedTextBrush", ActualThemeVariant, out var muted) && muted is IBrush brush)
        {
          block.Foreground = brush;
        }

        break;
      case SuggestionKind.Names when suggestion.HighlightStart >= 0 && block.Inlines is { } inlines:
        var start = suggestion.HighlightStart;
        var end = start + suggestion.HighlightLength;
        inlines.Add(new Run(line[..start]));
        inlines.Add(new Run(line[start..end]) { FontWeight = FontWeight.Bold, Foreground = AccentBrush() });
        inlines.Add(new Run(line[end..]));
        break;
      default:
        block.Text = line;
        break;
    }
  }

  private IBrush AccentBrush() =>
      this.TryFindResource("SystemAccentColor", ActualThemeVariant, out var accent) && accent is Color color
          ? new SolidColorBrush(color)
          : Brushes.DodgerBlue;
}
