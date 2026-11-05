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
using Avalonia.Input;
using Avalonia.Interactivity;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Views;

/// <summary>
/// One row of a property grid: a name and a checkbox, a text box, or plain text, depending on the
/// row.
/// </summary>
/// <remarks>
/// The view only forwards focus and key events to the row's ViewModel, which decides what to send.
/// Focus starts an edit (so incoming values do not overwrite the typing), Enter or lost focus ends
/// it, and Escape throws the typing away.
/// </remarks>
public sealed partial class PropertyRowView : UserControl
{
  /// <summary>
  /// Creates the view.
  /// </summary>
  public PropertyRowView()
  {
    InitializeComponent();
    Editor.GotFocus += OnGotFocus;
    Editor.LostFocus += OnLostFocus;
    Editor.KeyDown += OnKeyDown;
  }

  private PropertyRowViewModel? Row => DataContext as PropertyRowViewModel;

  private void OnGotFocus(object? sender, FocusChangedEventArgs e) => Row?.BeginEdit();

  private void OnLostFocus(object? sender, RoutedEventArgs e)
  {
    if (Row is { } row)
    {
      _ = row.CommitAsync();
    }
  }

  private void OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (Row is not { } row)
    {
      return;
    }

    if (e.Key == Key.Enter)
    {
      // Commit, then keep editing: the person may press Enter and carry on typing.
      _ = row.CommitAsync();
      row.BeginEdit();
      e.Handled = true;
    }
    else if (e.Key == Key.Escape)
    {
      row.CancelEdit();
      row.BeginEdit();
      e.Handled = true;
    }
  }
}
