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
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.Mcp.Tests;

public class PanelToolTests
{
  private static readonly string[] _pickArgs = ["f:100", "f:50"];

  private static WidgetNode Node(
      WidgetType type, int id, string name, params (WidgetProp Key, UiValue Value)[] props) =>
      new(type, id, name, props.ToDictionary(p => p.Key, p => p.Value), []);

  private static UiValue List(params UiValue[] items) => UiValue.FromList(items);

  private static WidgetNode Tree() => Node(WidgetType.Stack, 1, string.Empty) with
  {
    Children =
    [
        Node(WidgetType.Label, 2, "title", (WidgetProp.Text, UiValue.FromString("Enemies"))),
        Node(WidgetType.Button, 3, "kill", (WidgetProp.Text, UiValue.FromString("Kill all"))),
        Node(WidgetType.Checkbox, 4, "frozen", (WidgetProp.Value, UiValue.FromBool(false))),
        Node(WidgetType.TextBox, 5, "command"),
        Node(
            WidgetType.Table,
            6,
            "list",
            (WidgetProp.Menu, List(UiValue.FromString("Remove"))),
            (WidgetProp.Items, List(
                List(UiValue.FromLong(7), UiValue.FromInt(0),
                    List(UiValue.FromString("Slime")), UiValue.FromString(string.Empty))))),
        Node(
            WidgetType.Properties,
            8,
            "stats",
            (WidgetProp.Sections, List(List(
                UiValue.FromString("Enemy"),
                UiValue.FromBool(true),
                List(List(
                    UiValue.FromString("hp"), UiValue.FromInt(10), UiValue.FromBool(true))))))),
        Node(WidgetType.Dropdown, 9, "mode", (WidgetProp.Items, List(
            UiValue.FromString("Easy"), UiValue.FromString("Hard")))),
        Node(WidgetType.Canvas, 10, "map"),
    ],
  };

  private static async Task<(McpSetup Setup, ScriptedGame Game)> StartAsync()
  {
    var setup = await McpSetup.StartAsync();
    var game = await ScriptedGame.ConnectAsync(setup.Server);
    var session = await setup.Server.NextConnectedAsync();
    await game.SendAsync(new PanelList([
        new PanelInfo(0, "stats", "Stats", PanelDock.Left, true),
        new PanelInfo(1, "game.enemies", "Enemies", PanelDock.Right, true),
    ]));
    await Deadline.UntilAsync(() => session.Panels.Count == 2);
    return (setup, game);
  }

  // Answers the first PANEL_OPEN of the enemies panel with its tree.
  private static async Task SendTreeWhenOpenedAsync(ScriptedGame game)
  {
    await Deadline.UntilAsync(() => game.PanelMessages.Contains(new PanelOpen(1, true)));
    await game.SendAsync(new PanelTree(1, Tree()));
  }

