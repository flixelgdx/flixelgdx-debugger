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

namespace FlixelDebugger.App.Services;

/// <summary>
/// Runs work on the UI thread and ticks on a timer, which keeps ViewModels independent of Avalonia.
/// </summary>
/// <remarks>
/// <para>
/// The server raises its events on background threads, but controls may only be touched on the UI thread.
/// ViewModels therefore never use a dispatcher directly: they ask this interface instead. The app plugs in the
/// Avalonia dispatcher and the tests plug in a fake that runs everything right away and ticks by hand, so a
/// test never has to sleep.
/// </para>
/// </remarks>
public interface IUiScheduler
{
    /// <summary>
    /// Runs an action on the UI thread, later if the caller is on another thread.
    /// </summary>
    /// <param name="action">The work to run.</param>
    public void Post(Action action);

    /// <summary>
    /// Calls an action on the UI thread at a fixed interval until the returned handle is disposed.
    /// </summary>
    /// <param name="interval">The time between two calls.</param>
    /// <param name="tick">The work to run on every tick.</param>
    /// <returns>A handle that stops the timer when disposed.</returns>
    public IDisposable Every(TimeSpan interval, Action tick);
}
