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

using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Panels.Docking;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core;
using FlixelDebugger.Core.Adb;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlixelDebugger.App;

/// <summary>
/// Registers the app's services and ViewModels with dependency injection.
/// </summary>
public static class AppServices
{
  /// <summary>
  /// Adds Core's services (see <see cref="ServiceCollectionExtensions.AddFlixelDebuggerCore"/>) and
  /// everything the app needs on top.
  /// </summary>
  /// <remarks>
  /// Services that were registered earlier are kept, so a test can replace any of them. Nothing is
  /// started: the main window starts the server when it opens.
  /// </remarks>
  /// <param name="services">The service collection to add to.</param>
  /// <param name="settingsPath">
  /// The settings file to use, or <see langword="null"/> for the default location.
  /// </param>
  /// <returns>The same collection, for chaining.</returns>
  public static IServiceCollection AddFlixelDebuggerApp(
      this IServiceCollection services, string? settingsPath = null)
  {
    ArgumentNullException.ThrowIfNull(services);
    services.AddFlixelDebuggerCore(settingsPath);
    services.TryAddSingleton(TimeProvider.System);
    services.TryAddSingleton<SelectionOrigin>();
    services.TryAddSingleton<IUiScheduler, AvaloniaUiScheduler>();
    services.TryAddSingleton<WindowNotificationService>();
    services.TryAddSingleton<INotificationService>(
        sp => sp.GetRequiredService<WindowNotificationService>());
    services.TryAddSingleton<ISettingsStore>(
        sp => new FileSettingsStore(sp.GetRequiredService<AppSettings>(), settingsPath));
    services.TryAddSingleton<IAdbClient>(sp => new AdbClient(sp.GetRequiredService<AdbHelper>()));
    services.TryAddSingleton<IDebugHost>(sp => new DebugHost(sp.GetRequiredService<DebugServer>()));
    services.TryAddSingleton<IActiveSession, ActiveSession>();
    services.TryAddSingleton<IPanelRegistry>(
        sp => new PanelRegistry(sp, DefaultPanels.CreateDefinitions()));
    services.TryAddSingleton<IPanelLayout>(
        sp => new DockPanelLayout(sp.GetRequiredService<IPanelRegistry>()));
    services.TryAddSingleton<IDialogService, DialogService>();
    services.TryAddSingleton<MainWindowViewModel>();
    return services;
  }
}
