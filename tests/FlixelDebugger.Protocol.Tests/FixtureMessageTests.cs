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

using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.Protocol.Tests;

public class FixtureMessageTests
{
  private static (int, PatchCode, WidgetProp) Head(PatchOp op) => (op.Widget, op.Code, op.Key);

  [Fact]
  public void MsgHello()
  {
    var hello = Fixture.Message<Hello>("msg_hello");
    Assert.Equal(MessageType.Hello, hello.Type);
    Assert.Equal(1, hello.ProtocolMajor);
    Assert.Equal(0, hello.ProtocolMinor);
    Assert.Equal("desktop", hello.Platform);
    Assert.Equal("Linux 6.17 amd64", hello.Device);
    Assert.Equal(1280, hello.ScreenWidth);
    Assert.Equal(720, hello.ScreenHeight);
    Assert.Equal("Demo Game", hello.GameTitle);
    Assert.Equal("1.2.3", hello.FrameworkVersion);
  }

  [Fact]
  public void MsgHelloAck()
  {
    var ack = Fixture.Message<HelloAck>("msg_hello_ack");
    Assert.Equal(MessageType.HelloAck, ack.Type);
    Assert.Equal(1, ack.ProtocolMajor);
    Assert.Equal(0, ack.ProtocolMinor);
  }

  [Fact]
  public void MsgFeedList()
  {
    var list = Fixture.Message<FeedList>("msg_feed_list");
    Assert.Equal(
        new FeedInfo[]
        {
                new(0, "stats", FeedKind.Stats),
                new(1, "logs", FeedKind.Logs),
                new(2, "watches", FeedKind.Watches),
                new(3, "trackers", FeedKind.Trackers),
                new(4, "textures", FeedKind.Textures),
                new(5, "controls", FeedKind.Controls),
                new(6, "console", FeedKind.Console),
                new(7, "scene", FeedKind.Scene),
                new(8, "inspector", FeedKind.Inspector),
                new(9, "save", FeedKind.Save),
                new(10, "viewport", FeedKind.Viewport),
                new(11, "capture", FeedKind.Capture),
                new(12, "input", FeedKind.Input),
        },
        list.Feeds);
  }

  [Fact]
  public void MsgFeedSubscribe()
  {
    var subscribe = Fixture.Message<FeedSubscribe>("msg_feed_subscribe");
    Assert.Equal(7, subscribe.Feed);
    Assert.True(subscribe.On);
  }

  [Fact]
  public void MsgPing()
  {
    var ping = Fixture.Message<Ping>("msg_ping");
    Assert.Equal(MessageType.Ping, ping.Type);
    Assert.Equal(1234567890123L, ping.Nonce);
    Assert.Equal(12.5f, ping.RttMs);
  }

  [Fact]
  public void MsgPong()
  {
    var pong = Fixture.Message<Pong>("msg_pong");
    Assert.Equal(MessageType.Pong, pong.Type);
    Assert.Equal(1234567890123L, pong.Nonce);
  }

  [Fact]
  public void MsgAppRequests()
  {
    var show = Fixture.Message<AppRequest>("msg_app_request_show_panel");
    Assert.Equal(AppRequestKind.ShowPanel, show.Request);
    Assert.Equal("inspector", show.PanelId);
    var hide = Fixture.Message<AppRequest>("msg_app_request_hide_panel");
    Assert.Equal(AppRequestKind.HidePanel, hide.Request);
    Assert.Equal("trackers", hide.PanelId);
    var reset = Fixture.Message<AppRequest>("msg_app_request_reset_layout");
    Assert.Equal(AppRequestKind.ResetLayout, reset.Request);
    var notify = Fixture.Message<AppRequest>("msg_app_request_notify");
    Assert.Equal(AppRequestKind.Notify, notify.Request);
    Assert.Equal(Tone.Success, notify.Tone);
    Assert.Equal("Save written", notify.Text);
  }

  [Fact]
  public void PanelList()
  {
    var list = Fixture.Message<PanelList>("panel_list");
    Assert.Equal(
        new PanelInfo[]
        {
            new(0, "stats", "Stats", PanelDock.Left, true),
            new(1, "save", "Save", PanelDock.Center, true),
            new(2, "console", "Console", PanelDock.Bottom, true),
            new(3, "game.enemies", "Enemies", PanelDock.Right, false),
        },
        list.Panels);
  }

  [Fact]
  public void PanelOpen()
  {
    var open = Fixture.Message<PanelOpen>("panel_open");
    Assert.Equal(3, open.Panel);
    Assert.True(open.Open);
  }

