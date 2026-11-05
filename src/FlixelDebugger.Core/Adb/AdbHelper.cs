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
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace FlixelDebugger.Core.Adb;

/// <summary>
/// Finds and runs <c>adb</c>, so an Android device can reach the debugger over USB.
/// </summary>
/// <remarks>
/// <para>
/// An Android phone cannot reach <c>127.0.0.1</c> on your PC: that address means "this device" on the phone.
/// <c>adb reverse tcp:6510 tcp:6510</c> tells the phone to forward its own port 6510 to the PC, so the game can
/// keep connecting to <c>127.0.0.1:6510</c>. This class runs that command and explains in plain words why it
/// might not work (no adb, no device, or a device that has not accepted the USB debugging prompt).
/// </para>
/// <para>
/// adb is started directly with an argument list and never through a shell, so no text can be misread as extra
/// commands. Every call has a timeout.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var adb = new AdbHelper();
/// var result = await adb.ReverseAsync(6510);
/// Console.WriteLine(result.Message);
/// </code>
/// </example>
public sealed class AdbHelper
{
  /// <summary>How long one adb call may take by default. The first call can start the adb server, which is slow.</summary>
  public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

  private readonly string? _adbPath;
  private readonly TimeSpan _timeout;

  /// <summary>
  /// Creates a helper that looks for adb in the usual places.
  /// </summary>
  public AdbHelper()
      : this(null, DefaultTimeout)
  {
  }

  /// <summary>
  /// Creates a helper.
  /// </summary>
  /// <param name="adbPath">The full path of the adb program, or <see langword="null"/> to find it on every call.</param>
  /// <param name="timeout">How long one call may take. Pass <see langword="null"/> for <see cref="DefaultTimeout"/>.</param>
  public AdbHelper(string? adbPath, TimeSpan? timeout)
  {
    _adbPath = adbPath;
    _timeout = timeout ?? DefaultTimeout;
  }

  /// <summary>
  /// Finds the adb program: first in <c>ANDROID_HOME</c>, then in <c>ANDROID_SDK_ROOT</c>, then on the <c>PATH</c>.
  /// </summary>
  /// <returns>The full path of adb, or <see langword="null"/> if it cannot be found.</returns>
  public static string? FindAdb() => FindAdb(Environment.GetEnvironmentVariable, File.Exists, OperatingSystem.IsWindows());

