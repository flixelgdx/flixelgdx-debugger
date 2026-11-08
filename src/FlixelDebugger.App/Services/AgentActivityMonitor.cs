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

using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlixelDebugger.App.Services;

/// <summary>
/// Tracks whether an AI agent is using the debugger right now, and what it touched last, so the
/// window can show it.
/// </summary>
/// <remarks>
/// <para>
/// An agent acts in bursts: a tool call, a pause while the model thinks, another call. The monitor
/// calls the agent <i>active</i> from a tool call until <see cref="IdleTimeout"/> passes without
/// another one, and then everything fades back to normal. One timer on the UI thread checks the
/// time, so a call costs nothing more than the event it raises.
/// </para>
/// <para>
/// Events arrive on a background thread and are moved to the UI thread with
/// <see cref="IUiScheduler.Post"/> before any state changes, so the ViewModels and controls that
/// read this class never see a half-updated value. <see cref="Changed"/> fires on the UI thread
/// after every change.
/// </para>
/// </remarks>
public sealed partial class AgentActivityMonitor : ObservableObject, IDisposable
{
  /// <summary>How long without a tool call before the agent counts as idle.</summary>
  public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(3);

  /// <summary>How long an injected pointer position stays marked in the viewport.</summary>
  public static readonly TimeSpan PointerLife = TimeSpan.FromMilliseconds(1500);

  /// <summary>How often the monitor checks whether the agent went idle.</summary>
  public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

  /// <summary>The most calls <see cref="RecentText"/> remembers.</summary>
  public const int RecentCapacity = 10;

  private readonly TimeProvider _clock;
  private readonly IUiScheduler _ui;
  private readonly IDisposable _timer;
  private readonly Queue<string> _recent = new();
  private long _activeStamp;
  private long _pointerStamp;

  [ObservableProperty]
  private bool _isActive;

  [ObservableProperty]
  private string _client = string.Empty;

  [ObservableProperty]
  private string _tool = string.Empty;

  [ObservableProperty]
  private string _badgeText = string.Empty;

  [ObservableProperty]
  private string _recentText = string.Empty;

  /// <summary>
  /// Creates the monitor and starts following the MCP server.
  /// </summary>
  /// <param name="mcp">The MCP server whose calls are followed.</param>
  /// <param name="ui">Moves work to the UI thread and runs the idle timer.</param>
  /// <param name="clock">Tells the time, so tests can move it by hand.</param>
  public AgentActivityMonitor(IMcpService mcp, IUiScheduler ui, TimeProvider clock)
  {
    ArgumentNullException.ThrowIfNull(mcp);
    ArgumentNullException.ThrowIfNull(ui);
    ArgumentNullException.ThrowIfNull(clock);
    _ui = ui;
    _clock = clock;
    mcp.Activity += (_, e) => ui.Post(() => Apply(e));
    _timer = ui.Every(TickInterval, Tick);
  }

  /// <summary>Raised on the UI thread after the activity state changed.</summary>
  public event EventHandler? Changed;

  /// <summary>
  /// Gets the id of the game the agent is driving, or <see langword="null"/> when idle or unknown.
  /// </summary>
  public Guid? SessionId { get; private set; }

  /// <summary>
  /// Gets the id of the game object the agent touched last, or -1 when none.
  /// </summary>
  public int ObjectId { get; private set; } = -1;

  /// <summary>
  /// Gets whether the agent injected a pointer position a moment ago.
  /// </summary>
  public bool HasPointer { get; private set; }

  /// <summary>Gets the x of the last injected pointer position in view units.</summary>
  public float PointerX { get; private set; }

  /// <summary>Gets the y of the last injected pointer position in view units.</summary>
  public float PointerY { get; private set; }

  /// <summary>
  /// Tells whether the agent is active on a game, which is when that game's panels mark what the
  /// agent touched.
  /// </summary>
  /// <param name="sessionId">The id of the game the panel shows.</param>
  /// <returns>
  /// <see langword="true"/> if the agent is active and its last call named this game, or named no
  /// game at all.
  /// </returns>
  public bool IsDriving(Guid? sessionId) =>
      IsActive && (SessionId is null || SessionId == sessionId);

  /// <summary>
  /// Ends the activity at once, for example when the MCP server stops.
  /// </summary>
  public void Reset()
  {
    if (!IsActive && ObjectId < 0 && !HasPointer)
    {
      return;
    }

    IsActive = false;
    SessionId = null;
    ObjectId = -1;
    HasPointer = false;
    Client = string.Empty;
    Tool = string.Empty;
    BadgeText = string.Empty;
    Changed?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>
  /// Stops the idle timer.
  /// </summary>
  public void Dispose()
  {
    _timer.Dispose();
    GC.SuppressFinalize(this);
  }

  private void Apply(Mcp.AgentActivityEventArgs e)
  {
    var now = _clock.GetTimestamp();
    _activeStamp = now;
    Client = e.Client;
    Tool = e.Tool;
    BadgeText = $"Agent: {e.Client} - {e.Tool}";
    SessionId = e.SessionId ?? SessionId;
    if (e.ObjectId is { } id)
    {
      ObjectId = id;
    }

    if (e.ViewX is { } x && e.ViewY is { } y)
    {
      PointerX = x;
      PointerY = y;
      HasPointer = true;
      _pointerStamp = now;
    }

    Remember($"{DateTime.Now:HH:mm:ss} {e.Client}: {e.Tool}");
    IsActive = true;
    Changed?.Invoke(this, EventArgs.Empty);
  }

  private void Tick()
  {
    if (!IsActive)
    {
      return;
    }

    if (_clock.GetElapsedTime(_activeStamp) >= IdleTimeout)
    {
      Reset();
      return;
    }

    if (HasPointer && _clock.GetElapsedTime(_pointerStamp) >= PointerLife)
    {
      HasPointer = false;
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  private void Remember(string line)
  {
    if (_recent.Count == RecentCapacity)
    {
      _recent.Dequeue();
    }

    _recent.Enqueue(line);
    var text = new StringBuilder();
    foreach (var item in _recent.Reverse())
    {
      text.AppendLine(item);
    }

    RecentText = text.ToString().TrimEnd();
  }
}
