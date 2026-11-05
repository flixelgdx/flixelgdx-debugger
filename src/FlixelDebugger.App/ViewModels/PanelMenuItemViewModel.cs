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

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One check item of the Debug menu: a panel that can be shown or hidden.
/// </summary>
public sealed partial class PanelMenuItemViewModel : ObservableObject
{
  private readonly Action<string, bool> _apply;
  private bool _syncing;

  [ObservableProperty]
  private bool _isVisible;

  /// <summary>
  /// Creates a menu item.
  /// </summary>
  /// <param name="id">The panel id.</param>
  /// <param name="title">The text of the menu item.</param>
  /// <param name="visible">Whether the panel is visible now.</param>
  /// <param name="apply">Called when the person checks or unchecks the item, with the panel id and the new state.</param>
  public PanelMenuItemViewModel(string id, string title, bool visible, Action<string, bool> apply)
  {
    Id = id;
    Title = title;
    _apply = apply;
    _isVisible = visible;
  }

  /// <summary>Gets the panel id.</summary>
  public string Id { get; }

  /// <summary>Gets the text of the menu item.</summary>
  public string Title { get; }

  /// <summary>
  /// Updates the check mark without telling the layout, because the layout is where the change came from.
  /// </summary>
  /// <param name="visible">Whether the panel is visible.</param>
  public void SyncFromLayout(bool visible)
  {
    _syncing = true;
    IsVisible = visible;
    _syncing = false;
  }

  partial void OnIsVisibleChanged(bool value)
  {
    if (!_syncing)
    {
      _apply(Id, value);
    }
  }
}
