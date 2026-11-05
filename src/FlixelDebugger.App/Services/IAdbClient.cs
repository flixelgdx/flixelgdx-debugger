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

namespace FlixelDebugger.App.Services;

/// <summary>
/// The part of adb the app uses, behind an interface so tests do not need a phone.
/// </summary>
public interface IAdbClient
{
    /// <summary>
    /// Forwards a port from an Android device to this PC (<c>adb reverse</c>).
    /// </summary>
    /// <param name="port">The port to forward, normally the debugger's port.</param>
    /// <param name="serial">The device to use, or <see langword="null"/> when exactly one device is connected.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>A result with a message that can be shown as it is.</returns>
    public Task<AdbResult> ReverseAsync(int port, string? serial = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IAdbClient"/> of the real app, which forwards to <see cref="AdbHelper"/>.
/// </summary>
/// <param name="helper">The helper that runs adb.</param>
public sealed class AdbClient(AdbHelper helper) : IAdbClient
{
    /// <inheritdoc/>
    public Task<AdbResult> ReverseAsync(int port, string? serial = null, CancellationToken cancellationToken = default) =>
        helper.ReverseAsync(port, serial, cancellationToken);
}
