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

using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Tests.Fakes;

/// <summary>
/// A game for app tests. It keeps real <see cref="UiPanelState"/> objects, so the app sees panels
/// exactly as Core would give them, and records everything the app sends.
/// </summary>
internal sealed class FakeGameSession(
    string title = "Test Game", string platform = "desktop", string device = "Linux") : IGameSession
{
  private readonly Dictionary<string, HashSet<string>> _open = [];
  private UiPanelState[] _panels = [];

  public event EventHandler? StateChanged;

  public event EventHandler<SessionFeedEventArgs>? FeedUpdated;

  public event EventHandler? PingUpdated;

  public event EventHandler? PanelListChanged;

  public event EventHandler<PanelTreeEventArgs>? PanelTreeReceived;

  public event EventHandler<PanelPatchEventArgs>? PanelPatched;

  public event EventHandler<AppRequestEventArgs>? AppRequested;

  public Guid Id { get; } = Guid.NewGuid();

  public string Title { get; } = title;

  public string Platform { get; } = platform;

  public string Device { get; } = device;

  public SessionState State { get; set; } = SessionState.Live;

  public double? PingMs { get; set; }

  public StatsHistory Stats { get; } = new();

  public ControlsPayload? Controls { get; set; }

  public IReadOnlyList<UiPanelState> Panels => _panels;

  public List<string> Actions { get; } = [];

  public List<(string Panel, int Widget, WidgetEvent Kind, IReadOnlyList<UiValue> Args)> Events
  {
    get;
  } = [];

  public HashSet<string> WantedFeeds { get; } = [];

  public void Close()
  {
    State = SessionState.Closed;
    StateChanged?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>Tells whether any owner holds a panel open.</summary>
  public bool IsOpen(string panelId) =>
      _open.TryGetValue(panelId, out var owners) && owners.Count > 0;

  /// <summary>Replaces the panel list, keeping the state of panels whose id stays.</summary>
  public void SetPanels(params PanelInfo[] panels)
  {
    var list = new UiPanelState[panels.Length];
    for (var i = 0; i < panels.Length; i++)
    {
      var existing = Array.Find(_panels, p => p.Id == panels[i].Id);
      if (existing is not null)
      {
        existing.Update(panels[i]);
        existing.ClearTree();
      }

      list[i] = existing ?? new UiPanelState(panels[i]);
    }

    _panels = list;
    PanelListChanged?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>Sends a whole widget tree for a panel.</summary>
  public void SendTree(string panelId, WidgetNode root)
  {
    var panel = Find(panelId);
    panel.ApplyTree(root);
    PanelTreeReceived?.Invoke(this, new PanelTreeEventArgs(panel, root));
  }

  /// <summary>Sends property changes for a panel.</summary>
  public void SendPatch(string panelId, params PatchOp[] ops)
  {
    var panel = Find(panelId);
    panel.ApplyPatch(ops);
    PanelPatched?.Invoke(this, new PanelPatchEventArgs(panel, ops));
  }

  public void Request(AppRequest request) =>
      AppRequested?.Invoke(this, new AppRequestEventArgs(request));

  public void RaiseFeedUpdated(FeedKind kind, string id = "") =>
      FeedUpdated?.Invoke(this, new SessionFeedEventArgs(id, kind));

  public void RaisePingUpdated() => PingUpdated?.Invoke(this, EventArgs.Empty);

  public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) =>
      Record($"pause:{paused}");

  public Task<bool> StepAsync(CancellationToken cancellationToken = default) => Record("step");

  public Task<bool> SetHitboxesAsync(
      bool hitboxes, CancellationToken cancellationToken = default) =>
      Record($"hitboxes:{hitboxes}");

  public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default) =>
      Record($"freecam:{freeCam}");

  public Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default) =>
      Record("clearLogs");

  public Task<bool> SetFeedWantedAsync(
      string feedId, string owner, bool wanted, CancellationToken cancellationToken = default)
  {
    var key = $"{feedId}:{owner}";
    if (wanted)
    {
      WantedFeeds.Add(key);
    }
    else
    {
      WantedFeeds.Remove(key);
    }

    return Task.FromResult(true);
  }

  public Task<bool> SetPanelOpenAsync(
      string panelId, string owner, bool open, CancellationToken cancellationToken = default)
  {
    if (Array.Find(_panels, p => p.Id == panelId) is null)
    {
      return Task.FromResult(false);
    }

    if (!_open.TryGetValue(panelId, out var owners))
    {
      owners = [];
      _open[panelId] = owners;
    }

    if (open)
    {
      owners.Add(owner);
    }
    else
    {
      owners.Remove(owner);
    }

    return Task.FromResult(true);
  }

  public Task<bool> SendPanelEventAsync(
      string panelId,
      int widget,
      WidgetEvent kind,
      IReadOnlyList<UiValue> args,
      CancellationToken cancellationToken = default)
  {
    Events.Add((panelId, widget, kind, args));
    return Task.FromResult(true);
  }

  private UiPanelState Find(string panelId) =>
      Array.Find(_panels, p => p.Id == panelId)
          ?? throw new InvalidOperationException($"No panel '{panelId}'.");

  private Task<bool> Record(string action)
  {
    Actions.Add(action);
    return Task.FromResult(true);
  }
}
