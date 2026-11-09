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
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Feeds;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FlixelDebugger.Mcp.Tools;

/// <summary>
/// Tools that press keys, move and click a pointer, scroll, and type text in the game.
/// </summary>
/// <remarks>
/// The game queues injected input and applies one key or button change per frame, so a tap takes
/// two frames. While the game is paused the queue waits for step or resume. Input needs a game with
/// the input feed.
/// </remarks>
[McpServerToolType]
internal sealed class InputTools(ToolContext ctx)
{
  private const string Queued = "queued; the game applies one key or button change per frame";

  [McpServerTool(Name = "press_key")]
  [Description(
      "Taps a key: press, then release, which takes two frames. The key is a name such as A, " +
      "SPACE, ENTER, ESCAPE, UP, LEFT, SHIFT, F1 or a raw framework key code. If the game is " +
      "paused, call step afterwards so the game sees it.")]
  public async Task<string> PressKey(
      [Description("The key name, for example SPACE.")] string key,
      [Description("How many times to tap it (1 to 20).")] int count = 1,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Key, "key input");
    var code = Code(key);
    count = Math.Clamp(count, 1, 20);
    for (var i = 0; i < count; i++)
    {
      await ToolContext.SendAsync(
          game, BuiltInFeeds.Input, () => game.InputKeyAsync(true, code, cancellationToken))
          .ConfigureAwait(false);
      await ToolContext.SendAsync(
          game, BuiltInFeeds.Input, () => game.InputKeyAsync(false, code, cancellationToken))
          .ConfigureAwait(false);
    }

