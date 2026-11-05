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

using Dock.Model.Controls;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Tests.Fakes;

/// <summary>A panel layout that records what the ViewModels ask of it.</summary>
internal sealed class FakeLayout : IPanelLayout
{
  public event EventHandler? RootChanged
  {
    add { }
    remove { }
  }

  public event EventHandler<PanelVisibilityChangedEventArgs>? VisibilityChanged
  {
    add { }
    remove { }
  }

  public IRootDock? Root => null;

  public List<string> Revealed { get; } = [];

  public int Resets { get; private set; }

  public bool IsOpen(string id) => true;

  public bool IsVisible(string id) => true;

  public bool SetVisible(string id, bool visible) => true;

  public bool Reveal(string id)
  {
    Revealed.Add(id);
    return true;
  }

  public void Reset() => Resets++;

  public string? Save() => null;

  public bool Restore(string? text) => false;
}

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeClock : TimeProvider
{
  private long _timestamp;

  public override long TimestampFrequency => 1000;

  public override long GetTimestamp() => _timestamp;

  public void Advance(TimeSpan span) => _timestamp += (long)span.TotalMilliseconds;
}

/// <summary>
/// An active session, a fake game, and a scheduler that ticks by hand: everything a panel ViewModel
/// needs.
/// </summary>
internal sealed class PanelFixture
{
  public PanelFixture(bool connect = true)
  {
    if (connect)
    {
      Active.Current = Game;
    }
  }

  public ActiveSession Active { get; } = new();

  public FakeScheduler Scheduler { get; } = new();

  public FakeGameSession Game { get; } = new();

  public FakeLayout Layout { get; } = new();

  public FakeClock Clock { get; } = new();

  public SelectionOrigin Origin { get; } = new();

  public StatsPanelViewModel Stats() => new(Active, Scheduler);

  public ControlsPanelViewModel Controls() => new(Active, Scheduler, Layout);

  public WatchesPanelViewModel Watches() => new(Active, Scheduler);

  public TrackersPanelViewModel Trackers() => new(Active, Scheduler);

  public ScenePanelViewModel Scene() => new(Active, Scheduler, Origin);

  public InspectorPanelViewModel Inspector() => new(Active, Scheduler, Layout, Origin);

  public TexturesPanelViewModel Textures() => new(Active, Scheduler);

  public SavePanelViewModel Save() => new(Active, Scheduler, Clock);

  public CustomPanelViewModel Custom(string id = "custom.player", string title = "Player") =>
      new(id, title, Active, Scheduler);

  /// <summary>Ticks the scheduler so every panel refreshes.</summary>
  public void Tick() => Scheduler.Tick();
}
