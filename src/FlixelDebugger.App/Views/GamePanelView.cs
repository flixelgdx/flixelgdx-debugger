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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Widgets;

namespace FlixelDebugger.App.Views;

/// <summary>
/// Shows one <see cref="GamePanelViewModel"/>: its widget tree, or a short status line while there
/// is no tree.
/// </summary>
/// <remarks>
/// The view is built in code because its content is whatever the game describes. A
/// <see cref="WidgetFactory"/> turns each widget into a control, and the controls update
/// themselves when their widget changes, so this view only steps in when the panel gets a new root.
/// </remarks>
public sealed class GamePanelView : UserControl
{
  private readonly WidgetFactory _factory = new();
  private readonly Border _host = new() { Padding = new Thickness(6) };
  private readonly TextBlock _status = new()
  {
    HorizontalAlignment = HorizontalAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center,
    TextWrapping = TextWrapping.Wrap,
    Margin = new Thickness(12),
  };

  private GamePanelViewModel? _viewModel;
  private object? _shownRoot;

  /// <summary>Creates the view.</summary>
  public GamePanelView()
  {
    _status.Classes.Add("tone-muted");
    Content = _host;
  }

  /// <inheritdoc/>
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged -= OnViewModelChanged;
    }

    _viewModel = DataContext as GamePanelViewModel;
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged += OnViewModelChanged;
    }

    Show();
  }

  private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(GamePanelViewModel.Root) or nameof(GamePanelViewModel.StatusText))
    {
      Show();
    }
  }

  private void Show()
  {
    var root = _viewModel?.Root;
    if (root is not null && ReferenceEquals(root, _shownRoot))
    {
      return;
    }

    _shownRoot = root;
    _factory.Clear();
    if (root is not null)
    {
      _host.Child = _factory.Get(root);
    }
    else
    {
      _status.Text = _viewModel?.StatusText ?? string.Empty;
      _host.Child = _status;
    }
  }
}
