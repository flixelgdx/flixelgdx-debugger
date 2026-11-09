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
using System.Text.Json;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol.Feeds;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools for the viewport (a live map of what one camera shows) and for the game controls
/// (pause, step, hitboxes, free camera).
/// </summary>
/// <remarks>
/// Positions are in the <b>view space</b> of the camera: 0, 0 is the top left corner of what the
/// camera shows. They are not world coordinates.
/// </remarks>
[McpServerToolType]
internal sealed class ViewportTools(ToolContext ctx)
{
  private const int MaxSteps = 300;

  [McpServerTool(Name = "get_viewport", ReadOnly = true)]
  [Description(
      "Reads the viewport: the camera (size, zoom, scroll, which camera) and a box for every " +
      "visible object in view coordinates (0,0 is the top left of what the camera shows). " +
      "Boxes are the objects' hitboxes in draw order, bottom first; when there are more than " +
      "'limit', the ones drawn on top are kept. A box's 'id' is the same id get_scene and " +
      "inspect use. The viewport shows ONE camera (see 'camera' and 'cameraCount'). User " +
      "interface widgets are usually on their own camera, so switch with set_camera to see " +
      "their boxes. Click and pointer coordinates are in this camera's view space.")]
  public string GetViewport(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      [Description("The most boxes to return (1 to 1024).")] int limit = 100)
  {
    var game = ctx.Game(session);
    var viewport = game.Viewport ?? throw new McpException(
        "The game has not sent a viewport yet. Try again in a moment.");
    limit = Math.Clamp(limit, 1, 1024);
    var skip = Math.Max(0, viewport.Boxes.Count - limit);
    return Json.Object(w =>
    {
      WriteCamera(w, viewport);
      w.WriteNumber("boxTotal", viewport.Boxes.Count);
      w.WriteBoolean("truncated", skip > 0);
      w.WriteStartArray("boxes");
      for (var i = skip; i < viewport.Boxes.Count; i++)
      {
        var box = viewport.Boxes[i];
        w.WriteStartObject();
        w.WriteNumber("id", box.Id);
        w.WriteString("type", viewport.TypeOf(box));
        ToolContext.WriteNumber(w, "x", box.X);
        ToolContext.WriteNumber(w, "y", box.Y);
        ToolContext.WriteNumber(w, "w", box.Width);
        ToolContext.WriteNumber(w, "h", box.Height);
        if (box.IsGroup)
        {
          w.WriteBoolean("group", true);
        }

        if (box.IsInactive)
        {
          w.WriteBoolean("inactive", true);
        }

        if (box.IsSelected)
        {
          w.WriteBoolean("selected", true);
        }

        w.WriteEndObject();
      }

      w.WriteEndArray();
    });
  }

  [McpServerTool(Name = "viewport_pick")]
  [Description(
      "Selects the topmost object whose hitbox contains a point of the view, in the inspector " +
      "(like Alt + Click), and returns it. Clears the selection when nothing is there. Use " +
      "get_viewport for coordinates.")]
  public async Task<string> ViewportPick(
      [Description("The x position in view units.")] float x,
      [Description("The y position in view units.")] float y,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var viewport = await Act(
        game, () => game.ViewportPickAsync(x, y, cancellationToken), cancellationToken)
        .ConfigureAwait(false);
    return Json.Object(w =>
    {
      w.WriteNumber("selectedId", viewport?.SelectedId ?? -1);
      var box = viewport?.Boxes.FirstOrDefault(b => b.IsSelected);
      if (viewport is not null && box is { } selected && selected.IsSelected)
      {
        w.WriteString("type", viewport.TypeOf(selected));
      }
    });
  }

