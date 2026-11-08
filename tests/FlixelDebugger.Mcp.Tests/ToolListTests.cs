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
using ModelContextProtocol.Protocol;

namespace FlixelDebugger.Mcp.Tests;

public class ToolListTests
{
  private static readonly string[] _expected =
  [
    "list_games", "get_stats", "get_logs", "get_watches", "list_commands", "run_command",
    "get_scene", "inspect", "set_property", "get_viewport", "viewport_pick", "move_object",
    "pan_camera", "zoom_camera", "reset_camera", "set_camera", "set_paused", "step",
    "set_hitboxes", "set_freecam", "get_save", "set_save_value", "save_now",
    "list_custom_panels", "custom_press", "custom_set_property", "screenshot", "list_textures",
    "get_thumbnail", "press_key", "key_down", "key_up", "click", "pointer", "scroll",
    "click_object", "type_text",
  ];

  [Fact(Timeout = 30000)]
  public async Task ListsEveryToolWithADescription()
  {
    await using var setup = await McpSetup.StartAsync();

    var tools = await setup.Client.ListToolsAsync(cancellationToken: Deadline.Token());

    Assert.Equal(_expected.Order(), tools.Select(t => t.Name).Order());
    Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description), t.Name));
  }

  [Fact(Timeout = 30000)]
  public async Task ListGamesDescribesTheConnectedGame()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server, "Demo");
    await setup.Server.NextConnectedAsync();

    var json = JsonDocument.Parse(await setup.TextAsync("list_games"));

    var entry = json.RootElement.GetProperty("games")[0];
    Assert.Equal("Demo", entry.GetProperty("title").GetString());
    Assert.Equal("desktop", entry.GetProperty("platform").GetString());
    Assert.Equal("1280x720", entry.GetProperty("screen").GetString());
    Assert.Equal(36, entry.GetProperty("id").GetString()!.Length);
  }

  [Fact(Timeout = 30000)]
  public async Task NoGameGivesAHelpfulError()
  {
    await using var setup = await McpSetup.StartAsync();

    var error = await setup.ErrorAsync("get_stats");

    Assert.Contains("No game is connected", error, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task SeveralGamesNeedASessionAndAcceptAPrefix()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var first = await ScriptedGame.ConnectAsync(setup.Server, "First");
    await using var second = await ScriptedGame.ConnectAsync(setup.Server, "Second");
    await setup.Server.NextConnectedAsync(2);

    var error = await setup.ErrorAsync("get_watches");
    var games = JsonDocument.Parse(await setup.TextAsync("list_games"))
        .RootElement.GetProperty("games");
    var id = games[1].GetProperty("id").GetString()!;

    Assert.Contains("'session' argument is required", error, StringComparison.Ordinal);
    Assert.Contains("First", error, StringComparison.Ordinal);
    Assert.Contains("Second", error, StringComparison.Ordinal);
    Assert.Contains(id, error, StringComparison.Ordinal);
    await setup.TextAsync("get_watches", new() { ["session"] = id });
    await setup.TextAsync("get_watches", new() { ["session"] = id[..8] });
    var unknown = await setup.ErrorAsync("get_watches", new() { ["session"] = "nope" });
    Assert.Contains("No connected game matches", unknown, StringComparison.Ordinal);
  }

  [Fact(Timeout = 30000)]
  public async Task AToolErrorIsReportedAsAnErrorResult()
  {
    await using var setup = await McpSetup.StartAsync();
    await using var game = await ScriptedGame.ConnectAsync(setup.Server);
    await setup.Server.NextConnectedAsync();

    var result = await setup.Client.CallToolAsync(
        "get_stats",
        new Dictionary<string, object?> { ["fields"] = "bogus" },
        cancellationToken: Deadline.Token());

    Assert.True(result.IsError);
    Assert.Contains(
        "Unknown stats field", Assert.IsType<TextContentBlock>(result.Content[0]).Text,
        StringComparison.Ordinal);
  }
}
