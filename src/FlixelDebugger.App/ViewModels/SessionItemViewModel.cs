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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One entry of the list of connected games.
/// </summary>
/// <remarks>
/// The entry shows the game title, the platform, the device, and the ping. Call <see cref="Refresh"/> to update
/// the parts that change over time (the ping and the connection state).
/// </remarks>
public sealed partial class SessionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _pingText = string.Empty;

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private string _label = string.Empty;

    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="session">The game this entry stands for.</param>
    public SessionItemViewModel(IGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Session = session;
        Refresh();
    }

    /// <summary>Gets the game this entry stands for.</summary>
    public IGameSession Session { get; }

    /// <summary>Gets the game title.</summary>
    public string Title => Session.Title;

    /// <summary>Gets the platform: <c>desktop</c>, <c>android</c>, or <c>web</c>.</summary>
    public string Platform => Session.Platform;

    /// <summary>Gets the device description.</summary>
    public string Device => Session.Device;

    /// <summary>
    /// Tells whether another entry is the same game on the same device, which is how a reconnect is recognized.
    /// </summary>
    /// <param name="other">The other session.</param>
    /// <returns><see langword="true"/> if title and device are equal.</returns>
    public bool IsSameGameAs(IGameSession other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Session.Title, other.Title, StringComparison.Ordinal) &&
               string.Equals(Session.Device, other.Device, StringComparison.Ordinal);
    }

    /// <summary>
    /// Updates the ping and the connection state from the session.
    /// </summary>
    public void Refresh()
    {
        IsConnected = Session.State != SessionState.Closed;
        PingText = Session.PingMs is { } ping ? string.Create(CultureInfo.InvariantCulture, $"{ping:0} ms") : "- ms";
        var device = Device.Length > 0 ? $", {Device}" : string.Empty;
        var state = IsConnected ? PingText : "disconnected";
        Label = $"{Title} ({Platform}{device}) - {state}";
    }
}