  /// <summary>
  /// Finds the adb program using the given environment and file lookups.
  /// </summary>
  /// <param name="getVariable">Reads an environment variable, returning <see langword="null"/> when it is not set.</param>
  /// <param name="fileExists">Tells whether a file exists.</param>
  /// <param name="windows">Whether to look for <c>adb.exe</c> and split the <c>PATH</c> with <c>;</c>.</param>
  /// <returns>The full path of adb, or <see langword="null"/> if it cannot be found.</returns>
  public static string? FindAdb(Func<string, string?> getVariable, Func<string, bool> fileExists, bool windows)
  {
    ArgumentNullException.ThrowIfNull(getVariable);
    ArgumentNullException.ThrowIfNull(fileExists);
    var fileName = windows ? "adb.exe" : "adb";
    foreach (var variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
    {
      var root = getVariable(variable);
      if (string.IsNullOrWhiteSpace(root))
      {
        continue;
      }

      var candidate = Path.Combine(root, "platform-tools", fileName);
      if (fileExists(candidate))
      {
        return candidate;
      }
    }

    var path = getVariable("PATH");
    if (string.IsNullOrEmpty(path))
    {
      return null;
    }

    foreach (var folder in path.Split(windows ? ';' : ':', StringSplitOptions.RemoveEmptyEntries))
    {
      var candidate = Path.Combine(folder.Trim('"'), fileName);
      if (fileExists(candidate))
      {
        return candidate;
      }
    }

    return null;
  }

  /// <summary>
  /// Parses the output of <c>adb devices -l</c>.
  /// </summary>
  /// <remarks>
  /// Lines before the <c>List of devices attached</c> header and daemon messages that start with <c>*</c> are skipped.
  /// </remarks>
  /// <param name="output">The text adb printed.</param>
  /// <returns>The devices, in the order adb listed them.</returns>
  public static IReadOnlyList<AdbDevice> ParseDevices(string output)
  {
    ArgumentNullException.ThrowIfNull(output);
    var devices = new List<AdbDevice>();
    foreach (var rawLine in output.Split('\n'))
    {
      var line = rawLine.Trim();
      if (line.Length == 0 || line[0] == '*' || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length < 2)
      {
        continue;
      }

      string? model = null;
      string? product = null;
      var stateText = parts[1];
      for (var i = 2; i < parts.Length; i++)
      {
        if (parts[i].StartsWith("model:", StringComparison.Ordinal))
        {
          model = parts[i]["model:".Length..];
        }
        else if (parts[i].StartsWith("product:", StringComparison.Ordinal))
        {
          product = parts[i]["product:".Length..];
        }
        else if (i == 2 && !parts[i].Contains(':', StringComparison.Ordinal))
        {
          // "no permissions (...)" spreads the state over several words.
          stateText += " " + parts[i];
        }
      }

      devices.Add(new AdbDevice(parts[0], ParseState(stateText), model, product, stateText));
    }

    return devices;
  }

  /// <summary>
  /// Lists the connected devices by running <c>adb devices -l</c>.
  /// </summary>
  /// <param name="cancellationToken">A token that cancels the call.</param>
  /// <returns>
  /// <see cref="AdbStatus.Ok"/> with the devices; <see cref="AdbStatus.NoDevices"/> when none are connected;
  /// <see cref="AdbStatus.UnauthorizedDevice"/> when every connected device still needs to be authorized; or an
  /// error status.
  /// </returns>
  public async Task<AdbResult> ListDevicesAsync(CancellationToken cancellationToken = default)
  {
    var adb = _adbPath ?? FindAdb();
    if (adb is null)
    {
      return NotFound();
    }

    var run = await RunAsync(adb, ["devices", "-l"], cancellationToken).ConfigureAwait(false);
    if (run.Problem is not null)
    {
      return run.Problem;
    }

    if (run.ExitCode != 0)
    {
      var reason = string.IsNullOrWhiteSpace(run.Error) ? run.Output.Trim() : run.Error.Trim();
      return new AdbResult(AdbStatus.Failed, $"adb could not list devices: {reason}", []);
    }

    return Summarize(ParseDevices(run.Output));
  }

  /// <summary>
  /// Forwards a port from the device to this PC by running <c>adb reverse tcp:port tcp:port</c>.
  /// </summary>
  /// <param name="port">The port to forward, normally the debugger's port.</param>
  /// <param name="serial">The device to use (adb's <c>-s</c> option), or <see langword="null"/> when exactly one device is connected.</param>
  /// <param name="cancellationToken">A token that cancels the call.</param>
  /// <returns>A result whose message can be shown to the person as it is.</returns>
  public async Task<AdbResult> ReverseAsync(int port, string? serial = null, CancellationToken cancellationToken = default)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
    var adb = _adbPath ?? FindAdb();
    if (adb is null)
    {
      return NotFound();
    }

    var listed = await ListDevicesAsync(cancellationToken).ConfigureAwait(false);
    var devices = listed.Devices;
    if (listed.Status is not (AdbStatus.Ok or AdbStatus.UnauthorizedDevice))
    {
      return listed;
    }

    if (serial is null)
    {
      var ready = devices.Count(d => d.State == AdbDeviceState.Ready);
      if (ready == 0)
      {
        return Summarize(devices);
      }

      if (ready > 1)
      {
        return new AdbResult(
            AdbStatus.ManyDevices, "More than one device is connected. Choose which device to use.", devices);
      }
    }
    else
    {
      var chosen = devices.FirstOrDefault(d => d.Serial == serial);
      if (chosen is null)
      {
        return new AdbResult(AdbStatus.NoDevices, $"The device {serial} is not connected.", devices);
      }

      if (chosen.State == AdbDeviceState.Unauthorized)
      {
        return Unauthorized(devices);
      }
    }

    var forward = string.Create(CultureInfo.InvariantCulture, $"tcp:{port}");
    List<string> arguments = [];
    if (serial is not null)
    {
      arguments.Add("-s");
      arguments.Add(serial);
    }

    arguments.AddRange(["reverse", forward, forward]);
    var run = await RunAsync(adb, arguments, cancellationToken).ConfigureAwait(false);
    if (run.Problem is not null)
    {
      return run.Problem;
    }

    if (run.ExitCode != 0)
    {
      var reason = string.IsNullOrWhiteSpace(run.Error) ? run.Output.Trim() : run.Error.Trim();
      return new AdbResult(AdbStatus.Failed, $"adb could not forward port {port}: {reason}", devices);
    }

    return new AdbResult(AdbStatus.Ok, $"Port {port} is forwarded. The game can connect to 127.0.0.1:{port}.", devices);
  }

