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

using FlixelDebugger.Core.Adb;

namespace FlixelDebugger.Core.Tests;

public class AdbHelperTests
{
  [Fact]
  public void ParsesDevicesWithDetails()
  {
    const string Output =
        "List of devices attached\n" +
        "emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 " +
        "device:emu64xa transport_id:1\n" +
        "R58M12ABCDE            device usb:1-2 product:beyond1 model:SM_G973F device:beyond1 " +
        "transport_id:2\n" +
        "\n";

    var devices = AdbHelper.ParseDevices(Output);

    Assert.Equal(2, devices.Count);
    Assert.Equal("emulator-5554", devices[0].Serial);
    Assert.Equal(AdbDeviceState.Ready, devices[0].State);
    Assert.Equal("sdk_gphone64_x86_64", devices[0].Model);
    Assert.Equal("R58M12ABCDE", devices[1].Serial);
    Assert.Equal("SM_G973F", devices[1].Model);
    Assert.Equal("beyond1", devices[1].Product);
    Assert.Equal("SM G973F (R58M12ABCDE)", devices[1].DisplayName);
  }

  [Fact]
  public void ParsesUnauthorizedAndOfflineDevices()
  {
    const string Output =
        "List of devices attached\n" +
        "0123456789ABCDEF       unauthorized transport_id:3\n" +
        "192.168.1.20:5555      offline transport_id:4\n";

    var devices = AdbHelper.ParseDevices(Output);

    Assert.Equal(AdbDeviceState.Unauthorized, devices[0].State);
    Assert.Equal("unauthorized", devices[0].Description);
    Assert.Equal(AdbDeviceState.Offline, devices[1].State);
    Assert.Equal("192.168.1.20:5555", devices[1].Serial);
  }

  [Fact]
  public void ParsesTheMultiWordNoPermissionsState()
  {
    const string Output =
        "List of devices attached\n" +
        "0123456789ABCDEF no permissions (user in plugdev group; are your udev rules wrong?); " +
        "see [http://developer.android.com/tools/device.html] usb:1-1 transport_id:5\n";

    var device = Assert.Single(AdbHelper.ParseDevices(Output));

    Assert.Equal("0123456789ABCDEF", device.Serial);
    Assert.Equal(AdbDeviceState.NoPermissions, device.State);
  }

  [Fact]
  public void SkipsDaemonMessagesAndTheHeader()
  {
    const string Output =
        "* daemon not running; starting now at tcp:5037\n" +
        "* daemon started successfully\n" +
        "List of devices attached\n" +
        "abc123 device model:Pixel_7\r\n";

    var device = Assert.Single(AdbHelper.ParseDevices(Output));

    Assert.Equal("abc123", device.Serial);
    Assert.Equal("Pixel_7", device.Model);
  }

  [Fact]
  public void ParsesAnEmptyListAsNoDevices()
  {
    Assert.Empty(AdbHelper.ParseDevices("List of devices attached\n\n"));
    Assert.Empty(AdbHelper.ParseDevices(string.Empty));
  }

  [Fact]
  public void ParsesUnknownStatesAsOther()
  {
    var device = Assert.Single(
        AdbHelper.ParseDevices("List of devices attached\nabc recovery transport_id:1\n"));
    Assert.Equal(AdbDeviceState.Other, device.State);
    Assert.Equal("recovery", device.Description);
  }

  [Fact]
  public void FindsAdbInAndroidHomeFirst()
  {
    var env = new Dictionary<string, string>
    {
      ["ANDROID_HOME"] = "/sdk/home",
      ["ANDROID_SDK_ROOT"] = "/sdk/root",
      ["PATH"] = "/usr/bin:/opt/tools",
    };

    var found = AdbHelper.FindAdb(
        name => env.GetValueOrDefault(name),
        path => path is "/sdk/home/platform-tools/adb" or "/sdk/root/platform-tools/adb"
            or "/opt/tools/adb",
        windows: false);

    Assert.Equal(Path.Combine("/sdk/home", "platform-tools", "adb"), found);
  }

  [Fact]
  public void FallsBackToSdkRootThenToPath()
  {
    var env = new Dictionary<string, string>
    {
      ["ANDROID_HOME"] = "/sdk/home",
      ["ANDROID_SDK_ROOT"] = "/sdk/root",
      ["PATH"] = "/usr/bin:/opt/tools",
    };

    var fromRoot = AdbHelper.FindAdb(
        name => env.GetValueOrDefault(name),
        path => path.StartsWith("/sdk/root", StringComparison.Ordinal),
        windows: false);
    var fromPath = AdbHelper.FindAdb(
        name => env.GetValueOrDefault(name),
        path => path == Path.Combine("/opt/tools", "adb"),
        windows: false);

    Assert.Equal(Path.Combine("/sdk/root", "platform-tools", "adb"), fromRoot);
    Assert.Equal(Path.Combine("/opt/tools", "adb"), fromPath);
  }

  [Fact]
  public void LooksForAnExeOnWindows()
  {
    var env = new Dictionary<string, string> { ["ANDROID_HOME"] = "C:\\Android" };

    var found = AdbHelper.FindAdb(
        name => env.GetValueOrDefault(name),
        path => path.EndsWith("adb.exe", StringComparison.Ordinal),
        windows: true);

    Assert.NotNull(found);
    Assert.EndsWith("adb.exe", found, StringComparison.Ordinal);
  }

  [Fact]
  public void ReturnsNullWhenAdbIsNowhere()
  {
    Assert.Null(AdbHelper.FindAdb(_ => null, _ => false, windows: false));
  }

  [Fact]
  public async Task ReportsAFriendlyMessageWhenAdbIsMissing()
  {
    // The path points at nothing, so the process never starts and adb is never run.
    var helper = new AdbHelper("/definitely/not/here/adb", TimeSpan.FromSeconds(2));

    var devices = await helper.ListDevicesAsync(TestContext.Current.CancellationToken);
    var reverse = await helper.ReverseAsync(
        6510, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(AdbStatus.AdbNotFound, devices.Status);
    Assert.Contains("adb was not found", devices.Message, StringComparison.Ordinal);
    Assert.Equal(AdbStatus.AdbNotFound, reverse.Status);
  }
}
