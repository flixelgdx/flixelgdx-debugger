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

namespace FlixelDebugger.Mcp;

/// <summary>
/// Describes one tool call an AI agent made, so the app can show that an agent is at work.
/// </summary>
/// <remarks>
/// Events are raised on a background thread. A UI should copy what it needs and move to its own
/// thread before touching any control.
/// </remarks>
public sealed class AgentActivityEventArgs : EventArgs
{
  /// <summary>
  /// Creates the event data.
  /// </summary>
  /// <param name="client">The agent program name.</param>
  /// <param name="tool">The tool name.</param>
  /// <param name="sessionId">The game id, if known.</param>
  /// <param name="objectId">The touched object id, if any.</param>
  /// <param name="viewX">The injected pointer x in view units, if any.</param>
  /// <param name="viewY">The injected pointer y in view units, if any.</param>
  /// <param name="at">When the call happened.</param>
  public AgentActivityEventArgs(
      string client,
      string tool,
      Guid? sessionId,
      int? objectId,
      float? viewX,
      float? viewY,
      DateTimeOffset at)
  {
    Client = client;
    Tool = tool;
    SessionId = sessionId;
    ObjectId = objectId;
    ViewX = viewX;
    ViewY = viewY;
    At = at;
  }

  /// <summary>
  /// Gets the name the agent's program gave when it connected, for example Claude Code.
  /// </summary>
  public string Client { get; }

  /// <summary>Gets the name of the tool that was called.</summary>
  public string Tool { get; }

  /// <summary>
  /// Gets the id of the game the call acted on, or <see langword="null"/> when the call named no
  /// game and several are connected (or none).
  /// </summary>
  public Guid? SessionId { get; }

  /// <summary>
  /// Gets the id of the game object the call touched, or <see langword="null"/> if it touched
  /// none.
  /// </summary>
  public int? ObjectId { get; }

  /// <summary>
  /// Gets the x position of an injected pointer in view units, or <see langword="null"/>.
  /// </summary>
  public float? ViewX { get; }

  /// <summary>
  /// Gets the y position of an injected pointer in view units, or <see langword="null"/>.
  /// </summary>
  public float? ViewY { get; }

  /// <summary>Gets when the call happened.</summary>
  public DateTimeOffset At { get; }
}
