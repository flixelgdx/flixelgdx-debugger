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
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FlixelDebugger.App.Tests.Fakes;

/// <summary>
/// The real app services (registry, dock layout, panels) with fake edges: no window, no socket, no
/// adb, no disk.
/// </summary>
internal sealed class Harness
{
  /// <summary>
  /// Creates the app's services with fakes around them.
  /// </summary>
  /// <param name="standardPanels">
  /// Whether the registry already knows the standard panels, as if a game had announced them in an
  /// earlier run. Without them the registry starts empty, as it does on a first start.
  /// </param>
  public Harness(bool standardPanels = true)
  {
    var services = new ServiceCollection();
    services.AddSingleton<IUiScheduler>(Scheduler);
    services.AddSingleton<IDebugHost>(Host);
    services.AddSingleton<IAdbClient>(Adb);
    services.AddSingleton<INotificationService>(Notifier);
    services.AddSingleton<IDialogService>(Dialogs);
    services.AddSingleton<ISettingsStore>(Settings);
    services.AddSingleton<IMcpService>(Mcp);
    services.AddSingleton<TimeProvider>(Clock);
    services.AddFlixelDebuggerApp();
    Provider = services.BuildServiceProvider();
    if (standardPanels)
    {
      Registry.RegisterAll(TestData.StandardPanels.Select(
          p => new PanelDefinition(p.Id, p.Title, p.Dock, p.Shown)));
    }
  }

  public FakeScheduler Scheduler { get; } = new();

  public FakeHost Host { get; } = new();

  public FakeAdb Adb { get; } = new();

  public FakeNotifier Notifier { get; } = new();

  public FakeDialogs Dialogs { get; } = new();

  public MemorySettings Settings { get; } = new();

  public FakeMcp Mcp { get; } = new();

  public FakeClock Clock { get; } = new();

  public ServiceProvider Provider { get; }

  public T Get<T>()
      where T : notnull => Provider.GetRequiredService<T>();

  public IActiveSession Active => Get<IActiveSession>();

  public IPanelRegistry Registry => Get<IPanelRegistry>();

  public IPanelLayout Layout => Get<IPanelLayout>();

  public MainWindowViewModel Main => Get<MainWindowViewModel>();

  /// <summary>Connects a fake game to the host and returns it.</summary>
  public FakeGameSession Connect(string title = "Test Game", string device = "Linux")
  {
    var session = new FakeGameSession(title, "desktop", device);
    session.SetPanels(TestData.StandardPanels);
    Host.Connect(session);
    return session;
  }
}