  private static AdbDeviceState ParseState(string text) => text switch
  {
    "device" => AdbDeviceState.Ready,
    "unauthorized" => AdbDeviceState.Unauthorized,
    "offline" => AdbDeviceState.Offline,
    _ when text.StartsWith("no permissions", StringComparison.Ordinal) => AdbDeviceState.NoPermissions,
    _ => AdbDeviceState.Other,
  };

  private static AdbResult NotFound() => new(
      AdbStatus.AdbNotFound,
      "adb was not found. Install the Android SDK platform-tools and set ANDROID_HOME, or add adb to your PATH.",
      []);

  private static AdbResult Unauthorized(IReadOnlyList<AdbDevice> devices) => new(
      AdbStatus.UnauthorizedDevice,
      "The device has not authorized this computer. Unlock it and accept the USB debugging prompt, then try again.",
      devices);

  private static AdbResult Summarize(IReadOnlyList<AdbDevice> devices)
  {
    if (devices.Count == 0)
    {
      return new AdbResult(
          AdbStatus.NoDevices, "No device is connected. Plug one in over USB and turn on USB debugging.", devices);
    }

    if (devices.All(d => d.State == AdbDeviceState.Unauthorized))
    {
      return Unauthorized(devices);
    }

    var ready = devices.Count(d => d.State == AdbDeviceState.Ready);
    var message = ready == 1 ? "1 device is ready." : $"{ready} devices are ready.";
    return new AdbResult(AdbStatus.Ok, message, devices);
  }

  private async Task<RunOutcome> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
  {
    var info = new ProcessStartInfo(file)
    {
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      StandardOutputEncoding = Encoding.UTF8,
      StandardErrorEncoding = Encoding.UTF8,
    };
    foreach (var argument in arguments)
    {
      info.ArgumentList.Add(argument);
    }

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(_timeout);
    Process? process = null;
    try
    {
      process = Process.Start(info);
      if (process is null)
      {
        return new RunOutcome(NotFound());
      }

      var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
      var error = process.StandardError.ReadToEndAsync(timeout.Token);
      await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
      return new RunOutcome(null, process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }
    catch (Win32Exception)
    {
      return new RunOutcome(NotFound());
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      Kill(process);
      return new RunOutcome(new AdbResult(
          AdbStatus.TimedOut, $"adb did not answer within {_timeout.TotalSeconds:0} seconds.", []));
    }
    catch (OperationCanceledException)
    {
      Kill(process);
      throw;
    }
    finally
    {
      process?.Dispose();
    }
  }

  private static void Kill(Process? process)
  {
    try
    {
      process?.Kill(entireProcessTree: true);
    }
    catch (Exception e) when (e is InvalidOperationException or Win32Exception)
    {
      // The process already ended, which is exactly what we wanted.
    }
  }

  private sealed record RunOutcome(AdbResult? Problem, int ExitCode = 0, string Output = "", string Error = "");
}
