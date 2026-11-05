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
/// How important a notification is, which decides its color.
/// </summary>
public enum NotificationLevel
{
    /// <summary>Plain information.</summary>
    Info,

    /// <summary>Something worked.</summary>
    Success,

    /// <summary>Something needs attention but nothing broke.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error,
}

/// <summary>
/// Shows short messages that disappear by themselves and never block the window.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a message.
    /// </summary>
    /// <param name="level">How important the message is.</param>
    /// <param name="message">The text to show. It should be a full sentence a person can act on.</param>
    public void Show(NotificationLevel level, string message);
}
