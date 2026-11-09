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

using System.Text.Json;
using FlixelDebugger.Core.Tests;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Mcp.Tests;

public class ReadToolTests
{
  private static LogLine Line(long seq, LogLevel level, string message, bool uncaught = false) =>
      new(seq, seq * 0.5, level, uncaught, "Game", "Main.java:1", message, string.Empty);

  [Fact(Timeout = 30000)]
  public async Task GetLogsIgnoresTheUiFiltersAndSupportsACursor()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Logs, new LogsPayload(
        true,
        [
          Line(1, LogLevel.Debug, "debug one"),
          Line(2, LogLevel.Info, "info two"),
          Line(3, LogLevel.Error, "boom three"),
        ]));
    await Deadline.UntilAsync(() => session.Logs.Count == 3);
    session.Logs.SetLevelShown(LogLevel.Debug, false);
    session.Logs.Search = "zzz";

    var all = JsonDocument.Parse(await setup.TextAsync("get_logs")).RootElement;
    var warn = JsonDocument.Parse(
        await setup.TextAsync("get_logs", new() { ["level"] = "warn" })).RootElement;
    var since = JsonDocument.Parse(
        await setup.TextAsync("get_logs", new() { ["sinceSeq"] = 2 })).RootElement;
    var limited = JsonDocument.Parse(
        await setup.TextAsync("get_logs", new() { ["limit"] = 2 })).RootElement;

    Assert.Equal(3, all.GetProperty("lines").GetArrayLength());
    Assert.Equal(3, all.GetProperty("lastSeq").GetInt64());
    Assert.Contains(
        "boom three", warn.GetProperty("lines")[0].GetString(), StringComparison.Ordinal);
    Assert.Equal(1, warn.GetProperty("lines").GetArrayLength());
    Assert.Equal(1, since.GetProperty("lines").GetArrayLength());
    Assert.True(limited.GetProperty("truncated").GetBoolean());
    Assert.Contains(
        "info two", limited.GetProperty("lines")[0].GetString(), StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task GetStatsSummarizesTheWindow()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    foreach (var fps in new float[] { 30, 60 })
    {
      await game.Data(ScriptedGame.Stats, new StatsPayload(
          fps, 16f, 5f, 1f, 100, 2000, 6000, 3, 2, 4, 64f, 32f, 16f, 8f, 250, 1.5f, 2.5f, false,
          12.5, 10f, 20f, 1.5f));
    }

    await Deadline.UntilAsync(() => session.Stats.Count == 2);

    var json = JsonDocument.Parse(
        await setup.TextAsync("get_stats", new() { ["fields"] = "fps, draws" })).RootElement;

    var fpsField = json.GetProperty("fields").GetProperty("fps");
    Assert.Equal(60, fpsField.GetProperty("latest").GetDouble());
    Assert.Equal(30, fpsField.GetProperty("min").GetDouble());
    Assert.Equal(60, fpsField.GetProperty("max").GetDouble());
    Assert.Equal(45, fpsField.GetProperty("avg").GetDouble());
    var draws = json.GetProperty("fields").GetProperty("draws");
    Assert.Equal(100, draws.GetProperty("latest").GetDouble());
  }

  [Fact(Timeout = 30000)]
  public async Task GetSceneListsPathsIdsAndRespectsDepthAndLimit()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Scene, new ScenePayload(
    [
      new SceneNode(1, -1, "PlayState", "state", false),
      new SceneNode(2, 1, "FlixelGroup", "0", false),
      new SceneNode(3, 2, "FlixelSprite", "0", false),
      new SceneNode(4, 1, "FlixelText", "2", false),
    ]));
    await Deadline.UntilAsync(() => session.Scene.Nodes.Count == 4);

    var full = JsonDocument.Parse(await setup.TextAsync("get_scene")).RootElement;
    var shallow = JsonDocument.Parse(
        await setup.TextAsync("get_scene", new() { ["depth"] = 1 })).RootElement;
    var branch = JsonDocument.Parse(
        await setup.TextAsync("get_scene", new() { ["root"] = "state.0" })).RootElement;
    var limited = JsonDocument.Parse(
        await setup.TextAsync("get_scene", new() { ["limit"] = 2 })).RootElement;

    Assert.Equal(
        ["state", "state.0", "state.0.0", "state.2"],
        full.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("path").GetString()));
    Assert.Equal(3, shallow.GetProperty("nodes").GetArrayLength());
    Assert.Equal(
        ["state.0", "state.0.0"],
        branch.GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("path").GetString()));
    Assert.True(limited.GetProperty("truncated").GetBoolean());
    Assert.Equal(2, limited.GetProperty("nodes").GetArrayLength());
    var error = await setup.ErrorAsync("get_scene", new() { ["root"] = "nope.1" });
    Assert.Contains("No object matches", error, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task GetViewportReturnsTheCameraAndBoxes()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Viewport, new ViewportPayload(
        0, 2, 320f, 180f, 2f, 100f, -40f, false, false, true, 3, ["FlixelSprite"],
        [new ViewportBox(3, 10f, 20f, 16f, 16f, 0, ViewportBoxState.Selected)]));
    await Deadline.UntilAsync(() => session.Viewport is not null);

    var json = JsonDocument.Parse(await setup.TextAsync("get_viewport")).RootElement;

    Assert.Equal(2, json.GetProperty("cameraCount").GetInt32());
    Assert.Equal(3, json.GetProperty("selectedId").GetInt32());
    var box = json.GetProperty("boxes")[0];
    Assert.Equal("FlixelSprite", box.GetProperty("type").GetString());
    Assert.True(box.GetProperty("selected").GetBoolean());
  }

  [Fact(Timeout = 30000)]
  public async Task WatchesAndSaveAreReadable()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.Data(ScriptedGame.Watches, new WatchesPayload(
        [new NamedValue("hp", PropertyValue.FromInt(7))]));
    await game.Data(ScriptedGame.Save, new SavePayload(
        [new SaveRow("player.coins", "5", ValueKind.Int, true)], [], "1 values", false));
    await Deadline.UntilAsync(() => session.Watches is not null && session.Save is not null);

    var watches = JsonDocument.Parse(await setup.TextAsync("get_watches")).RootElement;
    var save = JsonDocument.Parse(await setup.TextAsync("get_save")).RootElement;

    Assert.Equal("7", watches.GetProperty("watches").GetProperty("hp").GetString());
    var row = save.GetProperty("rows")[0];
    Assert.Equal("player.coins", row.GetProperty("path").GetString());
    Assert.True(row.GetProperty("unsaved").GetBoolean());
  }

  [Fact(Timeout = 30000)]
  public async Task ListTexturesReportsTheRenderersTotalAndEachSize()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    // The list only has the 1x1 white pixel, but fonts and render targets take 5.5 MB.
    await game.Data(ScriptedGame.Textures, new TexturesListPayload(
        [new TextureEntry(1, "white_pixel_1x1", 1, 1, "RGBA8", 4, 0, false)], 1, 4, 0));
    await game.Data(ScriptedGame.Stats, new StatsPayload(
        60f, 16f, 5f, 1f, 100, 2000, 6000, 3, 2, 4, 64f, 32f, 5.5f, 8f, 250, 1.5f, 2.5f, false,
        12.5, 10f, 20f, 1.5f));
    await Deadline.UntilAsync(() => session.Textures is not null && session.Stats.Count == 1);

    var list = JsonDocument.Parse(await setup.TextAsync("list_textures")).RootElement;

    Assert.Equal(5.5, list.GetProperty("gpuMb").GetDouble());
    Assert.Equal(0, list.GetProperty("listedKb").GetDouble());
    var big = new TextureEntry(2, "atlas.png", 512, 512, "RGBA8", 1024 * 1024, 3, false);
    await game.Data(ScriptedGame.Textures, new TexturesListPayload([big], 1, big.GpuBytes, 0));
    await Deadline.UntilAsync(() => session.Textures?.TotalGpuBytes == big.GpuBytes);

    list = JsonDocument.Parse(await setup.TextAsync("list_textures")).RootElement;

    Assert.Equal(5.5, list.GetProperty("gpuMb").GetDouble());
    Assert.Equal(1024, list.GetProperty("listedKb").GetDouble());
    Assert.Equal(1024, list.GetProperty("textures")[0].GetProperty("kb").GetDouble());
  }
}
