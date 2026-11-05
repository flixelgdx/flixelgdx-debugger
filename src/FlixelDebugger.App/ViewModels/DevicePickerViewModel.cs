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
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.Core.Adb;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The ViewModel of the dialog that asks which Android device to use.
/// </summary>
public sealed partial class DevicePickerViewModel : ObservableObject
{
  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
  private AdbDevice? _selectedDevice;

  /// <summary>
  /// Creates the dialog state.
  /// </summary>
  /// <param name="devices">The devices to choose from.</param>
  public DevicePickerViewModel(IReadOnlyList<AdbDevice> devices)
  {
    ArgumentNullException.ThrowIfNull(devices);
    Devices = devices;
    _selectedDevice = devices.Count > 0 ? devices[0] : null;
  }

  /// <summary>
  /// Raised when the dialog should close. The argument is the chosen serial, or
  /// <see langword="null"/> for Cancel.
  /// </summary>
  public event EventHandler<string?>? CloseRequested;

  /// <summary>Gets the devices to choose from.</summary>
  public IReadOnlyList<AdbDevice> Devices { get; }

  [RelayCommand(CanExecute = nameof(CanSelect))]
  private void Select() => CloseRequested?.Invoke(this, SelectedDevice?.Serial);

  [RelayCommand]
  private void Cancel() => CloseRequested?.Invoke(this, null);

  private bool CanSelect() => SelectedDevice is not null;
}
