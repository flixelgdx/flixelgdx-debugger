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
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlixelDebugger.Core;

/// <summary>
/// Registers the debugger's core services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds the settings, the WebSocket server, and the adb helper as singletons.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The server's port and network scope come from the saved <see cref="AppSettings"/>. Nothing is
  /// started: call <see cref="DebugServer.StartAsync"/> when the app is ready. Services that were
  /// registered earlier are kept, so an app or a test can replace any of them.
  /// </para>
  /// </remarks>
  /// <example>
  /// <code>
  /// services.AddFlixelDebuggerCore();
  /// var server = provider.GetRequiredService&lt;DebugServer&gt;();
  /// </code>
  /// </example>
  /// <param name="services">The service collection to add to.</param>
  /// <param name="settingsPath">
  /// The settings file to read, or <see langword="null"/> for the default location.
  /// </param>
  /// <returns>The same collection, for chaining.</returns>
  public static IServiceCollection AddFlixelDebuggerCore(
      this IServiceCollection services, string? settingsPath = null)
  {
    ArgumentNullException.ThrowIfNull(services);
    services.TryAddSingleton(_ => AppSettings.Load(settingsPath));
    services.TryAddSingleton(
        provider => provider.GetRequiredService<AppSettings>().CreateServerOptions());
    services.TryAddSingleton<DebugServer>();
    services.TryAddSingleton<AdbHelper>();
    return services;
  }
}
