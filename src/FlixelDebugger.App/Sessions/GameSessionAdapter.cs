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

using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Sessions;

/// <summary>
/// Presents a Core <see cref="GameSession"/> as an <see cref="IGameSession"/> by forwarding every member.
/// </summary>
/// <remarks>
/// The adapter holds no state of its own, so it cannot go out of sync with the session. Use <see cref="Core"/>
/// when a feature needs something that <see cref="IGameSession"/> does not list yet.
/// </remarks>
public sealed class GameSessionAdapter : IGameSession
{
    private const string FallbackTitle = "Game";

    /// <summary>
    /// Wraps a session and starts forwarding its events.
    /// </summary>
    /// <param name="core">The real session.</param>
    public GameSessionAdapter(GameSession core)
    {
        ArgumentNullException.ThrowIfNull(core);
        Core = core;
        core.StateChanged += (_, e) => StateChanged?.Invoke(this, e);
        core.PanelListChanged += (_, e) => PanelListChanged?.Invoke(this, e);
        core.PingUpdated += (_, e) => PingUpdated?.Invoke(this, e);
        core.PanelUpdated += (_, e) => PanelUpdated?.Invoke(this, new SessionPanelEventArgs(e.Panel.Id, e.Panel.Kind));
    }

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <inheritdoc/>
    public event EventHandler? PanelListChanged;

    /// <inheritdoc/>
    public event EventHandler<SessionPanelEventArgs>? PanelUpdated;

    /// <inheritdoc/>
    public event EventHandler? PingUpdated;

    /// <summary>Gets the real session behind this adapter.</summary>
    public GameSession Core { get; }

    /// <inheritdoc/>
    public Guid Id => Core.Id;

    /// <inheritdoc/>
    public string Title => Core.Hello is { GameTitle.Length: > 0 } hello ? hello.GameTitle : FallbackTitle;

    /// <inheritdoc/>
    public string Platform => Core.Hello?.Platform ?? string.Empty;

    /// <inheritdoc/>
    public string Device => Core.Hello?.Device ?? string.Empty;

    /// <inheritdoc/>
    public SessionState State => Core.State;

    /// <inheritdoc/>
    public double? PingMs => Core.PingMs;

    /// <inheritdoc/>
    public LogStore Logs => Core.Logs;

    /// <inheritdoc/>
    public StatsHistory Stats => Core.Stats;

    /// <inheritdoc/>
    public ControlsPayload? Controls => Core.Controls;

    /// <inheritdoc/>
    public ConsoleCommandsPayload? ConsoleCommands => Core.ConsoleCommands;

    /// <inheritdoc/>
    public ConsoleResultPayload? ConsoleResult => Core.ConsoleResult;

    /// <inheritdoc/>
    public IReadOnlyList<SessionPanel> Panels
    {
        get
        {
            var panels = Core.Panels;
            var list = new SessionPanel[panels.Count];
            for (var i = 0; i < list.Length; i++)
            {
                list[i] = new SessionPanel(panels[i].Id, panels[i].Name, panels[i].Kind);
            }

            return list;
        }
    }

    /// <inheritdoc/>
    public Task<bool> RunCommandAsync(string line, CancellationToken cancellationToken = default) =>
        Core.RunCommandAsync(line, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default) =>
        Core.SetPausedAsync(paused, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> StepAsync(CancellationToken cancellationToken = default) => Core.StepAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<bool> SetHitboxesAsync(bool hitboxes, CancellationToken cancellationToken = default) =>
        Core.SetHitboxesAsync(hitboxes, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default) =>
        Core.SetFreeCamAsync(freeCam, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default) => Core.ClearLogsAsync(cancellationToken);
}