  [McpServerTool(Name = "move_object")]
  [Description(
      "Moves a game object by a distance in world units (not to a position). A group moves its " +
      "members with it. Returns the new box of the object when it is visible on the viewport's " +
      "camera; otherwise the move is sent and reported as unconfirmed. A zero move does " +
      "nothing.")]
  public async Task<string> MoveObject(
      [Description("The object's numeric id from get_scene or get_viewport.")] int id,
      [Description("The horizontal distance; positive moves right.")] float dx,
      [Description("The vertical distance; positive moves down.")] float dy,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    if (dx == 0f && dy == 0f)
    {
      return Json.Object(w =>
      {
        w.WriteNumber("id", id);
        w.WriteBoolean("sent", false);
        w.WriteString("note", "A zero move changes nothing, so nothing was sent.");
      });
    }

    // No box on the viewport's camera means the game will not report any change, so do not wait
    // for one.
    if (game.Viewport?.Boxes.Any(b => b.Id == id) != true)
    {
      await ToolContext.SendAsync(
          game, BuiltInFeeds.Viewport, () => game.ViewportMoveAsync(id, dx, dy, cancellationToken))
          .ConfigureAwait(false);
      return Json.Object(w =>
      {
        w.WriteNumber("id", id);
        w.WriteBoolean("sent", true);
        w.WriteBoolean("confirmed", false);
        w.WriteString(
            "note",
            "The object has no box on the viewport's camera, so the game cannot report the " +
            "move. It was sent; check with inspect.");
      });
    }

    var viewport = await Act(
        game, () => game.ViewportMoveAsync(id, dx, dy, cancellationToken), cancellationToken)
        .ConfigureAwait(false);
    return Json.Object(w =>
    {
      w.WriteNumber("id", id);
      var box = viewport?.Boxes.FirstOrDefault(b => b.Id == id);
      if (box is { } found && found.Id == id)
      {
        ToolContext.WriteNumber(w, "x", found.X);
        ToolContext.WriteNumber(w, "y", found.Y);
      }
      else
      {
        w.WriteString("note", "The object has no box on this camera right now.");
      }
    });
  }

  [McpServerTool(Name = "pan_camera")]
  [Description(
      "Moves the viewport's camera by a distance in view units (positive dx moves the camera " +
      "right, so the world slides left). This turns the free camera on; reset_camera undoes it.")]
  public Task<string> PanCamera(
      [Description("The horizontal distance in view units.")] float dx,
      [Description("The vertical distance in view units.")] float dy,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default) =>
      CameraAction(session, g => g.ViewportPanAsync(dx, dy, cancellationToken), cancellationToken);

  [McpServerTool(Name = "zoom_camera")]
  [Description(
      "Multiplies the camera's zoom by 'factor' (1.25 zooms in, 0.8 zooms out; the zoom stays " +
      "between 0.1 and 10) and keeps one point of the view still. Turns the free camera on; " +
      "reset_camera undoes it.")]
  public Task<string> ZoomCamera(
      [Description("The zoom multiplier; it must be above 0.")] float factor,
      [Description("The x of the point to keep still, in view units. Default: the center.")]
      float? anchorX = null,
      [Description("The y of the point to keep still, in view units. Default: the center.")]
      float? anchorY = null,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var view = game.Viewport;
    var ax = anchorX ?? (view?.ViewWidth ?? 0f) / 2f;
    var ay = anchorY ?? (view?.ViewHeight ?? 0f) / 2f;
    return CameraAction(
        session, g => g.ViewportZoomAsync(factor, ax, ay, cancellationToken), cancellationToken);
  }

  [McpServerTool(Name = "reset_camera")]
  [Description(
      "Turns the free camera off, which restores the camera's scroll, zoom, and follow target " +
      "exactly as they were.")]
  public Task<string> ResetCamera(
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default) =>
      CameraAction(session, g => g.ViewportResetCameraAsync(cancellationToken), cancellationToken);

  [McpServerTool(Name = "set_camera")]
  [Description(
      "Chooses which camera the viewport shows and the camera tools act on (a position in the " +
      "game's camera list; 0 is the main camera). get_viewport reports 'cameraCount'.")]
  public async Task<string> SetCamera(
      [Description("The camera index.")] int index,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    if (index is < 0 or > 254)
    {
      throw new McpException("The camera index must be from 0 to 254.");
    }

    var game = ctx.Game(session);
    var count = game.Viewport?.CameraCount ?? 0;
    // An index past the end of the camera list falls back to the main camera.
    var expected = index < count ? index : 0;
    var current = game.Viewport;
    ViewportPayload? viewport;
    if (current is not null && current.Camera == expected)
    {
      await ToolContext.SendAsync(
          game,
          BuiltInFeeds.Viewport,
          () => game.ViewportSetCameraAsync((byte)index, cancellationToken)).ConfigureAwait(false);
      viewport = current;
    }
    else
    {
      viewport = await ToolContext.ActAsync(
          game,
          BuiltInFeeds.Viewport,
          () => game.ViewportSetCameraAsync((byte)index, cancellationToken),
          p => p is ViewportPayload v && v.Camera == (index < v.CameraCount ? index : 0),
          cancellationToken: cancellationToken).ConfigureAwait(false) as ViewportPayload
          ?? game.Viewport;
    }

    return Json.Object(w =>
    {
      if (viewport is not null)
      {
        WriteCamera(w, viewport);
      }
    });
  }

