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

using System.Diagnostics;
using System.Text.Json;
using FlixelDebugger.Core.Tests;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Panels;
using ModelContextProtocol.Protocol;

namespace FlixelDebugger.Mcp.Tests;

/// <summary>
/// Tests for the problems found when agents used the tools against a real game.
/// </summary>
public class EndToEndFixTests
{
  private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47];
  private static readonly int[] _sides = [64, 128, 256];
  private static readonly string[] _statFields = ["fps", "paused"];
  private static readonly int[] _badTarget = [1, 2];

  private static ViewportPayload View(byte camera, byte count = 2, params ViewportBox[] boxes) =>
      new(camera, count, 320f, 180f, 1f, 0f, 0f, false, false, false, -1, ["Button"], boxes);

  private static async Task<ScriptedGame> ConnectWithSceneAsync(McpSetup setup)
  {
    var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Scene, new ScenePayload(
    [
      new SceneNode(1, -1, "PlayState", "state", false),
      new SceneNode(378, 1, "Button", "0", false),
    ]));
    await Deadline.UntilAsync(() => session.Scene.Nodes.Count == 2);
    return game;
  }

  [Fact(Timeout = 30000)]
  public async Task ParallelScreenshotsTakeTurnsBecauseTheGameKeepsOneOpenRequest()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Capture, new CaptureStatePayload(true));
    var busy = 0;
    var answered = 0;
    game.OnAction = action =>
    {
      if (action.Action is not CaptureRequestAction request)
      {
        return Task.CompletedTask;
      }

      // Like the real game: one open request at a time, extras are dropped without an answer.
      if (Interlocked.Exchange(ref busy, 1) == 1)
      {
        return Task.CompletedTask;
      }

      _ = Task.Run(async () =>
      {
        await Task.Delay(100);
        Interlocked.Increment(ref answered);
        Interlocked.Exchange(ref busy, 0);
        await game.Data(
            ScriptedGame.Capture, new CaptureImagePayload(request.RequestId, 1, 1, _png));
      });
      return Task.CompletedTask;
    };

    // Different sizes cannot share a picture, so each needs its own request.
    var calls = _sides.Select(side => setup.Client.CallToolAsync(
        "screenshot",
        new Dictionary<string, object?> { ["maxSide"] = side },
        cancellationToken: Deadline.Token()).AsTask());
    var results = await Task.WhenAll(calls);

    Assert.All(results, r => Assert.NotEqual(true, r.IsError));
    Assert.Equal(3, Volatile.Read(ref answered));
  }

  [Fact(Timeout = 30000)]
  public async Task ACallerInLineReusesAPictureThatFinishedAfterItAsked()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Capture, new CaptureStatePayload(true));
    game.OnAction = action =>
    {
      if (action.Action is CaptureRequestAction request)
      {
        _ = Task.Run(async () =>
        {
          await Task.Delay(100);
          await game.Data(
              ScriptedGame.Capture, new CaptureImagePayload(request.RequestId, 1, 1, _png));
        });
      }

      return Task.CompletedTask;
    };

    var calls = Enumerable.Range(0, 3).Select(_ => setup.Client.CallToolAsync(
        "screenshot", cancellationToken: Deadline.Token()).AsTask());
    var results = await Task.WhenAll(calls);

    Assert.All(results, r => Assert.NotEqual(true, r.IsError));
    Assert.Equal(1, game.Actions.Count(a => a.Action is CaptureRequestAction));
  }

  [Fact(Timeout = 30000)]
  public async Task AScreenshotIsRecompressedAndTheTextShowsBothSizes()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Capture, new CaptureStatePayload(true));
    var stored = PngSupport.Make(64, 64, stored: true);
    game.OnAction = action => action.Action is CaptureRequestAction request
        ? game.Data(
            ScriptedGame.Capture, new CaptureImagePayload(request.RequestId, 64, 64, stored))
        : Task.CompletedTask;

    var result = await setup.Client.CallToolAsync(
        "screenshot", cancellationToken: Deadline.Token());

    var image = Assert.IsType<ImageContentBlock>(result.Content[0]);
    var small = image.DecodedData.ToArray();
    Assert.True(small.Length < stored.Length / 2);
    PngSupport.AssertValid(small, PngSupport.Raw(64, 64));
    var text = Assert.IsType<TextContentBlock>(result.Content[1]).Text;
    Assert.Contains("64x64 PNG", text, StringComparison.Ordinal);
    Assert.Contains("->", text, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task NumbersAreAcceptedWhereAnObjectIdIsExpected()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ConnectWithSceneAsync(setup);
    game.OnAction = action =>
    {
      switch (action.Action)
      {
        case InspectorSelectAction select:
          return game.Data(
              ScriptedGame.Inspector, new InspectorStatePayload(select.Id, "state.0", [], true));
        case InspectorRequestThumbnailAction thumb:
          return game.Data(
              ScriptedGame.Inspector, new InspectorThumbnailPayload(thumb.Id, 1, 1, _png));
        default:
          return Task.CompletedTask;
      }
    };

    var inspect = JsonDocument.Parse(
        await setup.TextAsync("inspect", new() { ["target"] = 378 })).RootElement;
    await setup.TextAsync("click_object", new() { ["target"] = 378 });
    var byNumber = await setup.Client.CallToolAsync(
        "get_thumbnail",
        new Dictionary<string, object?> { ["id"] = 378 },
        cancellationToken: Deadline.Token());
    var byPath = await setup.Client.CallToolAsync(
        "get_thumbnail",
        new Dictionary<string, object?> { ["id"] = "state.0" },
        cancellationToken: Deadline.Token());
    var scene = JsonDocument.Parse(
        await setup.TextAsync("get_scene", new() { ["root"] = 378 })).RootElement;

    Assert.Equal(378, inspect.GetProperty("id").GetInt32());
    Assert.Contains(game.Actions, a => a.Action == new InputClickObjectAction(378, 0));
    Assert.NotEqual(true, byNumber.IsError);
    Assert.NotEqual(true, byPath.IsError);
    Assert.Equal(
        2, game.Actions.Count(a => a.Action == new InspectorRequestThumbnailAction(378)));
    Assert.Equal(1, scene.GetProperty("nodes").GetArrayLength());
  }

  [Fact(Timeout = 30000)]
  public async Task StatsFieldsAcceptAnArrayOrAString()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();

    var array = JsonDocument.Parse(await setup.TextAsync(
        "get_stats", new() { ["fields"] = _statFields })).RootElement;
    var text = JsonDocument.Parse(await setup.TextAsync(
        "get_stats", new() { ["fields"] = "fps, paused" })).RootElement;

    Assert.Equal(
        ["fps", "paused"], array.GetProperty("fields").EnumerateObject().Select(p => p.Name));
    Assert.Equal(
        ["fps", "paused"], text.GetProperty("fields").EnumerateObject().Select(p => p.Name));
  }

  [Fact(Timeout = 30000)]
  public async Task AWrongArgumentTypeNamesTheArgumentAndTheExpectedType()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();

    var wrong = await setup.ErrorAsync("get_scene", new() { ["depth"] = "deep" });
    var missing = await setup.ErrorAsync("click", new() { ["x"] = 1.0 });
    var shape = await setup.ErrorAsync("inspect", new() { ["target"] = _badTarget });

    Assert.Contains("Argument 'depth' must be a whole number", wrong, StringComparison.Ordinal);
    Assert.Contains("got JSON string", wrong, StringComparison.Ordinal);
    Assert.Contains("Missing required argument 'y'", missing, StringComparison.Ordinal);
    Assert.Contains(
        "Argument 'target' must be a string or a number", shape, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task AZeroMoveReturnsAtOnceAndSendsNothing()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Viewport, View(0, 2, new ViewportBox(5, 1f, 1f, 2f, 2f, 0, 0)));
    await Deadline.UntilAsync(() => session.Viewport is not null);

    var timer = Stopwatch.StartNew();
    var text = await setup.TextAsync(
        "move_object", new() { ["id"] = 5, ["dx"] = 0.0, ["dy"] = 0.0 });

    Assert.True(timer.ElapsedMilliseconds < 1000);
    Assert.Contains("Nothing was sent", text, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(game.Actions, a => a.Action is ViewportMoveAction);
  }

  [Fact(Timeout = 30000)]
  public async Task MovingAnObjectThatIsNotInTheViewportIsSentWithoutWaiting()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Viewport, View(0));
    await Deadline.UntilAsync(() => session.Viewport is not null);

    var timer = Stopwatch.StartNew();
    var json = JsonDocument.Parse(await setup.TextAsync(
        "move_object", new() { ["id"] = 99, ["dx"] = 3.0, ["dy"] = 0.0 })).RootElement;

    Assert.True(timer.ElapsedMilliseconds < 1000);
    Assert.True(json.GetProperty("sent").GetBoolean());
    Assert.False(json.GetProperty("confirmed").GetBoolean());
    await game.NextActionAsync(a => a.Action == new ViewportMoveAction(99, 3f, 0f));
  }

  [Fact(Timeout = 30000)]
  public async Task SetCameraWaitsForTheCameraThatWasAskedFor()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Viewport, View(0));
    await Deadline.UntilAsync(() => session.Viewport is not null);
    game.OnAction = action =>
    {
      if (action.Action is ViewportSetCameraAction set)
      {
        // A state sampled before the switch arrives first, then the real answer.
        _ = Task.Run(async () =>
        {
          await game.Data(ScriptedGame.Viewport, View(0));
          await Task.Delay(100);
          await game.Data(ScriptedGame.Viewport, View(set.Index <= 1 ? set.Index : (byte)0));
        });
      }

      return Task.CompletedTask;
    };

    var one = JsonDocument.Parse(
        await setup.TextAsync("set_camera", new() { ["index"] = 1 })).RootElement;
    var past = JsonDocument.Parse(
        await setup.TextAsync("set_camera", new() { ["index"] = 7 })).RootElement;

    Assert.Equal(1, one.GetProperty("camera").GetInt32());
    Assert.Equal(0, past.GetProperty("camera").GetInt32());
  }

  [Fact(Timeout = 30000)]
  public async Task ClickObjectFailsForAnIdThatIsNotInTheSceneTree()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ConnectWithSceneAsync(setup);

    var error = await setup.ErrorAsync("click_object", new() { ["target"] = 999 });

    Assert.Contains("is in the scene tree", error, StringComparison.Ordinal);
    Assert.DoesNotContain(game.Actions, a => a.Action is InputClickObjectAction);
  }

  [Fact(Timeout = 30000)]
  public async Task DescriptionsExplainCamerasAndCoordinates()
  {
    await using var setup = await McpSetup.StartAsync();

    var tools = await setup.Client.ListToolsAsync(cancellationToken: Deadline.Token());
    string Describe(string name) => tools.First(t => t.Name == name).Description!;

    Assert.Contains("own camera", Describe("get_viewport"), StringComparison.Ordinal);
    Assert.Contains("view space", Describe("get_viewport"), StringComparison.Ordinal);
    Assert.Contains("view units of the camera", Describe("click"), StringComparison.Ordinal);
    Assert.Contains("own camera", Describe("click"), StringComparison.Ordinal);
    Assert.Contains("view units of the camera", Describe("pointer"), StringComparison.Ordinal);
    Assert.Contains("own camera", Describe("click_object"), StringComparison.Ordinal);
  }
}
