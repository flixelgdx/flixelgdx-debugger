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

    private static Window? FindOwner() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
