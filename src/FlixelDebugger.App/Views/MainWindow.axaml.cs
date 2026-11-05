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

using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Data;
using Avalonia.Input;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Views;

/// <summary>
/// The main window: the menus, the game list, the dock with all panels, and the status bar.
/// </summary>
/// <remarks>
/// The window only wires things the ViewModel cannot do: it creates the notification manager,
/// builds the check items of the Debug menu from the panel list, forwards the shortcut keys, and
/// tells the ViewModel when to start and when to save the layout.
/// </remarks>
public sealed partial class MainWindow : Window
{
  private readonly List<MenuItem> _panelItems = [];
  private MainWindowViewModel? _viewModel;
  private bool _saved;

  /// <summary>
  /// Creates the window.
  /// </summary>
  /// <param name="notifications">
  /// The service that shows toast messages; it is connected to this window.
  /// </param>
  public MainWindow(WindowNotificationService notifications)
  {
    ArgumentNullException.ThrowIfNull(notifications);
    InitializeComponent();
    notifications.Attach(new WindowNotificationManager(this)
    {
      Position = NotificationPosition.BottomRight,
      MaxItems = 3,
    });
    AddHandler(KeyDownEvent, OnShortcutKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
  }

  /// <summary>
  /// Creates the window without notifications, for the designer and tests.
  /// </summary>
  public MainWindow()
      : this(new WindowNotificationService()) { }

  /// <inheritdoc/>
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (_viewModel is not null)
    {
      _viewModel.PanelItems.CollectionChanged -= OnPanelItemsChanged;
    }

    _viewModel = DataContext as MainWindowViewModel;
    if (_viewModel is not null)
    {
      _viewModel.PanelItems.CollectionChanged += OnPanelItemsChanged;
      RebuildPanelMenu();
    }
  }

  /// <inheritdoc/>
  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);
    if (_viewModel is not null)
    {
      _ = _viewModel.InitializeAsync();
    }
  }

  /// <inheritdoc/>
  protected override void OnClosing(WindowClosingEventArgs e)
  {
    base.OnClosing(e);
    if (!_saved && !e.Cancel)
    {
      _saved = true;
      _viewModel?.SaveLayout();
    }
  }

  private void OnPanelItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
      RebuildPanelMenu();

  // The Debug menu lists a variable number of panels above its fixed items, so its check items are
  // built here.
  private void RebuildPanelMenu()
  {
    if (_viewModel is null)
    {
      return;
    }

    foreach (var old in _panelItems)
    {
      DebugMenu.Items.Remove(old);
    }

    _panelItems.Clear();
    var index = 0;
    foreach (var panel in _viewModel.PanelItems)
    {
      var item = new MenuItem { Header = panel.Title, ToggleType = MenuItemToggleType.CheckBox };
      item.Bind(
          MenuItem.IsCheckedProperty,
          new Binding(nameof(PanelMenuItemViewModel.IsVisible))
          {
            Mode = BindingMode.TwoWay,
            Source = panel,
          });
      DebugMenu.Items.Insert(index++, item);
      _panelItems.Add(item);
    }
  }

  // F2, F3, and F4 work anywhere in the window except while a text box has focus, so typing is
  // never hijacked.
  private void OnShortcutKey(object? sender, KeyEventArgs e)
  {
    if (e.Handled || e.KeyModifiers != KeyModifiers.None ||
        _viewModel is not { HasActiveSession: true } viewModel ||
        FocusManager?.GetFocusedElement() is TextBox)
    {
      return;
    }

    switch (e.Key)
    {
      case Key.F2:
        viewModel.ToggleHitboxesCommand.Execute(null);
        break;
      case Key.F3:
        viewModel.TogglePauseCommand.Execute(null);
        break;
      case Key.F4:
        viewModel.ToggleFreeCameraCommand.Execute(null);
        break;
      default:
        return;
    }

    e.Handled = true;
  }
}
