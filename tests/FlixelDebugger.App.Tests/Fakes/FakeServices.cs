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

using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Adb;
using FlixelDebugger.Core.Settings;

namespace FlixelDebugger.App.Tests.Fakes;

/// <summary>Runs posted work immediately (or, in deferred mode, when the test flushes it) and ticks timers only when the test says so.</summary>
internal sealed class FakeScheduler : IUiScheduler
{
  private readonly List<Action> _ticks = [];
  private readonly Queue<Action> _queue = new();
  private readonly Lock _lock = new();

  /// <summary>Gets or sets whether posted work waits for <see cref="Flush"/>, like the real UI thread's queue.</summary>
  public bool Deferred { get; set; }

  public void Post(Action action)
  {
    if (!Deferred)
    {
      action();
      return;
    }

    lock (_lock)
    {
      _queue.Enqueue(action);
    }
  }

  public int Pending
  {
    get
    {
      lock (_lock)
      {
        return _queue.Count;
      }
    }
  }

  public void Flush()
  {
    while (true)
    {
      Action action;
      lock (_lock)
      {
        if (!_queue.TryDequeue(out action!))
        {
          return;
        }
      }

      action();
    }
  }

  public IDisposable Every(TimeSpan interval, Action tick)
  {
    _ticks.Add(tick);
    return new Handle(() => _ticks.Remove(tick));
  }

  public void Tick()
  {
    foreach (var tick in _ticks.ToArray())
    {
      tick();
    }
  }

  private sealed class Handle(Action stop) : IDisposable
  {
    public void Dispose() => stop();
  }
}

internal sealed class FakeHost : IDebugHost
{
  private readonly List<IGameSession> _sessions = [];

  public event EventHandler<HostSessionEventArgs>? SessionConnected;

  public event EventHandler<HostSessionEventArgs>? SessionDisconnected;

  public bool IsRunning { get; private set; }

  public int Port { get; private set; } = 6510;

  public bool AllInterfaces { get; private set; }

  public string ListenAddress => $"ws://{(AllInterfaces ? "0.0.0.0" : "127.0.0.1")}:{Port}/";

  public IReadOnlyList<IGameSession> Sessions => _sessions;

  public Exception? StartFailure { get; set; }

  public List<string> Calls { get; } = [];

  public void Configure(int port, bool listenOnAllInterfaces)
  {
    Calls.Add($"configure:{port}:{listenOnAllInterfaces}");
    Port = port;
    AllInterfaces = listenOnAllInterfaces;
  }

  public Task StartAsync(CancellationToken cancellationToken = default)
  {
    Calls.Add("start");
    if (StartFailure is { } failure)
    {
      return Task.FromException(failure);
    }

    IsRunning = true;
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken = default)
  {
    Calls.Add("stop");
    IsRunning = false;
    return Task.CompletedTask;
  }

  public ValueTask DisposeAsync() => ValueTask.CompletedTask;

  public void Connect(IGameSession session)
  {
    _sessions.Add(session);
    SessionConnected?.Invoke(this, new HostSessionEventArgs(session));
  }

  public void Disconnect(FakeGameSession session)
  {
    _sessions.Remove(session);
    session.Close();
    SessionDisconnected?.Invoke(this, new HostSessionEventArgs(session));
  }
}

internal sealed class FakeAdb : IAdbClient
{
  public Queue<AdbResult> Results { get; } = new();

  public List<(int Port, string? Serial)> Calls { get; } = [];

  public Task<AdbResult> ReverseAsync(int port, string? serial = null, CancellationToken cancellationToken = default)
  {
    Calls.Add((port, serial));
    return Task.FromResult(Results.Dequeue());
  }
}

internal sealed class FakeNotifier : INotificationService
{
  public List<(NotificationLevel Level, string Message)> Messages { get; } = [];

  public void Show(NotificationLevel level, string message) => Messages.Add((level, message));
}

internal sealed class FakeDialogs : IDialogService
{
  public bool SettingsResult { get; set; }

  public Action<SettingsViewModel>? EditSettings { get; set; }

  public string? PickedSerial { get; set; }

  public int PickCalls { get; private set; }

  public Task<bool> ShowSettingsAsync(SettingsViewModel settings)
  {
    EditSettings?.Invoke(settings);
    return Task.FromResult(SettingsResult);
  }

  public Task<string?> PickDeviceAsync(IReadOnlyList<AdbDevice> devices)
  {
    PickCalls++;
    return Task.FromResult(PickedSerial);
  }
}

internal sealed class MemorySettings : ISettingsStore
{
  public AppSettings Current { get; } = new();

  public int Saves { get; private set; }

  public bool Save()
  {
    Saves++;
    return true;
  }
}
