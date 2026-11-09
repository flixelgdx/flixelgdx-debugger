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
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol.Feeds;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools for the game's save data.
/// </summary>
[McpServerToolType]
internal sealed class DataTools(ToolContext ctx)
{
  [McpServerTool(Name = "get_save", ReadOnly = true)]
  [Description(
      "Reads the game's save data as one row per value: a dotted path, the value as text, and " +
      "whether it has unsaved changes. Also lists paths that were removed but not saved yet. " +
      "Nothing reaches disk until save_now.")]
  public string GetSave(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      [Description("Only paths that contain this text (ignoring case).")] string? filter = null,
      [Description("The most rows to return (1 to 300).")] int limit = 100)
  {
    var game = ctx.Game(session);
    var save = game.Save ?? throw new McpException("The game has not sent its save data yet.");
    limit = Math.Clamp(limit, 1, 300);
    var rows = SaveListing.Build(save, filter);
    return Json.Object(w =>
    {
      w.WriteString("status", save.Status);
      w.WriteBoolean("isError", save.IsError);
      w.WriteNumber("selected", save.Selected);
      w.WriteStartArray("saves");
      foreach (var name in save.Saves)
      {
        w.WriteStringValue(name);
      }

      w.WriteEndArray();
      w.WriteNumber("matched", rows.Count);
      w.WriteBoolean("truncated", rows.Count > limit);
      w.WriteStartArray("rows");
      foreach (var row in rows.Take(limit))
      {
        w.WriteStartObject();
        w.WriteString("path", row.Path);
        w.WriteString("value", row.Display);
        if (row.Unsaved)
        {
          w.WriteBoolean("unsaved", true);
        }

        if (row.Removed)
        {
          w.WriteBoolean("removed", true);
        }

        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "set_save_value")]
  [Description(
      "Changes an existing save value and keeps its type (a string may be typed with or " +
      "without double quotes, a boolean must be true or false, a number is parsed as the " +
      "type the value already has). The value only reaches disk after save_now. Returns the " +
      "game's status line.")]
  public async Task<string> SetSaveValue(
      [Description("The dotted path from get_save, for example player.coins.")] string path,
      [Description("The new value as text (at most 200 characters).")] string text,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var save = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Save,
        () => game.SaveSetAsync(path, text, cancellationToken),
        static p => p is SavePayload,
        cancellationToken: cancellationToken).ConfigureAwait(false) as SavePayload ?? game.Save;
    return StatusText(save);
  }

  [McpServerTool(Name = "save_now")]
  [Description("Writes the game's unsaved changes to storage now. Returns the status line.")]
  public async Task<string> SaveNow(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var save = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Save,
        () => game.SaveNowAsync(cancellationToken),
        static p => p is SavePayload,
        cancellationToken: cancellationToken).ConfigureAwait(false) as SavePayload ?? game.Save;
    return StatusText(save);
  }

  private static string StatusText(SavePayload? save) => Json.Object(w =>
  {
    w.WriteString("status", save?.Status);
    w.WriteBoolean("isError", save?.IsError ?? false);
  });
}
