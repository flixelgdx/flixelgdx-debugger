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

namespace FlixelDebugger.App.Tests;

public class DevicePickerViewModelTests
{
    [Fact]
    public void ThePickReturnsTheSerialAndCancelReturnsNull()
    {
        AdbDevice[] devices =
        [
            new("a1", AdbDeviceState.Ready, "Pixel", null, "device"),
            new("b2", AdbDeviceState.Ready, null, null, "device"),
        ];
        var picker = new DevicePickerViewModel(devices);
        var results = new List<string?>();
        picker.CloseRequested += (_, serial) => results.Add(serial);

        picker.SelectCommand.Execute(null);
        picker.SelectedDevice = devices[1];
        picker.SelectCommand.Execute(null);
        picker.CancelCommand.Execute(null);

        Assert.Equal(["a1", "b2", null], results);
    }
}
