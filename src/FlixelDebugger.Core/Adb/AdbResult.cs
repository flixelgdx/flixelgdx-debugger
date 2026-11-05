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
/// The outcome of an adb operation, in terms a person can act on.
/// </summary>
public enum AdbStatus
{
    /// <summary>The operation worked.</summary>
    Ok,

    /// <summary>The <c>adb</c> program could not be found.</summary>
    AdbNotFound,

    /// <summary>adb works but no device is connected.</summary>
    NoDevices,

    /// <summary>A device is connected but has not authorized this computer yet.</summary>
    UnauthorizedDevice,

    /// <summary>Several devices are ready and the caller did not say which one to use.</summary>
    ManyDevices,

    /// <summary>adb ran but reported an error.</summary>
    Failed,

    /// <summary>adb did not finish in time.</summary>
    TimedOut,
}

/// <summary>
/// The result of an adb operation: a status, a friendly message, and the devices that were listed.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">A short sentence for the person, in plain American English.</param>
/// <param name="Devices">The devices adb listed. It is empty when adb could not be run.</param>
public sealed record AdbResult(AdbStatus Status, string Message, IReadOnlyList<AdbDevice> Devices)
{
    /// <summary>Gets whether the operation worked.</summary>
    public bool Success => Status == AdbStatus.Ok;
}
