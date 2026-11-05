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

namespace FlixelDebugger.Core.Adb;

/// <summary>
/// The state of a device as <c>adb devices</c> reports it.
/// </summary>
public enum AdbDeviceState
{
  /// <summary>The device is connected and ready (adb prints <c>device</c>).</summary>
  Ready,

  /// <summary>
  /// The device is waiting for the person to accept the USB debugging prompt on its screen.
  /// </summary>
  Unauthorized,

  /// <summary>The device is connected but not responding.</summary>
  Offline,

  /// <summary>
  /// The computer is not allowed to talk to the device (usually missing udev rules on Linux).
  /// </summary>
  NoPermissions,

  /// <summary>Any other state, such as <c>bootloader</c> or <c>recovery</c>.</summary>
  Other,
}

/// <summary>
/// One device from <c>adb devices -l</c>.
/// </summary>
/// <param name="Serial">
/// The serial number or address that identifies the device, for example <c>emulator-5554</c>.
/// </param>
/// <param name="State">The connection state.</param>
/// <param name="Model">The model name, if adb reported one.</param>
/// <param name="Product">The product name, if adb reported one.</param>
/// <param name="Description">
/// The raw state text adb printed, for example <c>device</c> or <c>unauthorized</c>.
/// </param>
public sealed record AdbDevice(
    string Serial,
    AdbDeviceState State,
    string? Model,
    string? Product,
    string Description)
{
  /// <summary>
  /// Gets a short name for lists: the model if known, otherwise the serial, plus the serial in
  /// brackets.
  /// </summary>
  public string DisplayName =>
      Model is { Length: > 0 } ? $"{Model.Replace('_', ' ')} ({Serial})" : Serial;
}
