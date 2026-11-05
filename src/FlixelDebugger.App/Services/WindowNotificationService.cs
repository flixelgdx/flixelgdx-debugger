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

using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace FlixelDebugger.App.Services;

/// <summary>
/// The <see cref="INotificationService"/> of the real app: toast messages in the corner of the main
/// window.
/// </summary>
/// <remarks>
/// The window attaches its notification manager once it exists. Messages that arrive earlier are
/// dropped, because nothing can show them yet.
/// </remarks>
public sealed class WindowNotificationService : INotificationService
{
  private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(6);

  private WindowNotificationManager? _manager;

  /// <summary>
  /// Connects the service to the manager that draws the toasts.
  /// </summary>
  /// <param name="manager">The manager of the main window.</param>
  public void Attach(WindowNotificationManager manager)
  {
    ArgumentNullException.ThrowIfNull(manager);
    _manager = manager;
  }

  /// <inheritdoc/>
  public void Show(NotificationLevel level, string message)
  {
    ArgumentNullException.ThrowIfNull(message);
    var type = level switch
    {
      NotificationLevel.Success => NotificationType.Success,
      NotificationLevel.Warning => NotificationType.Warning,
      NotificationLevel.Error => NotificationType.Error,
      _ => NotificationType.Information,
    };

    Dispatcher.UIThread.Post(
        () => _manager?.Show(new Notification(null, message, type, _lifetime)));
  }
}
