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
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol.Feeds;
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
      "cost many tokens. Several screenshots at once are handled one after another. Needs a " +
      "game with the capture feed.")]
  public async Task<CallToolResult> Screenshot(
      [Description("The longest side of the picture in pixels (64 to 2048).")]
      int maxSide = 1024,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    ToolContext.RequireFeed(game, BuiltInFeeds.Capture, "screenshots");
    if (game.CaptureSupported == false)
    {
      throw new McpException(
          "This game's graphics backend cannot read the finished frame back, so screenshots " +
          "are not available.");
    }

    maxSide = Math.Clamp(maxSide, 64, 2048);

    // The game keeps one open capture and drops the rest, so callers take turns. A caller that
    // waited in line can reuse a picture that finished after it asked.
    var asked = Stopwatch.GetTimestamp();
    var gate = ctx.GateFor(game);
    if (!await gate.Lock.WaitAsync(ctx.CaptureTimeout, cancellationToken).ConfigureAwait(false))
    {
      throw new McpException(
          "Other screenshots of this game were still running. Try again in a moment.");
    }

    CaptureImagePayload image;
    try
    {
      if (gate.Last is { } shared && gate.LastDone >= asked && gate.LastMaxSide == maxSide)
      {
        image = shared;
      }
      else
      {
        var requestId = ctx.NextRequestId();
        image = await ToolContext.ActAsync(
            game,
            BuiltInFeeds.Capture,
            () => game.RequestCaptureAsync(requestId, (ushort)maxSide, cancellationToken),
            p => p is CaptureImagePayload i && i.RequestId == requestId,
            ctx.CaptureTimeout,
            cancellationToken).ConfigureAwait(false) as CaptureImagePayload
            ?? throw new McpException(
                "The game did not send a screenshot in time. It may have failed to read the " +
                "frame. Try again.");
        gate.Last = image;
        gate.LastDone = Stopwatch.GetTimestamp();
        gate.LastMaxSide = maxSide;
      }
    }
    finally
    {
      gate.Lock.Release();
    }

    return await PictureAsync(image.Png, image.Width, image.Height).ConfigureAwait(false);
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
      "list_textures) or of the part of the texture a sprite shows (kind 'object', an object " +
      "id or scene path from get_scene). It is the texture, not what is drawn on screen; use " +
      "screenshot for that.")]
  public async Task<CallToolResult> GetThumbnail(
      [Description("The texture id, or the object id or scene path (a number or a string).")]
      JsonElement id,
      [Description("'texture' or 'object'.")] string kind = "object",
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var text = ToolArgs.Text(id, "id");
    var texture = string.Equals(kind, "texture", StringComparison.OrdinalIgnoreCase);
    if (!texture && !string.Equals(kind, "object", StringComparison.OrdinalIgnoreCase))
    {
      throw new McpException("Argument 'kind' must be the string 'texture' or 'object'.");
    }

    int number;
    if (texture)
    {
      if (!SceneAddress.TryParseId(text, out number))
      {
        throw new McpException(
            "Argument 'id' must be a texture id (a whole number from list_textures).");
      }
    }
    else if (!SceneAddress.TryResolveId(game.Scene, text, out number))
    {
      throw new McpException(
          $"No object matches '{text}'. Use a numeric id or a scene path from get_scene.");
    }
    else if (game.Inspector is { } state && state.SelectedId == number && !state.HasTexture)
    {
      // The game never answers for an object without a texture (text, for example), so say so
      // now instead of waiting for the timeout.
      throw new McpException(
          $"Object {number} has no texture to show (for example, text is drawn from a font), " +
          "so the game cannot make a thumbnail. Use screenshot to see it on screen.");
    }

    Thumbnail? found;
    if (texture)
    {
      var got = await ToolContext.ActAsync(
          game,
          BuiltInFeeds.Textures,
          () => game.RequestThumbnailAsync(number, cancellationToken),
          p => p is TexturesThumbnailPayload t && t.Id == number,
          _thumbnailTimeout,
          cancellationToken).ConfigureAwait(false) as TexturesThumbnailPayload;
      found = got is null ? null : new Thumbnail(got.Id, got.Width, got.Height, got.Png);
    }
    else
    {
      var got = await ToolContext.ActAsync(
          game,
          BuiltInFeeds.Inspector,
          () => game.RequestInspectorThumbnailAsync(number, cancellationToken),
          p => p is InspectorThumbnailPayload t && t.Id == number,
          _thumbnailTimeout,
          cancellationToken).ConfigureAwait(false) as InspectorThumbnailPayload;
      found = got is null ? null : new Thumbnail(got.Id, got.Width, got.Height, got.Png);
    }

    if (found is not { } picture)
    {
      // Games before protocol 1.1 send nothing at all when they cannot make a picture.
      throw new McpException(
          "The game did not send a thumbnail in time. The id may be unknown, the object may " +
          "have no texture, or the graphics backend cannot read pixels back.");
    }

    if (picture.Png.IsEmpty)
    {
      throw new McpException(texture
          ? NoTextureThumbnail(game, number)
          : NoObjectThumbnail(game, number));
    }

    return await PictureAsync(picture.Png, picture.Width, picture.Height).ConfigureAwait(false);
  }

  // The game answers with an empty thumbnail when it cannot make one, so say why as well as the
  // tool can tell from what it knows.
  private static string NoTextureThumbnail(GameSession game, int id)
  {
    var entry = game.Textures?.Entries.FirstOrDefault(e => e.Id == id);
    if (entry is not { Name: not null } found)
    {
      return $"The game has no texture with id {id}. Use an id from list_textures.";
    }

    return $"The game could not read texture {id} ('{found.Name}', {found.Format}) back. " +
        "Compressed textures cannot be read, and some graphics backends cannot read any.";
  }

  private static string NoObjectThumbnail(GameSession game, int id)
  {
    if (!game.Scene.TryGetNode(id, out var node))
    {
      return $"The game has no picture for object {id}. The id may be unknown, or the object " +
          "is not a sprite with a texture.";
    }

    return $"Object {id} ({node.Type}) has no texture to show, so the game cannot make a " +
        "thumbnail. Text and other objects drawn without a sprite frame have none, " +
        "compressed textures cannot be read, and some graphics backends cannot read any. Use " +
        "screenshot to see it on screen.";
  }

  // The game writes PNGs with uncompressed deflate blocks. Compressing them properly makes the
  // picture several times smaller, which matters because an agent pays for every byte.
  private static async Task<CallToolResult> PictureAsync(
      ReadOnlyMemory<byte> png, int width, int height)
  {
    var small = await Task.Run(() => PngRecompressor.Shrink(png)).ConfigureAwait(false);
    return new CallToolResult
    {
      Content =
      [
        ImageContentBlock.FromBytes(small, "image/png"),
        new TextContentBlock
        {
          Text = string.Create(
              CultureInfo.InvariantCulture,
              $"{width}x{height} PNG, {Size(png.Length)} -> {Size(small.Length)}"),
        },
      ],
    };
  }

  private static string Size(int bytes) => bytes >= 1024 * 1024
      ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.0} MB")
      : string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.0} KB");
}
