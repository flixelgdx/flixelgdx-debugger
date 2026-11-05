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

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlixelDebugger.App;

/// <summary>
/// The Avalonia application: builds the services and opens the main window.
/// </summary>
public sealed class App : Application
{
  private ServiceProvider? _services;

  /// <summary>
  /// Gets the service provider, available once the framework finished initializing.
  /// </summary>
  public IServiceProvider? Services => _services;

  /// <inheritdoc/>
  public override void Initialize() => AvaloniaXamlLoader.Load(this);

  /// <inheritdoc/>
  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      _services = new ServiceCollection().AddFlixelDebuggerApp().BuildServiceProvider();
      var window = new MainWindow(_services.GetRequiredService<WindowNotificationService>())
      {
        DataContext = _services.GetRequiredService<MainWindowViewModel>(),
      };

      desktop.MainWindow = window;
      desktop.Exit += (_, _) =>
      {
        // The dock layout was already saved when the window closed. Stop the server, then free the
        // services.
        _services.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
      };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
