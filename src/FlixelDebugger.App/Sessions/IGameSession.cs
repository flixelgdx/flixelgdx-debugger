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

using FlixelDebugger.Core.Completion;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Sessions;

/// <summary>
/// A panel a connected game has, as the UI needs to know it.
/// </summary>
/// <param name="Id">The stable panel id, for example <c>stats</c> or <c>custom.player</c>.</param>
/// <param name="Name">The display name the game gave the panel.</param>
/// <param name="Kind">The kind of the panel.</param>
public readonly record struct SessionPanel(string Id, string Name, PanelKind Kind);

/// <summary>
/// Describes a panel of a game whose data just changed.
/// </summary>
/// <param name="panelId">The id of the panel.</param>
/// <param name="kind">The kind of the panel.</param>
public sealed class SessionPanelEventArgs(string panelId, PanelKind kind) : EventArgs
{
    /// <summary>Gets the id of the panel.</summary>
    public string PanelId { get; } = panelId;

    /// <summary>Gets the kind of the panel.</summary>
    public PanelKind Kind { get; } = kind;
}

/// <summary>
/// One connected game, as the ViewModels see it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameSession"/> in Core is sealed and only the server can create it, so ViewModels would be
/// impossible to test with it. This interface lists the members the app needs. <see cref="GameSessionAdapter"/>
/// forwards every member to the real session, and tests use a small fake. Core still does all the work; nothing
/// is re-implemented here.
/// </para>
/// <para>
/// When a panel needs more of the session (for example the scene tree), add the member here and in the adapter.
/// </para>
/// <para>
/// <b>Threads.</b> Events are raised on background threads. Read the data properties from any thread, and move
/// to the UI thread (see <see cref="Services.IUiScheduler"/>) before touching a control.
/// </para>
/// </remarks>
public interface IGameSession : ICommandRunner
{
    /// <summary>Raised when <see cref="State"/> changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised when the game sent a new panel list.</summary>
    public event EventHandler? PanelListChanged;

    /// <summary>Raised after the data of a panel changed.</summary>
    public event EventHandler<SessionPanelEventArgs>? PanelUpdated;

    /// <summary>Raised when a new ping was measured.</summary>
    public event EventHandler? PingUpdated;

    /// <summary>Gets a unique id for this connection.</summary>
    public Guid Id { get; }

    /// <summary>Gets the title of the game, or <c>Game</c> if it did not send one.</summary>
    public string Title { get; }

    /// <summary>Gets the platform the game runs on: <c>desktop</c>, <c>android</c>, or <c>web</c>.</summary>
    public string Platform { get; }

    /// <summary>Gets a description of the device or operating system.</summary>
    public string Device { get; }

    /// <summary>Gets where the connection is in its life.</summary>
    public SessionState State { get; }

    /// <summary>Gets the last measured round trip time in milliseconds, or <see langword="null"/> if none was measured yet.</summary>
    public double? PingMs { get; }

    /// <summary>Gets the log lines of this game.</summary>
    public LogStore Logs { get; }

    /// <summary>Gets the stats history of this game.</summary>
    public StatsHistory Stats { get; }

    /// <summary>Gets the latest controls state, or <see langword="null"/> before the first one.</summary>
    public ControlsPayload? Controls { get; }

    /// <summary>Gets the latest console command list, or <see langword="null"/> before the first one.</summary>
    public ConsoleCommandsPayload? ConsoleCommands { get; }

    /// <summary>Gets the result of the last console command, or <see langword="null"/> if none ran.</summary>
    public ConsoleResultPayload? ConsoleResult { get; }

    /// <summary>Gets the panels the game currently has.</summary>
    public IReadOnlyList<SessionPanel> Panels { get; }

    /// <summary>Pauses or resumes the game.</summary>
    /// <param name="paused"><see langword="true"/> to pause.</param>
    /// <param name="cancellationToken">A token that cancels the send.</param>
    /// <returns>Whether the action was sent.</returns>
    public Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default);

    /// <summary>Advances the paused game by one frame.</summary>
    /// <param name="cancellationToken">A token that cancels the send.</param>
    /// <returns>Whether the action was sent.</returns>
    public Task<bool> StepAsync(CancellationToken cancellationToken = default);

    /// <summary>Shows or hides hitboxes in the game.</summary>
    /// <param name="hitboxes"><see langword="true"/> to show them.</param>
    /// <param name="cancellationToken">A token that cancels the send.</param>
    /// <returns>Whether the action was sent.</returns>
    public Task<bool> SetHitboxesAsync(bool hitboxes, CancellationToken cancellationToken = default);

    /// <summary>Turns the game's free camera on or off.</summary>
    /// <param name="freeCam"><see langword="true"/> to turn it on.</param>
    /// <param name="cancellationToken">A token that cancels the send.</param>
    /// <returns>Whether the action was sent.</returns>
    public Task<bool> SetFreeCamAsync(bool freeCam, CancellationToken cancellationToken = default);

    /// <summary>Clears the game's log and the lines stored here.</summary>
    /// <param name="cancellationToken">A token that cancels the send.</param>
    /// <returns>Whether the action was sent.</returns>
    public Task<bool> ClearLogsAsync(CancellationToken cancellationToken = default);
}