  [Fact(Timeout = 30000)]
  public async Task PanelsCanBeListedAndRead()
  {
    var (setup, game) = await StartAsync();
    await using (setup)
    await using (game)
    {
      var list = JsonDocument.Parse(await setup.TextAsync("list_panels")).RootElement;
      Assert.Equal(
          ["stats", "game.enemies"],
          list.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("id").GetString()));

      var answer = SendTreeWhenOpenedAsync(game);
      var json = JsonDocument.Parse(await setup.TextAsync(
          "read_panel", new() { ["panelId"] = "game.enemies" })).RootElement;
      await answer;

      var children = json.GetProperty("root").GetProperty("children");
      Assert.Equal("Enemies", children[0].GetProperty("text").GetString());
      Assert.Equal("button", children[1].GetProperty("type").GetString());
      var row = children[4].GetProperty("rows")[0];
      Assert.Equal(7, row.GetProperty("id").GetInt64());
      Assert.Equal("Slime", row.GetProperty("cells")[0].GetString());
      var property = children[5].GetProperty("sections")[0].GetProperty("rows")[0];
      Assert.Equal(("hp", "10", true), (
          property.GetProperty("name").GetString(),
          property.GetProperty("value").GetString(),
          property.GetProperty("editable").GetBoolean()));

      var missing = await setup.ErrorAsync("read_panel", new() { ["panelId"] = "nope" });
      Assert.Contains("no panel 'nope'", missing, StringComparison.Ordinal);
    }
  }

  [Fact(Timeout = 30000)]
  public async Task WidgetToolsSendTheEventsAPersonWouldSend()
  {
    var (setup, game) = await StartAsync();
    await using (setup)
    await using (game)
    {
      game.OnPanelEvent = e => e.Event == WidgetEvent.Click
          ? game.SendAsync(new PanelPatch(1,
              [new PatchOp(2, PatchCode.Set, WidgetProp.Text, UiValue.FromString("Enemies (0)"))]))
          : Task.CompletedTask;
      var answer = SendTreeWhenOpenedAsync(game);
      await setup.TextAsync("read_panel", new() { ["panelId"] = "game.enemies" });
      await answer;

      var click = JsonDocument.Parse(await setup.TextAsync(
          "panel_click", new() { ["panelId"] = "game.enemies", ["widget"] = "kill" })).RootElement;
      await setup.TextAsync(
          "panel_set",
          new() { ["panelId"] = "game.enemies", ["widget"] = "frozen", ["value"] = "true" });
      await setup.TextAsync(
          "panel_set",
          new() { ["panelId"] = "game.enemies", ["widget"] = "mode", ["value"] = "hard" });
      await setup.TextAsync(
          "panel_submit",
          new() { ["panelId"] = "game.enemies", ["widget"] = "5", ["text"] = "god on" });
      await setup.TextAsync(
          "panel_row",
          new()
          {
            ["panelId"] = "game.enemies",
            ["widget"] = "list",
            ["action"] = "menu",
            ["rowId"] = 7,
            ["menuItem"] = "remove",
          });
      await setup.TextAsync(
          "panel_edit",
          new()
          {
            ["panelId"] = "game.enemies",
            ["widget"] = "stats",
            ["section"] = "Enemy",
            ["row"] = "hp",
            ["value"] = "12",
          });
      await setup.TextAsync(
          "panel_event",
          new()
          {
            ["panelId"] = "game.enemies",
            ["widget"] = "map",
            ["event"] = "pick",
            ["args"] = _pickArgs,
          });
      var wrongType = await setup.ErrorAsync(
          "panel_click", new() { ["panelId"] = "game.enemies", ["widget"] = "frozen" });
      var badValue = await setup.ErrorAsync(
          "panel_set",
          new() { ["panelId"] = "game.enemies", ["widget"] = "frozen", ["value"] = "maybe" });

      var changed = click.GetProperty("changed")[0];
      Assert.Equal("title", changed.GetProperty("name").GetString());
      Assert.Equal("Enemies (0)", changed.GetProperty("value").GetString());
      var events = game.PanelMessages.OfType<PanelEvent>().ToList();
      Assert.Equal(
          [WidgetEvent.Click, WidgetEvent.Change, WidgetEvent.Change, WidgetEvent.Submit,
              WidgetEvent.Menu, WidgetEvent.Edit, WidgetEvent.Pick],
          events.Select(e => e.Event));
      Assert.True(events[1].Args[0].AsBool());
      Assert.Equal(1, events[2].Args[0].AsInt());
      Assert.Equal("god on", events[3].Args[0].AsString());
      Assert.Equal((7L, 0), (events[4].Args[0].AsLong(), events[4].Args[1].AsInt()));
      Assert.Equal(
          (0, 0, ValueKind.Int, 12),
          (events[5].Args[0].AsInt(), events[5].Args[1].AsInt(), events[5].Args[2].Kind,
              events[5].Args[2].AsInt()));
      Assert.Equal((100f, 50f), (events[6].Args[0].AsFloat(), events[6].Args[1].AsFloat()));
      Assert.Contains("does not take this action", wrongType, StringComparison.Ordinal);
      Assert.Contains("true or false", badValue, StringComparison.Ordinal);

      await setup.TextAsync("close_panel", new() { ["panelId"] = "game.enemies" });
      await Deadline.UntilAsync(() => game.PanelMessages.Contains(new PanelOpen(1, false)));
    }
  }
}
