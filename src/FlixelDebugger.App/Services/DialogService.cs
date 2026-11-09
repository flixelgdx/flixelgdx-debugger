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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.App.Views;
using FlixelDebugger.Core.Adb;

namespace FlixelDebugger.App.Services;

/// <summary>
/// The <see cref="IDialogService"/> of the real app: modal windows on top of the main window.
/// </summary>
public sealed class DialogService : IDialogService
{
  /// <inheritdoc/>
  public async Task<bool> ShowSettingsAsync(SettingsViewModel settings)
  {
    ArgumentNullException.ThrowIfNull(settings);
    var owner = FindOwner();
    if (owner is null)
    {
      return false;
    }

    var window = new SettingsWindow { DataContext = settings };
    settings.CloseRequested += (_, accepted) => window.Close(accepted);
    return await window.ShowDialog<bool>(owner);
  }

  /// <inheritdoc/>
  public async Task<string?> PickDeviceAsync(IReadOnlyList<AdbDevice> devices)
  {
    ArgumentNullException.ThrowIfNull(devices);
    var owner = FindOwner();
    if (owner is null)
    {
      return null;
    }

    var model = new DevicePickerViewModel(devices);
    var window = new DevicePickerWindow { DataContext = model };
    model.CloseRequested += (_, serial) => window.Close(serial);
    return await window.ShowDialog<string?>(owner);
  }

  /// <inheritdoc/>
  public async Task<bool> ConfirmAsync(string question)
  {
    ArgumentNullException.ThrowIfNull(question);
    var owner = FindOwner();
    if (owner is null)
    {
      return false;
    }

    var window = new Window
    {
      Title = "Please confirm",
      SizeToContent = SizeToContent.WidthAndHeight,
      CanResize = false,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      ShowInTaskbar = false,
    };
    var yes = new Button { Content = "Yes", IsDefault = true, MinWidth = 72 };
    var no = new Button { Content = "No", IsCancel = true, MinWidth = 72 };
    yes.Click += (_, _) => window.Close(true);
    no.Click += (_, _) => window.Close(false);
    var buttons = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      HorizontalAlignment = HorizontalAlignment.Right,
    };
    buttons.Children.Add(no);
    buttons.Children.Add(yes);
    var content = new StackPanel { Margin = new Thickness(20), Spacing = 16, MaxWidth = 420 };
    content.Children.Add(new TextBlock { Text = question, TextWrapping = TextWrapping.Wrap });
    content.Children.Add(buttons);
    window.Content = content;
    return await window.ShowDialog<bool>(owner);
  }

  private static Window? FindOwner() =>
      (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
          ?.MainWindow;
}
