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
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Views;

/// <summary>
/// The view of the scene panel: a tree of the game's objects.
/// </summary>
/// <remarks>
/// When the game selects an object, the ViewModel selects and expands the matching node. A tree
/// only scrolls to a node when asked, so this view scrolls the selected item into view once its
/// container exists.
/// </remarks>
public sealed partial class ScenePanelView : UserControl
{
  private ScenePanelViewModel? _viewModel;

  /// <summary>
  /// Creates the view.
  /// </summary>
  public ScenePanelView()
  {
    InitializeComponent();
  }

  /// <inheritdoc/>
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged -= OnViewModelChanged;
    }

    _viewModel = DataContext as ScenePanelViewModel;
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged += OnViewModelChanged;
    }
  }

  private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(ScenePanelViewModel.SelectedNode) ||
        _viewModel?.SelectedNode is not { } node)
    {
      return;
    }

    // The containers of newly expanded ancestors are created in a later layout pass.
    Dispatcher.UIThread.Post(
        () =>
        {
          foreach (var item in Tree.GetVisualDescendants().OfType<TreeViewItem>())
          {
            if (ReferenceEquals(item.DataContext, node))
            {
              item.BringIntoView();
              return;
            }
          }
        },
        DispatcherPriority.Background);
  }
}