    return Queued;
  }

  [McpServerTool(Name = "key_down")]
  [Description(
      "Presses and holds a key until key_up is called (or the debugger disconnects, which " +
      "releases everything). Use it to walk a character, then step or wait.")]
  public async Task<string> KeyDown(
      [Description("The key name, for example RIGHT.")] string key,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Key, "key input");
    var code = Code(key);
    await ToolContext.SendAsync(
        game, BuiltInFeeds.Input, () => game.InputKeyAsync(true, code, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  [McpServerTool(Name = "key_up")]
  [Description("Releases a key that key_down is holding.")]
  public async Task<string> KeyUp(
      [Description("The key name, for example RIGHT.")] string key,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Key, "key input");
    var code = Code(key);
    await ToolContext.SendAsync(
        game, BuiltInFeeds.Input, () => game.InputKeyAsync(false, code, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  [McpServerTool(Name = "click")]
  [Description(
      "Clicks at a point of the view (press, then release, two frames). The position is in " +
      "view units of the camera the viewport panel shows (see set_camera): 0,0 is the top " +
      "left of what that camera shows, the same space as get_viewport boxes. User interface " +
      "widgets are usually on their own camera, so a point on the game camera does not hit " +
      "them. To press a UI button use click_object with its id instead.")]
  public async Task<string> Click(
      [Description("The x position in view units.")] float x,
      [Description("The y position in view units.")] float y,
      [Description("'left' (default), 'right', or 'middle'.")] string button = "left",
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Pointer, "pointer input");
    var code = Button(button);
    await ToolContext.SendAsync(
        game,
        BuiltInFeeds.Input,
        () => game.InputPointerAsync(InputPointerAction.Down, code, x, y, cancellationToken))
        .ConfigureAwait(false);
    await ToolContext.SendAsync(
        game,
        BuiltInFeeds.Input,
        () => game.InputPointerAsync(InputPointerAction.Up, code, x, y, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  [McpServerTool(Name = "pointer")]
  [Description(
      "Moves the pointer, or presses or releases a button at a point of the view. Coordinates " +
      "are in view units of the camera the viewport panel shows (see click and set_camera). " +
      "Use it for hovering and dragging: move, down, move, up. The pointer stays where it " +
      "was put until the real mouse moves.")]
  public async Task<string> Pointer(
      [Description("'move', 'down', or 'up'.")] string phase,
      [Description("The x position in view units.")] float x,
      [Description("The y position in view units.")] float y,
      [Description("'left' (default), 'right', or 'middle'.")] string button = "left",
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Pointer, "pointer input");
    var action = phase?.Trim().ToLowerInvariant() switch
    {
      "move" => InputPointerAction.Move,
      "down" => InputPointerAction.Down,
      "up" => InputPointerAction.Up,
      _ => throw new McpException("'phase' must be 'move', 'down', or 'up'."),
    };
    var code = Button(button);
    await ToolContext.SendAsync(
        game,
        BuiltInFeeds.Input,
        () => game.InputPointerAsync(action, code, x, y, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  [McpServerTool(Name = "scroll")]
  [Description("Turns the mouse wheel. A positive amount scrolls down.")]
  public async Task<string> Scroll(
      [Description("The wheel amount; positive scrolls down.")] float amount,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Scroll, "scroll input");
    await ToolContext.SendAsync(
        game, BuiltInFeeds.Input, () => game.InputScrollAsync(amount, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  [McpServerTool(Name = "click_object")]
  [Description(
      "Clicks the center of an object's box. This is the main way to press UI buttons: find " +
      "the widget with get_scene and pass its id or scene path. User interface widgets usually " +
      "live on their own camera; the game looks for the object on its own camera, so you do " +
      "not need to switch cameras first. Does nothing when the object is not drawn at all. An " +
      "id that is not in the scene tree is an error.")]
  public async Task<string> ClickObject(
      [Description(
          "The object: a numeric id (preferred) or a scene path like state.3.0. A number or " +
          "a string.")]
      JsonElement target,
      [Description("'left' (default), 'right', or 'middle'.")] string button = "left",
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Pointer, "pointer input");
    var text = ToolArgs.Text(target, "target");
    if (!SceneAddress.TryResolveId(game.Scene, text, out var id) ||
        !game.Scene.TryGetNode(id, out _))
    {
      throw new McpException(
          $"No object '{text}' is in the scene tree, so nothing was clicked. Use a numeric id " +
          "or a scene path from get_scene (pass refresh=true if the scene just changed).");
    }

    var code = Button(button);
    await ToolContext.SendAsync(
        game,
        BuiltInFeeds.Input,
        () => game.InputClickObjectAsync(id, code, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  [McpServerTool(Name = "type_text")]
  [Description(
      "Types text into the game as keyboard characters, so a focused text box receives it. It " +
      "presses no keys: use press_key for ENTER, BACKSPACE, and arrows. Text longer than 1000 " +
      "characters is cut.")]
  public async Task<string> TypeText(
      [Description("The text to type.")] string text,
      [Description("Game id from list_games; optional with one game.")] string? session = null,
      CancellationToken cancellationToken = default)
  {
    var game = Require(session, InputSupport.Text, "text input");
    var typed = text ?? string.Empty;
    await ToolContext.SendAsync(
        game, BuiltInFeeds.Input, () => game.InputTextAsync(typed, cancellationToken))
        .ConfigureAwait(false);
    return Queued;
  }

  private GameSession Require(string? session, InputSupport needed, string what)
  {
    var game = ctx.Game(session);
    ToolContext.RequireFeed(game, BuiltInFeeds.Input, what);
    if (game.InputFlags is { } flags && (flags & needed) == 0)
    {
      throw new McpException($"This game does not support {what}.");
    }

    return game;
  }

  private static int Code(string key) => KeyNames.TryGet(key, out var code)
      ? code
      : throw new McpException($"Unknown key '{key}'. Examples: {KeyNames.Examples()}.");

  private static byte Button(string? button) => button?.Trim().ToLowerInvariant() switch
  {
    null or "" or "left" or "0" => 0,
    "right" or "1" => 1,
    "middle" or "2" => 2,
    "back" or "3" => 3,
    "forward" or "4" => 4,
    _ => throw new McpException("'button' must be left, right, or middle."),
  };
}