  [Fact]
  public void PanelTree()
  {
    var tree = Fixture.Message<PanelTree>("panel_tree");
    Assert.Equal(3, tree.Panel);
    var root = tree.Root;
    Assert.Equal(WidgetType.Stack, root.Type);
    Assert.Equal(1, root.Id);
    Assert.Equal(4f, root.Get(WidgetProp.Spacing)!.AsFloat());
    Assert.Equal(3, root.Children.Count);

    var title = root.Children[0];
    Assert.Equal((WidgetType.Label, 2, "title"), (title.Type, title.Id, title.Name));
    Assert.Equal(UiStyle.Heading, title.Get(WidgetProp.Style)!.AsInt());
    Assert.Equal("Enemies", title.Get(WidgetProp.Text)!.AsString());

    var kill = root.Children[1];
    Assert.Equal((WidgetType.Button, 3, "kill"), (kill.Type, kill.Id, kill.Name));
    Assert.Equal(Tone.Error, UiStyle.ToneOf(kill.Get(WidgetProp.Style)!.AsInt()));
    Assert.Equal("Sure?", kill.Get(WidgetProp.Confirm)!.AsString());

    var table = root.Children[2];
    Assert.Equal((WidgetType.Table, 4, "list"), (table.Type, table.Id, table.Name));
    Assert.Equal(1f, table.Get(WidgetProp.Grow)!.AsFloat());
    Assert.Equal(9L, table.Get(WidgetProp.Selected)!.AsLong());
    var rows = table.Get(WidgetProp.Items)!;
    Assert.Equal(2, rows.Count);
    Assert.Equal(7L, rows[0][0].AsLong());
    Assert.Equal("Slime", rows[0][2][0].AsString());
    Assert.Equal("64 x 64", rows[1][2][0][0].AsString());
    Assert.Equal(4096.0, rows[1][2][0][1].AsDouble());
    Assert.Equal("low health", rows[1][3].AsString());
    var columns = table.Get(WidgetProp.Columns)!;
    Assert.Equal("HP", columns[1][0].AsString());
    Assert.Equal(60f, columns[1][1].AsFloat());
    Assert.Equal(UiStyle.ColumnRight, columns[1][2].AsInt());
  }

  [Fact]
  public void PanelPatch()
  {
    var patch = Fixture.Message<PanelPatch>("panel_patch");
    Assert.Equal(3, patch.Panel);
    Assert.Equal(3, patch.Ops.Count);
    Assert.Equal((2, PatchCode.Set, WidgetProp.Text), Head(patch.Ops[0]));
    Assert.Equal("Enemies (3)", patch.Ops[0].Value.AsString());
    Assert.Equal((3, PatchCode.Set, WidgetProp.Enabled), Head(patch.Ops[1]));
    Assert.False(patch.Ops[1].Value.AsBool(true));
    Assert.Equal((4, PatchCode.Append, WidgetProp.Items), Head(patch.Ops[2]));
    Assert.Equal(11L, patch.Ops[2].Value[0][0].AsLong());
  }

  [Theory]
  [InlineData("panel_event_click", 3, WidgetEvent.Click, 0)]
  [InlineData("panel_event_change", 5, WidgetEvent.Change, 1)]
  [InlineData("panel_event_submit", 6, WidgetEvent.Submit, 1)]
  [InlineData("panel_event_select", 4, WidgetEvent.Select, 1)]
  [InlineData("panel_event_select_none", 4, WidgetEvent.Select, 1)]
  [InlineData("panel_event_menu", 4, WidgetEvent.Menu, 2)]
  [InlineData("panel_event_edit", 8, WidgetEvent.Edit, 3)]
  [InlineData("panel_event_drag", 9, WidgetEvent.Drag, 3)]
  [InlineData("panel_event_zoom", 9, WidgetEvent.Zoom, 3)]
  public void PanelEvents(string name, int widget, WidgetEvent kind, int argCount)
  {
    var e = Fixture.Message<PanelEvent>(name);
    Assert.Equal(3, e.Panel);
    Assert.Equal(widget, e.Widget);
    Assert.Equal(kind, e.Event);
    Assert.Equal(argCount, e.Args.Count);
  }

  [Fact]
  public void PanelEventArguments()
  {
    Assert.True(Fixture.Message<PanelEvent>("panel_event_change").Args[0].AsBool());
    Assert.Equal("god on", Fixture.Message<PanelEvent>("panel_event_submit").Args[0].AsString());
    Assert.Equal(9L, Fixture.Message<PanelEvent>("panel_event_select").Args[0].AsLong());
    Assert.True(Fixture.Message<PanelEvent>("panel_event_select_none").Args[0].IsNull);
    var edit = Fixture.Message<PanelEvent>("panel_event_edit").Args;
    Assert.Equal((0, 2, 12.5), (edit[0].AsInt(), edit[1].AsInt(), edit[2].AsDouble()));
    var drag = Fixture.Message<PanelEvent>("panel_event_drag").Args;
    Assert.Equal((12L, 4.5f, -2f), (drag[0].AsLong(), drag[1].AsFloat(), drag[2].AsFloat()));
    var zoom = Fixture.Message<PanelEvent>("panel_event_zoom").Args;
    Assert.Equal((1.25f, 160f, 90f), (zoom[0].AsFloat(), zoom[1].AsFloat(), zoom[2].AsFloat()));
  }
}
