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
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol.Panels;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools that return pictures: a screenshot of the game and thumbnails of textures and objects.
/// </summary>
[McpServerToolType]
internal sealed class ScreenTools(ToolContext ctx)
{
  private static readonly TimeSpan _thumbnailTimeout = TimeSpan.FromSeconds(5);

  [McpServerTool(Name = "screenshot", ReadOnly = true)]
  [Description(
      "Takes a screenshot of the game as a PNG image: the finished frame, including the game's " +
      "own overlay but not the debugger's hitbox or selection outlines. Works while paused. " +
      "'maxSide' limits the longest side in pixels (64 to 2048, default 1024); big pictures " +
      "cost many tokens. Needs a game on protocol 1.1 or newer.")]
  public async Task<CallToolResult> Screenshot(
      [Description("The longest side of the picture in pixels (64 to 2048).")]
      int maxSide = 1024,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    ToolContext.RequirePanel(game, BuiltInPanels.Capture, "screenshots");
    if (game.CaptureSupported == false)
    {
      throw new McpException(
          "This game's graphics backend cannot read the finished frame back, so screenshots " +
          "are not available.");
    }

    maxSide = Math.Clamp(maxSide, 64, 2048);
    var requestId = ctx.NextRequestId();
    var image = await ToolContext.ActAsync(
        game,
        BuiltInPanels.Capture,
        () => game.RequestCaptureAsync(requestId, (ushort)maxSide, cancellationToken),
        p => p is CaptureImagePayload i && i.RequestId == requestId,
        ctx.CaptureTimeout,
        cancellationToken).ConfigureAwait(false) as CaptureImagePayload
        ?? throw new McpException(
            "The game did not send a screenshot in time. It may have failed to read the frame, " +
            "or another capture was already running. Try again.");
    return Picture(image.Png, image.Width, image.Height);
  }

  [McpServerTool(Name = "list_textures", ReadOnly = true)]
  [Description(
      "Lists the textures loaded by the game (id, name, size, GPU memory, how many objects use " +
      "it). Use an id with get_thumbnail.")]
  public string ListTextures(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      [Description("The most textures to return (1 to 500).")] int limit = 100)
  {
    var game = ctx.Game(session);
    var textures = game.Textures ?? throw new McpException(
        "The game has not sent its texture list yet.");
    limit = Math.Clamp(limit, 1, 500);
    return Json.Object(w =>
    {
      w.WriteNumber("textureCount", textures.TextureCount);
      ToolContext.WriteNumber(w, "gpuMb", textures.TotalGpuBytes / (1024.0 * 1024.0));
      w.WriteBoolean("truncated", textures.Entries.Count > limit);
      w.WriteStartArray("textures");
      foreach (var entry in textures.Entries.Take(limit))
      {
        w.WriteStartObject();
        w.WriteNumber("id", entry.Id);
        w.WriteString("name", entry.Name);
        w.WriteString("size", $"{entry.Width}x{entry.Height}");
        w.WriteString("format", entry.Format);
        w.WriteNumber("users", entry.Users);
        if (entry.Duplicate)
        {
          w.WriteBoolean("duplicate", true);
        }

        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "get_thumbnail", ReadOnly = true)]
  [Description(
      "Returns a small PNG (at most 256 pixels) of a texture's pixels (kind 'texture', id from " +
      "list_textures) or of the part of the texture a sprite shows (kind 'object', id from " +
      "get_scene). It is the texture, not what is drawn on screen; use screenshot for that.")]
  public async Task<CallToolResult> GetThumbnail(
      [Description("The texture id or the object id.")] int id,
      [Description("'texture' or 'object'.")] string kind = "object",
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var texture = string.Equals(kind, "texture", StringComparison.OrdinalIgnoreCase);
    if (!texture && !string.Equals(kind, "object", StringComparison.OrdinalIgnoreCase))
    {
      throw new McpException("'kind' must be 'texture' or 'object'.");
    }

    Thumbnail found;
    if (texture)
    {
      var got = await ToolContext.ActAsync(
          game,
          BuiltInPanels.Textures,
          () => game.RequestThumbnailAsync(id, cancellationToken),
          p => p is TexturesThumbnailPayload t && t.Id == id,
          _thumbnailTimeout,
          cancellationToken).ConfigureAwait(false) as TexturesThumbnailPayload;
      found = got is null
          ? default
          : new Thumbnail(got.Id, got.Width, got.Height, got.Png);
    }
    else
    {
      var got = await ToolContext.ActAsync(
          game,
          BuiltInPanels.Inspector,
          () => game.RequestInspectorThumbnailAsync(id, cancellationToken),
          p => p is InspectorThumbnailPayload t && t.Id == id,
          _thumbnailTimeout,
          cancellationToken).ConfigureAwait(false) as InspectorThumbnailPayload;
      found = got is null
          ? default
          : new Thumbnail(got.Id, got.Width, got.Height, got.Png);
    }

    if (found.Png.IsEmpty)
    {
      throw new McpException(
          "The game did not send a thumbnail. The id may be unknown, the object may have no " +
          "texture, or the graphics backend cannot read pixels back.");
    }

    return Picture(found.Png, found.Width, found.Height);
  }

  private static CallToolResult Picture(ReadOnlyMemory<byte> png, int width, int height) => new()
  {
    Content =
    [
      ImageContentBlock.FromBytes(png, "image/png"),
      new TextContentBlock
      {
        Text = string.Create(CultureInfo.InvariantCulture, $"{width}x{height} PNG"),
      },
    ],
  };
}
