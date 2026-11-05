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
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests.Fakes;

/// <summary>A game session whose data and events the test controls by hand.</summary>
internal sealed class FakeGameSession(string title = "Test Game", string platform = "desktop", string device = "Linux") : IGameSession
{
    public event EventHandler? StateChanged;

    public event EventHandler? PanelListChanged;

    public event EventHandler<SessionPanelEventArgs>? PanelUpdated;

    public event EventHandler? PingUpdated;

    public Guid Id { get; } = Guid.NewGuid();

    public string Title { get; } = title;

    public string Platform { get; } = platform;

    public string Device { get; } = device;

    public SessionState State { get; set; } = SessionState.Live;

    public double? PingMs { get; set; }

    public LogStore Logs { get; } = new();

    public StatsHistory Stats { get; } = new();

    public ControlsPayload? Controls { get; set; }

    public ConsoleCommandsPayload? ConsoleCommands { get; set; }

    public ConsoleResultPayload? ConsoleResult { get; set; }

    public IReadOnlyList<SessionPanel> Panels { get; set; } = [];

    public List<string> RunLines { get; } = [];

    public List<string> Actions { get; } = [];

    public bool RunResult { get; set; } = true;

    public void Close()
    {
        State = SessionState.Closed;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RaisePanelUpdated(PanelKind kind, string id = "") => PanelUpdated?.Invoke(this, new SessionPanelEventArgs(id, kind));

    public void RaisePanelListChanged() => PanelListChanged?.Invoke(this, EventArgs.Empty);

    public void RaisePingUpdated() => PingUpdated?.Invoke(this, EventArgs.Empty);

    public Task<bool> RunCommandAsync(string line, CancellationToken cancellationToken = default)
    {
        RunLines.Add(line);
        return Task.FromResult(RunResult);
    }

    public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) => Record($"pause:{paused}");

    public Task<bool> StepAsync(CancellationToken cancellationToken = default) => Record("step");

    public Task<bool> SetHitboxesAsync(bool hitboxes, CancellationToken cancellationToken = default) => Record($"hitboxes:{hitboxes}");

    public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default) => Record($"freecam:{freeCam}");

    public Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default)
    {
        Logs.Clear();
        return Record("clearLogs");
    }

    private Task<bool> Record(string action)
    {
        Actions.Add(action);
        return Task.FromResult(true);
    }
}
