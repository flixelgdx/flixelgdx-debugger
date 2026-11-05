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

using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Adb;

namespace FlixelDebugger.App.Services;

/// <summary>
/// Opens the app's small dialog windows, which keeps windows out of the ViewModels.
/// </summary>
public interface IDialogService
{
  /// <summary>
  /// Shows the settings dialog and waits until it closes.
  /// </summary>
  /// <param name="settings">The ViewModel the dialog edits.</param>
  /// <returns><see langword="true"/> if the person chose Save.</returns>
  public Task<bool> ShowSettingsAsync(SettingsViewModel settings);

  /// <summary>
  /// Asks which Android device to use when several are connected.
  /// </summary>
  /// <param name="devices">The devices adb listed.</param>
  /// <returns>
  /// The serial of the chosen device, or <see langword="null"/> if the person cancelled.
  /// </returns>
  public Task<string?> PickDeviceAsync(IReadOnlyList<AdbDevice> devices);
}
