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
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Messages;
using ModelContextProtocol.Protocol;

namespace FlixelDebugger.Mcp.Tests;

public class ActionToolTests
{
  private static InspectorStatePayload Sprite(int id, string path, float x) => new(
      id,
      path,
      [
        new InspectorSection(
            "FlixelSprite",
            "Sprite",
            [
              new PropertyRow("x", PropertyValue.FromFloat(x), true),
              new PropertyRow("id", PropertyValue.FromInt(id), false),
            ]),
      ],
      false);

  [Fact(Timeout = 30000)]
  public async Task InspectSelectsByPathAndReturnsProperties()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    game.OnAction = action => action.Action is InspectorSelectPathAction path
        ? game.Data(ScriptedGame.Inspector, Sprite(3, path.Path, 10f))
        : Task.CompletedTask;

    var json = JsonDocument.Parse(
        await setup.TextAsync("inspect", new() { ["target"] = "state.0.0" })).RootElement;

    Assert.Equal(3, json.GetProperty("id").GetInt32());
    Assert.Equal("state.0.0", json.GetProperty("path").GetString());
    var rows = json.GetProperty("sections")[0].GetProperty("properties");
    Assert.Equal("x", rows[0].GetProperty("name").GetString());
    Assert.Equal("10", rows[0].GetProperty("value").GetString());
    Assert.True(rows[0].GetProperty("editable").GetBoolean());
    Assert.False(rows[1].GetProperty("editable").GetBoolean());
  }

  [Fact(Timeout = 30000)]
  public async Task InspectTimesOutCleanlyWhenTheObjectDoesNotExist()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();

    var error = await setup.ErrorAsync("inspect", new() { ["target"] = "999" });

    Assert.Contains("did not select '999'", error, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task SetPropertySendsTheRightActionAndReturnsTheNewValue()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Inspector, Sprite(3, "state.0.0", 10f));
    await Deadline.UntilAsync(() => session.Inspector is not null);
    game.OnAction = action => action.Action is InspectorSetPropertyAction set
        ? game.Data(ScriptedGame.Inspector, Sprite(3, "state.0.0", set.Value.AsFloat()))
        : Task.CompletedTask;

    var json = JsonDocument.Parse(await setup.TextAsync(
        "set_property",
        new() { ["target"] = "3", ["property"] = "x", ["value"] = "25.5" })).RootElement;

    var sent = Assert.IsType<InspectorSetPropertyAction>(
        (await game.NextActionAsync(a => a.Action is InspectorSetPropertyAction)).Action);
    Assert.Equal(3, sent.Id);
    Assert.Equal(0, sent.SectionIndex);
    Assert.Equal(0, sent.PropertyIndex);
    Assert.Equal(25.5f, sent.Value.AsFloat());
    Assert.Equal("10", json.GetProperty("before").GetString());
    Assert.Equal("25.5", json.GetProperty("after").GetString());
    Assert.True(json.GetProperty("confirmed").GetBoolean());
  }

  [Fact(Timeout = 30000)]
  public async Task SetPropertyRejectsReadOnlyUnknownAndBadValues()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Inspector, Sprite(3, "state.0.0", 10f));
    await Deadline.UntilAsync(() => session.Inspector is not null);

    var readOnly = await setup.ErrorAsync(
        "set_property", new() { ["target"] = "3", ["property"] = "id", ["value"] = "4" });
    var unknown = await setup.ErrorAsync(
        "set_property", new() { ["target"] = "3", ["property"] = "nope", ["value"] = "4" });
    var bad = await setup.ErrorAsync(
        "set_property", new() { ["target"] = "3", ["property"] = "x", ["value"] = "abc" });

    Assert.Contains("read-only", readOnly, StringComparison.Ordinal);
    Assert.Contains("No property 'nope'", unknown, StringComparison.Ordinal);
    Assert.Contains("Properties: x, id", unknown, StringComparison.Ordinal);
    Assert.Contains("Cannot use 'abc'", bad, StringComparison.Ordinal);
    Assert.DoesNotContain(game.Actions, a => a.Action is InspectorSetPropertyAction);
  }

  [Fact(Timeout = 30000)]
  public async Task RunCommandReturnsTheResultAndTheLinesPrintedMeanwhile()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Logs, new LogsPayload(
        true,
        [new LogLine(1, 0.5, LogLevel.Info, false, "Game", "A:1", "old line", string.Empty)]));
    await Deadline.UntilAsync(() => session.Logs.Count == 1);
    game.OnAction = async action =>
    {
      if (action.Action is ConsoleRunAction run)
      {
        await game.Data(ScriptedGame.Logs, new LogsPayload(
            false,
            [new LogLine(
                2, 1.0, LogLevel.Info, false, "cmd", "A:2", $"ran {run.Line}", string.Empty)]));
        await game.Data(
            ScriptedGame.Console, new ConsoleResultPayload(ConsoleStatus.Ran, "ok"));
      }
    };

    var json = JsonDocument.Parse(
        await setup.TextAsync("run_command", new() { ["command"] = "goto 0 0" })).RootElement;

    Assert.Equal("ran", json.GetProperty("status").GetString());
    Assert.Equal("ok", json.GetProperty("message").GetString());
    var output = json.GetProperty("output");
    Assert.Equal(1, output.GetArrayLength());
    Assert.Contains("ran goto 0 0", output[0].GetString(), StringComparison.Ordinal);
    Assert.DoesNotContain("old line", output.ToString(), StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task RunCommandRejectsLongLines()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();

    var error = await setup.ErrorAsync(
        "run_command", new() { ["command"] = new string('a', 501) });

    Assert.Contains("1 to 500", error, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task ControlsToolsSendActionsAndReportTheState()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    game.OnAction = action => action.Action is ControlsSetPausedAction p
        ? game.Data(
            ScriptedGame.Controls,
            new ControlsPayload(p.Paused, false, false, 20, LogLevel.Default, []))
        : Task.CompletedTask;

    var paused = JsonDocument.Parse(
        await setup.TextAsync("set_paused", new() { ["paused"] = true })).RootElement;
    await setup.TextAsync("step", new() { ["frames"] = 2 });

    Assert.True(paused.GetProperty("paused").GetBoolean());
    Assert.Equal(2, game.Actions.Count(a => a.Action is ControlsStepAction));
  }

  [Fact(Timeout = 30000)]
  public async Task ViewportToolsSendTheRightActions()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    var state = new ViewportPayload(
        0, 1, 320f, 180f, 1f, 0f, 0f, false, false, false, 3, ["FlixelSprite"],
        [new ViewportBox(3, 1f, 2f, 3f, 4f, 0, ViewportBoxState.Selected)]);
    game.OnAction = action => action.Action is ViewportPickAction or ViewportMoveAction
        ? game.Data(ScriptedGame.Viewport, state)
        : Task.CompletedTask;

    var pick = JsonDocument.Parse(await setup.TextAsync(
        "viewport_pick", new() { ["x"] = 5.0, ["y"] = 6.0 })).RootElement;
    await setup.TextAsync("move_object", new() { ["id"] = 3, ["dx"] = 1.5, ["dy"] = -2.0 });

    Assert.Equal(3, pick.GetProperty("selectedId").GetInt32());
    Assert.Equal("FlixelSprite", pick.GetProperty("type").GetString());
    Assert.Equal(
        new ViewportPickAction(5f, 6f),
        (await game.NextActionAsync(a => a.Action is ViewportPickAction)).Action);
    Assert.Equal(
        new ViewportMoveAction(3, 1.5f, -2f),
        (await game.NextActionAsync(a => a.Action is ViewportMoveAction)).Action);
  }

  [Fact(Timeout = 30000)]
  public async Task CameraToolsSkipStatesFromBeforeTheAction()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server, minor: 1);
    var session = await setup.Server.NextConnectedAsync();
    static ViewportPayload State(float scrollX, int actions) => new(
        0, 1, 320f, 180f, 1f, scrollX, 0f, true, false, false, -1, [], [], actions);
    await game.Data(ScriptedGame.Viewport, State(0f, 4));
    await Deadline.UntilAsync(() => session.Viewport?.ActionCount == 4);

    // The game had already sampled the old scroll when the pan arrived, so that state comes
    // first; the tool must report the one after it.
    game.OnAction = async action =>
    {
      if (action.Action is ViewportPanAction)
      {
        await game.Data(ScriptedGame.Viewport, State(0f, 4));
        await game.Data(ScriptedGame.Viewport, State(10f, 5));
      }
    };

    var pan = JsonDocument.Parse(await setup.TextAsync(
        "pan_camera", new() { ["dx"] = 10.0, ["dy"] = 0.0 })).RootElement;

    Assert.Equal(10, pan.GetProperty("scrollX").GetDouble());
  }

  [Fact(Timeout = 30000)]
  public async Task InputToolsSendKeyPointerScrollTextAndClickObject()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Scene, new ScenePayload(
        [
          new SceneNode(1, -1, "PlayState", "state", false),
          new SceneNode(2, 1, "Button", "0", false),
        ]));
    await Deadline.UntilAsync(() => session.Scene.Nodes.Count == 2);

    await setup.TextAsync("press_key", new() { ["key"] = "space" });
    await setup.TextAsync("key_down", new() { ["key"] = "LEFT" });
    await setup.TextAsync("click", new() { ["x"] = 10.0, ["y"] = 20.0, ["button"] = "right" });
    await setup.TextAsync("scroll", new() { ["amount"] = -2.0 });
    await setup.TextAsync("type_text", new() { ["text"] = "hi" });
    await setup.TextAsync("click_object", new() { ["target"] = "state.0" });
    await Deadline.UntilAsync(() => game.Actions.Count == 8);

    Assert.Equal(
        new ActionPayload[]
        {
          new InputKeyAction(true, 62),
          new InputKeyAction(false, 62),
          new InputKeyAction(true, 21),
          new InputPointerAction(InputPointerAction.Down, 1, 10f, 20f),
          new InputPointerAction(InputPointerAction.Up, 1, 10f, 20f),
          new InputScrollAction(-2f),
          new InputTextAction("hi"),
          new InputClickObjectAction(2, 0),
        },
        game.Actions.Select(a => a.Action).Take(8));
  }

  [Fact(Timeout = 30000)]
  public async Task InputToolsGiveClearErrors()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Input, new InputStatePayload(InputSupport.Key));

    var badKey = await setup.ErrorAsync("press_key", new() { ["key"] = "banana" });
    var noPointer = await setup.ErrorAsync(
        "click", new() { ["x"] = 1.0, ["y"] = 1.0 });

    Assert.Contains("Unknown key 'banana'", badKey, StringComparison.Ordinal);
    Assert.Contains("does not support pointer input", noPointer, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task SaveToolsSendActions()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    game.OnAction = action => action.Action is SaveSetAction or SaveSaveNowAction
        ? game.Data(
            ScriptedGame.Save, new SavePayload([], [], "done", false))
        : Task.CompletedTask;

    var set = JsonDocument.Parse(await setup.TextAsync(
        "set_save_value", new() { ["path"] = "a.b", ["text"] = "5" })).RootElement;
    await setup.TextAsync("save_now");

    Assert.Equal("done", set.GetProperty("status").GetString());
    Assert.Contains(game.Actions, a => a.Action == new SaveSetAction("a.b", "5"));
    Assert.Contains(game.Actions, a => a.Action is SaveSaveNowAction);
  }

  [Fact(Timeout = 30000)]
  public async Task ScreenshotReturnsAPngImage()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Capture, new CaptureStatePayload(true));
    byte[] png = [0x89, 0x50, 0x4E, 0x47];
    game.OnAction = action => action.Action is CaptureRequestAction request
        ? game.Data(ScriptedGame.Capture, new CaptureImagePayload(request.RequestId, 2, 1, png))
        : Task.CompletedTask;

    var result = await setup.Client.CallToolAsync(
        "screenshot",
        new Dictionary<string, object?> { ["maxSide"] = 640 },
        cancellationToken: Deadline.Token());

    Assert.NotEqual(true, result.IsError);
    var image = Assert.IsType<ImageContentBlock>(result.Content[0]);
    Assert.Equal("image/png", image.MimeType);
    Assert.Equal(png, image.DecodedData.ToArray());
    var request = Assert.IsType<CaptureRequestAction>(
        (await game.NextActionAsync(a => a.Action is CaptureRequestAction)).Action);
    Assert.Equal(640, request.MaxSide);
  }

  [Fact(Timeout = 30000)]
  public async Task ScreenshotTimesOutCleanlyWhenTheGameNeverAnswers()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();

    var error = await setup.ErrorAsync("screenshot");

    Assert.Contains("did not send a screenshot", error, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task ScreenshotExplainsUnsupportedAndOldGames()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Capture, new CaptureStatePayload(false));
    await Deadline.UntilAsync(() => session.CaptureSupported == false);

    var unsupported = await setup.ErrorAsync("screenshot");

    await using var old = await ScriptedGame.ConnectAsync(setup.Server, "Old", withPanels: false);
    await old.SendAsync(new FeedList(
        [new FeedInfo(0, "stats", FeedKind.Stats)]));
    await setup.Server.NextConnectedAsync(2);
    await Deadline.UntilAsync(
        () => setup.Server.Server.Sessions.Any(s => s.Feeds.Count == 1));
    var oldId = setup.Server.Server.Sessions.First(s => s.Feeds.Count == 1).Id.ToString();
    var tooOld = await setup.ErrorAsync("screenshot", new() { ["session"] = oldId });

    Assert.Contains("cannot read the finished frame", unsupported, StringComparison.Ordinal);
    Assert.Contains("no 'capture' feed", tooOld, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task ThumbnailReturnsAnImage()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();
    byte[] png = [1, 2, 3];
    game.OnAction = action => action.Action is InspectorRequestThumbnailAction request
        ? game.Data(
            ScriptedGame.Inspector, new InspectorThumbnailPayload(request.Id, 1, 1, png))
        : Task.CompletedTask;

    var result = await setup.Client.CallToolAsync(
        "get_thumbnail",
        new Dictionary<string, object?> { ["id"] = 3, ["kind"] = "object" },
        cancellationToken: Deadline.Token());

    Assert.Equal(png, Assert.IsType<ImageContentBlock>(result.Content[0]).DecodedData.ToArray());
  }

  [Fact(Timeout = 30000)]
  public async Task ThumbnailOfAnObjectWithoutTextureFailsAtOnce()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Inspector, new InspectorStatePayload(3, "state.0", [], false));
    await Deadline.UntilAsync(() => session.Inspector?.SelectedId == 3);
    var watch = Stopwatch.StartNew();

    var error = await setup.ErrorAsync(
        "get_thumbnail", new() { ["id"] = 3, ["kind"] = "object" });

    Assert.Contains("has no texture", error, StringComparison.Ordinal);
    Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
  }
}
