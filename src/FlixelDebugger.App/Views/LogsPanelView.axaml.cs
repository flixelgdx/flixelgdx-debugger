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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Views;

/// <summary>
/// The view of the log panel. It reports scrolling, forwards clicks on rows, and copies text; the
/// rules are in <see cref="LogsPanelViewModel"/>.
/// </summary>
public sealed partial class LogsPanelView : UserControl
{
  private LogsPanelViewModel? _viewModel;
  private LogRowViewModel? _contextRow;

  /// <summary>
  /// Creates the view.
  /// </summary>
  public LogsPanelView()
  {
    InitializeComponent();

    // Tunnel, so the click is seen before the list box turns it into its own selection.
    List.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel);
    List.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
  }

  /// <inheritdoc/>
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (_viewModel is not null)
    {
      _viewModel.ScrollToBottomRequested -= OnScrollToBottomRequested;
    }

    _viewModel = DataContext as LogsPanelViewModel;
    if (_viewModel is not null)
    {
      _viewModel.ScrollToBottomRequested += OnScrollToBottomRequested;
    }
  }

  private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    var row = (e.Source as StyledElement)?.DataContext as LogRowViewModel;
    var properties = e.GetCurrentPoint(List).Properties;
    if (properties.IsRightButtonPressed)
    {
      _contextRow = row;
    }
    else if (properties.IsLeftButtonPressed && row is not null && _viewModel is not null)
    {
      _viewModel.ToggleSelect(row);
      e.Handled = true;
    }
  }

  private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
  {
    // A longer list changes the extent but not the offset. That is not the person scrolling, so
    // ignore it.
    if (_viewModel is null || e.Source is not ScrollViewer scroller ||
        (e.OffsetDelta.Y == 0 && e.ExtentDelta.Y != 0))
    {
      return;
    }

    _viewModel.ReportScroll(scroller.Extent.Height - scroller.Viewport.Height - scroller.Offset.Y);
  }

  private void OnScrollToBottomRequested(object? sender, EventArgs e)
  {
    var count = _viewModel?.Rows.Count ?? 0;
    if (count > 0)
    {
      // Wait for the new rows to be laid out first.
      Dispatcher.UIThread.Post(() => List.ScrollIntoView(count - 1), DispatcherPriority.Background);
    }
  }

  private async void OnCopyLine(object? sender, RoutedEventArgs e) =>
      await CopyAsync(_viewModel?.GetLineText(_contextRow));

  private async void OnCopyDetails(object? sender, RoutedEventArgs e) =>
      await CopyAsync(_viewModel?.GetDetailsText(_contextRow));

  private async Task CopyAsync(string? text)
  {
    if (text is not null && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
    {
      await clipboard.SetTextAsync(text);
    }
  }
}
