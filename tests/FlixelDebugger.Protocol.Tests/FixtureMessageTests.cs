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

namespace FlixelDebugger.Protocol.Tests;

public class FixtureMessageTests
{
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
  public void MsgPanelList()
  {
    var list = Fixture.Message<PanelList>("msg_panel_list");
    Assert.Equal(
        new PanelInfo[]
        {
                new(0, "stats", "Stats", PanelKind.Stats),
                new(1, "logs", "Logs", PanelKind.Logs),
                new(2, "watches", "Watches", PanelKind.Watches),
                new(3, "trackers", "Trackers", PanelKind.Trackers),
                new(4, "textures", "Textures", PanelKind.Textures),
                new(5, "controls", "Controls", PanelKind.Controls),
                new(6, "console", "Console", PanelKind.Console),
                new(7, "scene", "Scene", PanelKind.Scene),
                new(8, "inspector", "Inspector", PanelKind.Inspector),
                new(9, "save", "Save", PanelKind.Save),
                new(10, "custom.demo", "Demo", PanelKind.Custom),
                new(11, "viewport", "Viewport", PanelKind.Viewport),
        },
        list.Panels);
  }

  [Fact]
  public void MsgPing()
  {
    var ping = Fixture.Message<Ping>("msg_ping");
    Assert.Equal(MessageType.Ping, ping.Type);
    Assert.Equal(1234567890123L, ping.Nonce);
  }

  [Fact]
  public void MsgPong()
  {
    var pong = Fixture.Message<Pong>("msg_pong");
    Assert.Equal(MessageType.Pong, pong.Type);
    Assert.Equal(1234567890123L, pong.Nonce);
  }
}
