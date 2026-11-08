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

using System.Globalization;
using System.Text;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Sessions;
using ModelContextProtocol;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Finds the game a tool call is meant for.
/// </summary>
/// <remarks>
/// Several games can be connected at once, and guessing which one an agent meant could change the
/// wrong game. So a tool call must name its game with a <c>session</c> id, unless exactly one game
/// is connected, in which case that one is used. An id can be shortened to any prefix that is
/// unique.
/// </remarks>
public sealed class SessionResolver
{
  private readonly DebugServer _server;

  /// <summary>
  /// Creates a resolver.
  /// </summary>
  /// <param name="server">The server whose connected games are searched.</param>
  public SessionResolver(DebugServer server)
  {
    ArgumentNullException.ThrowIfNull(server);
    _server = server;
  }

  /// <summary>
  /// Finds a game without throwing.
  /// </summary>
  /// <param name="id">The session id or a unique prefix of it, or <see langword="null"/>.</param>
  /// <param name="session">The game, when found.</param>
  /// <returns>
  /// <see langword="true"/> if exactly one game matches, or if <paramref name="id"/> is empty and
  /// exactly one game is connected.
  /// </returns>
  public bool TryResolve(string? id, out GameSession session)
  {
    var live = Live();
    if (string.IsNullOrWhiteSpace(id))
    {
      session = live.Count == 1 ? live[0] : null!;
      return live.Count == 1;
    }

    var text = id.Trim();
    GameSession? match = null;
    foreach (var candidate in live)
    {
      var full = candidate.Id.ToString("D", CultureInfo.InvariantCulture);
      if (string.Equals(full, text, StringComparison.OrdinalIgnoreCase))
      {
        session = candidate;
        return true;
      }

      if (full.StartsWith(text, StringComparison.OrdinalIgnoreCase))
      {
        if (match is not null)
        {
          session = null!;
          return false;
        }

        match = candidate;
      }
    }

    session = match!;
    return match is not null;
  }

  /// <summary>
  /// Finds a game or fails with a message that tells the agent what to do.
  /// </summary>
  /// <param name="id">The session id or a unique prefix of it, or <see langword="null"/>.</param>
  /// <returns>The game.</returns>
  /// <exception cref="McpException">
  /// No game is connected, the id matches nothing, or no id was given and several games are
  /// connected.
  /// </exception>
  public GameSession Resolve(string? id)
  {
    if (TryResolve(id, out var session))
    {
      return session;
    }

    var live = Live();
    if (live.Count == 0)
    {
      throw new McpException(
          "No game is connected. Start a FlixelGDX game in debug mode and let it connect to the " +
          "debugger, then call list_games.");
    }

    var prefix = string.IsNullOrWhiteSpace(id)
        ? "Several games are connected, so the 'session' argument is required. "
        : $"No connected game matches session '{id}' (or the id is ambiguous). ";
    throw new McpException(prefix + "Connected games:\n" + Describe(live));
  }

  /// <summary>
  /// Lists the connected games, one per line, as <c>id - title (platform, device)</c>.
  /// </summary>
  /// <param name="games">The games to describe.</param>
  /// <returns>The text.</returns>
  public static string Describe(IReadOnlyList<GameSession> games)
  {
    ArgumentNullException.ThrowIfNull(games);
    var text = new StringBuilder();
    foreach (var game in games)
    {
      var hello = game.Hello;
      text.Append(game.Id.ToString("D", CultureInfo.InvariantCulture)).Append(" - ");
      text.Append(hello is null || hello.GameTitle.Length == 0 ? "Game" : hello.GameTitle);
      text.Append(" (").Append(hello?.Platform).Append(", ").Append(hello?.Device).Append(")\n");
    }

    return text.ToString().TrimEnd();
  }

  /// <summary>Gets the games that finished their handshake and are still connected.</summary>
  /// <returns>The live games.</returns>
  public List<GameSession> Live()
  {
    var live = new List<GameSession>();
    foreach (var session in _server.Sessions)
    {
      if (session.State == SessionState.Live)
      {
        live.Add(session);
      }
    }

    return live;
  }
}
