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

using System.Runtime.CompilerServices;
using System.Text.Json;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;
using ModelContextProtocol;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Shared helpers of the tool classes: finding the game, sending an action and waiting for the
/// game's answer, and writing common values.
/// </summary>
internal sealed class ToolContext(SessionResolver sessions)
{
  /// <summary>How long to wait for the game to report a change after an action.</summary>
  public static readonly TimeSpan ActionTimeout = TimeSpan.FromMilliseconds(1500);

  private readonly ConditionalWeakTable<GameSession, CaptureGate> _gates = new();
  private int _requestId;

  /// <summary>How long the screenshot tool waits for the game's picture.</summary>
  public TimeSpan CaptureTimeout { get; init; } = TimeSpan.FromSeconds(12);

  public SessionResolver Sessions { get; } = sessions;

  public GameSession Game(string? session) => Sessions.Resolve(session);

  public int NextRequestId() => Interlocked.Increment(ref _requestId);

  /// <summary>
  /// Gets the gate that lines up screenshots of one game. The game keeps one open capture request
  /// and drops the rest, so parallel callers must take turns.
  /// </summary>
  public CaptureGate GateFor(GameSession game) =>
      _gates.GetValue(game, static _ => new CaptureGate());

  /// <summary>
  /// Fails with a helpful message when the game has no panel with the given id.
  /// </summary>
  public static void RequirePanel(GameSession game, string panelId, string what)
  {
    if (!game.TryGetPanel(panelId, out _))
    {
      throw new McpException(
          $"This game has no '{panelId}' panel, so {what} is not available. Games built with " +
          "an older FlixelGDX do not have the capture and input panels; " +
          "update the framework.");
    }
  }

  /// <summary>
  /// Sends an action, then waits for the next matching payload of a panel.
  /// </summary>
  /// <returns>The payload, or <see langword="null"/> if the game did not answer in time.</returns>
  public static async Task<PanelPayload?> ActAsync(
      GameSession game,
      string panelId,
      Func<Task<bool>> send,
      Func<PanelPayload, bool>? match = null,
      TimeSpan? timeout = null,
      CancellationToken cancellationToken = default)
  {
    using var wait = PanelWait.Start(game, panelId, match);
    if (!await send().ConfigureAwait(false))
    {
      throw new McpException(
          $"Could not send to the '{panelId}' panel: the game has no such panel or the " +
          "connection is closed.");
    }

    return await wait.WaitAsync(timeout ?? ActionTimeout, cancellationToken)
        .ConfigureAwait(false);
  }

  /// <summary>
  /// Sends an action that has no reply and fails when it could not be sent.
  /// </summary>
  public static async Task SendAsync(GameSession game, string panelId, Func<Task<bool>> send)
  {
    if (!await send().ConfigureAwait(false))
    {
      throw new McpException(
          $"Could not send to the '{panelId}' panel: the game has no such panel or the " +
          "connection is closed.");
    }
  }

  public static string KindName(ValueKind kind) => kind switch
  {
    ValueKind.Bool => "bool",
    ValueKind.Int => "int",
    ValueKind.Long => "long",
    ValueKind.Float => "float",
    ValueKind.Double => "double",
    ValueKind.String => "string",
    ValueKind.Error => "error",
    _ => "unknown",
  };

  public static void WriteRow(Utf8JsonWriter writer, PropertyRow row)
  {
    writer.WriteStartObject();
    writer.WriteString("name", row.Name);
    writer.WriteString("value", ValueFormat.EditText(row.Value));
    writer.WriteString("kind", KindName(row.Value.Kind));
    writer.WriteBoolean("editable", row.Editable);
    writer.WriteEndObject();
  }

  public static void WriteNumber(Utf8JsonWriter writer, string name, double value) =>
      writer.WriteNumber(name, double.IsFinite(value) ? Math.Round(value, 2) : 0);
}

/// <summary>
/// Lets screenshots of one game run one after another, and lets a caller in line reuse a picture
/// that finished after it asked.
/// </summary>
internal sealed class CaptureGate
{
  public SemaphoreSlim Lock { get; } = new(1, 1);

  public CaptureImagePayload? Last { get; set; }

  public long LastDone { get; set; }

  public int LastMaxSide { get; set; }
}