  [McpServerTool(Name = "set_paused")]
  [Description(
      "Pauses or resumes the game. While paused, injected input waits until you call step or " +
      "resume. Returns the state the game reports.")]
  public async Task<string> SetPaused(
      [Description("true to pause, false to resume.")] bool paused,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var controls = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Controls,
        () => game.SetPausedAsync(paused, cancellationToken),
        p => p is ControlsPayload c && c.Paused == paused,
        cancellationToken: cancellationToken).ConfigureAwait(false) as ControlsPayload;
    return ControlsText(controls ?? game.Controls);
  }

  [McpServerTool(Name = "step")]
  [Description(
      "Advances a paused game by 'frames' frames (1 to 300). Call set_paused with true first; " +
      "stepping a running game does nothing useful. Useful for frame-exact checks with injected " +
      "input.")]
  public async Task<string> Step(
      [Description("How many frames to advance.")] int frames = 1,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    frames = Math.Clamp(frames, 1, MaxSteps);
    for (var i = 0; i < frames; i++)
    {
      await ToolContext.SendAsync(
          game, BuiltInFeeds.Controls, () => game.StepAsync(cancellationToken))
          .ConfigureAwait(false);
      // One STEP advances one frame, so give the game time to run it before the next one.
      await Task.Delay(40, cancellationToken).ConfigureAwait(false);
    }

    return ControlsText(game.Controls);
  }

  [McpServerTool(Name = "set_hitboxes")]
  [Description("Shows or hides the hitbox outlines in the game window.")]
  public async Task<string> SetHitboxes(
      [Description("true to show hitboxes.")] bool enabled,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var controls = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Controls,
        () => game.SetHitboxesAsync(enabled, cancellationToken),
        p => p is ControlsPayload c && c.Hitboxes == enabled,
        cancellationToken: cancellationToken).ConfigureAwait(false) as ControlsPayload;
    return ControlsText(controls ?? game.Controls);
  }

  [McpServerTool(Name = "set_freecam")]
  [Description(
      "Turns the debugger's free camera on or off. When on, the camera stops following its " +
      "target and can be flown with pan_camera and zoom_camera.")]
  public async Task<string> SetFreecam(
      [Description("true to turn the free camera on.")] bool enabled,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = ctx.Game(session);
    var controls = await ToolContext.ActAsync(
        game,
        BuiltInFeeds.Controls,
        () => game.SetFreeCamAsync(enabled, cancellationToken),
        p => p is ControlsPayload c && c.FreeCam == enabled,
        cancellationToken: cancellationToken).ConfigureAwait(false) as ControlsPayload;
    return ControlsText(controls ?? game.Controls);
  }

  private static string ControlsText(ControlsPayload? controls) => Json.Object(w =>
  {
    w.WriteBoolean("paused", controls?.Paused ?? false);
    w.WriteBoolean("hitboxes", controls?.Hitboxes ?? false);
    w.WriteBoolean("freeCam", controls?.FreeCam ?? false);
  });

  private static void WriteCamera(Utf8JsonWriter w, ViewportPayload view)
  {
    w.WriteNumber("camera", view.Camera);
    w.WriteNumber("cameraCount", view.CameraCount);
    ToolContext.WriteNumber(w, "viewWidth", view.ViewWidth);
    ToolContext.WriteNumber(w, "viewHeight", view.ViewHeight);
    ToolContext.WriteNumber(w, "zoom", view.Zoom);
    ToolContext.WriteNumber(w, "scrollX", view.ScrollX);
    ToolContext.WriteNumber(w, "scrollY", view.ScrollY);
    w.WriteBoolean("freeCam", view.FreeCam);
    w.WriteBoolean("paused", view.Paused);
    w.WriteBoolean("hitboxes", view.Hitboxes);
    w.WriteNumber("selectedId", view.SelectedId);
  }

  private static async Task<ViewportPayload?> Act(
      GameSession game, Func<Task<bool>> send, CancellationToken cancellationToken) =>
      await ToolContext.ActAsync(
          game,
          BuiltInFeeds.Viewport,
          send,
          static p => p is ViewportPayload,
          cancellationToken: cancellationToken).ConfigureAwait(false) as ViewportPayload
      ?? game.Viewport;

  private async Task<string> CameraAction(
      string? session, Func<GameSession, Task<bool>> send, CancellationToken cancellationToken)
  {
    var game = ctx.Game(session);
    var viewport = await Act(game, () => send(game), cancellationToken).ConfigureAwait(false);
    return Json.Object(w =>
    {
      if (viewport is not null)
      {
        WriteCamera(w, viewport);
      }
    });
  }
}
