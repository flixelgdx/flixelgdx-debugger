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

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools that read general state of a game: the game list, performance, logs, watches, and the
/// console.
/// </summary>
[McpServerToolType]
internal sealed class GameTools(ToolContext ctx)
{
  private static readonly string[] _defaultStats =
      ["fps", "frameMs", "cpuMs", "draws", "objectCount", "heapMb", "paused"];

  [McpServerTool(Name = "list_games", ReadOnly = true)]
  [Description(
      "Lists the FlixelGDX games connected to the debugger. Call this first. Each game has an " +
      "id; pass it as the 'session' argument of the other tools when more than one game is " +
      "connected (with exactly one game the argument can be left out).")]
  public string ListGames()
  {
    var games = ctx.Sessions.Live();
    return Json.Object(w =>
    {
      w.WriteStartArray("games");
      foreach (var game in games)
      {
        var hello = game.Hello;
        w.WriteStartObject();
        w.WriteString("id", game.Id.ToString("D", CultureInfo.InvariantCulture));
        w.WriteString("title", hello?.GameTitle);
        w.WriteString("platform", hello?.Platform);
        w.WriteString("device", hello?.Device);
        w.WriteString("screen", $"{hello?.ScreenWidth}x{hello?.ScreenHeight}");
        w.WriteString("frameworkVersion", hello?.FrameworkVersion);
        w.WriteString("protocol", $"{hello?.ProtocolMajor}.{hello?.ProtocolMinor}");
        if (game.PingMs is { } ping)
        {
          ToolContext.WriteNumber(w, "pingMs", ping);
        }

        w.WriteBoolean("paused", game.Controls?.Paused ?? false);
        w.WriteBoolean("canScreenshot", game.CaptureSupported == true);
        w.WriteBoolean("canInput", game.InputFlags is { } flags && flags != InputSupport.None);
        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "get_stats", ReadOnly = true)]
  [Description(
      "Reads performance numbers of a game: the latest value plus min, max, and average over " +
      "the last ~6 seconds. 'fields' is a comma separated list; the default is fps, frameMs, " +
      "cpuMs, draws, objectCount, heapMb, paused. Other fields: presentMs, triangles, " +
      "vertices, passes, submits, pipelines, nativeMb, textureMb, ringKb, sendKbps, recvKbps, " +
      "elapsed, camX, camY, camZoom.")]
  public string GetStats(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      [Description("Field names: an array of strings or a comma separated string.")]
      JsonElement? fields = null)
  {
    var game = ctx.Game(session);
    var names = ToolArgs.List(fields, "fields") is { Length: > 0 } listed ? listed : _defaultStats;
    var wanted = new List<StatsField>();
    foreach (var name in names)
    {
      if (!Enum.TryParse<StatsField>(name, ignoreCase: true, out var field))
      {
        throw new McpException(
            $"Unknown stats field '{name}'. Valid fields: " +
            string.Join(", ", Enum.GetNames<StatsField>().Select(Camel)));
      }

      wanted.Add(field);
    }

    var window = new double[StatsHistory.Capacity];
    return Json.Object(w =>
    {
      w.WriteNumber("samples", game.Stats.Count);
      w.WriteStartObject("fields");
      foreach (var field in wanted)
      {
        var count = game.Stats.CopyTo(field, window);
        var min = double.MaxValue;
        var max = double.MinValue;
        var sum = 0.0;
        for (var i = 0; i < count; i++)
        {
          min = Math.Min(min, window[i]);
          max = Math.Max(max, window[i]);
          sum += window[i];
        }

        w.WriteStartObject(Camel(field.ToString()));
        ToolContext.WriteNumber(w, "latest", game.Stats.Latest(field));
        if (count > 0)
        {
          ToolContext.WriteNumber(w, "min", min);
          ToolContext.WriteNumber(w, "max", max);
          ToolContext.WriteNumber(w, "avg", sum / count);
        }

        w.WriteEndObject();
      }

      w.WriteEndObject();
    });
  }

  [McpServerTool(Name = "get_logs", ReadOnly = true)]
  [Description(
      "Reads the game's recent log lines (the debugger keeps the newest 500) regardless of the " +
      "filters the person has set in the Logs panel. Use 'sinceSeq' with the 'lastSeq' of the " +
      "previous call to read only new lines. Returns the newest 'limit' lines that match.")]
  public string GetLogs(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      [Description("Only lines at this level or above: debug, info, warn, or error.")]
      string? level = null,
      [Description("Only lines whose text contains this (ignoring case).")] string? text = null,
      [Description("Only lines with a sequence number above this. Use -1 for all.")]
      long sinceSeq = -1,
      [Description("The most lines to return (1 to 500).")] int limit = 100)
  {
    var game = ctx.Game(session);
    var minimum = LogLevel.Debug;
    if (!string.IsNullOrWhiteSpace(level) &&
        !Enum.TryParse(level, ignoreCase: true, out minimum))
    {
      throw new McpException("Unknown level. Use debug, info, warn, or error.");
    }

    limit = Math.Clamp(limit, 1, LogStore.Capacity);
    var lines = new List<LogEntry>();
    game.Logs.CopySince(sinceSeq, lines);
    var lastSeq = lines.Count > 0 ? lines[^1].Seq : sinceSeq;
    var matched = lines.Where(e =>
        (e.Line.Uncaught || e.Line.Level >= minimum) &&
        (string.IsNullOrEmpty(text) ||
            e.Line.Message.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            e.Line.Tag.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
    var shown = matched.Count > limit ? matched.GetRange(matched.Count - limit, limit) : matched;
    return Json.Object(w =>
    {
      w.WriteNumber("lastSeq", lastSeq);
      w.WriteNumber("matched", matched.Count);
      w.WriteBoolean("truncated", matched.Count > shown.Count);
      w.WriteStartArray("lines");
      foreach (var entry in shown)
      {
        w.WriteStringValue(FormatLine(entry.Line));
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "get_watches", ReadOnly = true)]
  [Description(
      "Reads the values the game exposes as watches and trackers (named live values that game " +
      "code registered for debugging).")]
  public string GetWatches(
      [Description("Game id from list_games; optional with one game.")] string? session = null)
  {
    var game = ctx.Game(session);
    return Json.Object(w =>
    {
      w.WriteStartObject("watches");
      foreach (var row in game.Watches?.Rows ?? [])
      {
        w.WriteString(row.Name, ValueFormat.EditText(row.Value));
      }

      w.WriteEndObject();
      w.WriteStartObject("trackers");
      foreach (var block in game.Trackers?.Blocks ?? [])
      {
        w.WriteStartObject(block.Name);
        foreach (var row in block.Rows)
        {
          w.WriteString(row.Name, ValueFormat.EditText(row.Value));
        }

        w.WriteEndObject();
      }

      w.WriteEndObject();
    });
  }

  [McpServerTool(Name = "list_commands", ReadOnly = true)]
  [Description("Lists the console commands the game accepts, with usage and description.")]
  public string ListCommands(
      [Description("Game id from list_games; optional with one game.")] string? session = null)
  {
    var game = ctx.Game(session);
    return Json.Object(w =>
    {
      w.WriteStartArray("commands");
      foreach (var command in game.ConsoleCommands?.Commands ?? [])
      {
        w.WriteStartObject();
        w.WriteString("name", command.Name);
        w.WriteString("usage", command.Usage);
        w.WriteString("description", command.Description);
        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "run_command")]
  [Description(
      "Runs one console command in the game (see list_commands) and returns its status and the " +
      "log lines printed meanwhile. Output pairing is best effort: if a person runs commands " +
      "at the same moment, their lines can show up too.")]
  public async Task<string> RunCommand(
      [Description("The console line, at most 500 characters.")] string command,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    if (string.IsNullOrWhiteSpace(command) || command.Length > ProtocolConstants.MaxCommandChars)
    {
      throw new McpException(
          $"The command must be 1 to {ProtocolConstants.MaxCommandChars} characters long.");
    }

    var lines = new List<LogEntry>();
    game.Logs.CopySince(-1, lines);
    var mark = lines.Count > 0 ? lines[^1].Seq : -1;

    var result = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Console,
        () => game.RunCommandAsync(command, cancellationToken),
        static p => p is ConsoleResultPayload,
        TimeSpan.FromSeconds(5),
        cancellationToken).ConfigureAwait(false) as ConsoleResultPayload
        ?? throw new McpException("The game did not answer the command in time.");

    // Command output travels through the logs, which the game sends in batches every 0.1 seconds.
    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
    game.Logs.CopySince(mark, lines);
    return Json.Object(w =>
    {
      w.WriteString("status", result.Status switch
      {
        ConsoleStatus.Ran => "ran",
        ConsoleStatus.Failed => "failed",
        _ => "unknown command",
      });
      w.WriteString("message", result.Message);
      w.WriteStartArray("output");
      foreach (var entry in lines)
      {
        w.WriteStringValue(FormatLine(entry.Line));
      }

      w.WriteEndArray();
    });
  }

  private static string FormatLine(LogLine line)
  {
    var text = string.Create(
        CultureInfo.InvariantCulture,
        $"#{line.Seq} {line.Time:0.000} {LogStore.LevelName(line.Level)} [{line.Tag}] " +
        $"{line.Message}");
    return line.Stack.Length > 0 ? text + "\n" + line.Stack : text;
  }

  private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
