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

using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Panels;
using A = FlixelDebugger.Protocol.ProtocolConstants.Actions;

namespace FlixelDebugger.Protocol.Tests;

public class MessageDecoderTests
{
    private static PanelList ListOf(params PanelInfo[] panels) => new(panels);

    [Fact]
    public void EmptyInputThrows()
    {
        Assert.Throws<ProtocolException>(() => new MessageDecoder().Decode([]));
    }

    [Fact]
    public void UnknownMessageTypeIsKeptAsRawBytes()
    {
        byte[] bytes = [99, 1, 2, 3];
        var message = Assert.IsType<UnknownMessage>(new MessageDecoder().Decode(bytes));
        Assert.Equal(99, message.RawType);
        Assert.Equal(new byte[] { 1, 2, 3 }, message.Raw.ToArray());
        Assert.Equal(bytes, message.ToArray());
    }

    [Fact]
    public void RemembersTheLastPanelList()
    {
        var decoder = new MessageDecoder();
        Assert.Null(decoder.GetKind(3));

        decoder.Decode(ListOf(new PanelInfo(3, "stats", "Stats", PanelKind.Stats)).ToArray());
        Assert.Equal(PanelKind.Stats, decoder.GetKind(3));
        Assert.Equal(1, decoder.PanelCount);

        decoder.Decode(ListOf(new PanelInfo(4, "logs", "Logs", PanelKind.Logs)).ToArray());
        Assert.Null(decoder.GetKind(3));
        Assert.Equal(PanelKind.Logs, decoder.GetKind(4));
    }

    [Fact]
    public void PayloadBeforeAnyPanelListIsUnknown()
    {
        var data = Fixture.Load("panel_stats");
        var message = Assert.IsType<PanelData>(new MessageDecoder().Decode(data));
        var unknown = Assert.IsType<UnknownPayload>(message.Payload);
        Assert.Null(unknown.Kind);
        Assert.Equal(data, message.ToArray());
    }

    [Fact]
    public void StaticDecodeUsesTheGivenLookup()
    {
        var message = MessageDecoder.Decode(Fixture.Load("panel_stats"), static index => index == 0 ? PanelKind.Stats : null);
        Assert.IsType<StatsPayload>(Assert.IsType<PanelData>(message).Payload);

        var action = MessageDecoder.Decode(Fixture.Load("action_logs_clear"), static _ => PanelKind.Logs);
        Assert.IsType<LogsClearAction>(Assert.IsType<PanelAction>(action).Action);
    }

    [Fact]
    public void UnknownPanelKindIsKeptAsRawBytes()
    {
        var decoder = new MessageDecoder();
        decoder.Decode(ListOf(new PanelInfo(0, "future", "Future", (PanelKind)200)).ToArray());

        byte[] data = [4, 0, 0, 1, 2, 3];
        var payload = Assert.IsType<UnknownPayload>(Assert.IsType<PanelData>(decoder.Decode(data)).Payload);
        Assert.Equal((PanelKind)200, payload.Kind);
        Assert.Equal(new byte[] { 1, 2, 3 }, payload.Raw.ToArray());
        Assert.Equal(data, decoder.Decode(data).ToArray());

        byte[] action = [5, 0, 0, 9, 1, 2];
        var unknown = Assert.IsType<UnknownAction>(Assert.IsType<PanelAction>(decoder.Decode(action)).Action);
        Assert.Equal(9, unknown.Code);
        Assert.Equal(new byte[] { 1, 2 }, unknown.Raw.ToArray());
        Assert.Equal(action, decoder.Decode(action).ToArray());
    }

    [Theory]
    [InlineData(PanelKind.Textures)]
    [InlineData(PanelKind.Console)]
    [InlineData(PanelKind.Inspector)]
    public void UnknownSubtypeIsKeptAsRawBytesIncludingTheSubtype(PanelKind kind)
    {
        var decoder = new MessageDecoder();
        decoder.Decode(ListOf(new PanelInfo(0, "p", "P", kind)).ToArray());

        byte[] data = [4, 0, 0, 77, 1, 2, 3];
        var payload = Assert.IsType<UnknownPayload>(Assert.IsType<PanelData>(decoder.Decode(data)).Payload);
        Assert.Equal(kind, payload.Kind);
        Assert.Equal(new byte[] { 77, 1, 2, 3 }, payload.Raw.ToArray());
        Assert.Equal(data, decoder.Decode(data).ToArray());
    }

    [Theory]
    [InlineData(PanelKind.Custom)]
    [InlineData(PanelKind.Stats)]
    [InlineData(PanelKind.Logs)]
    [InlineData(PanelKind.Watches)]
    [InlineData(PanelKind.Trackers)]
    [InlineData(PanelKind.Textures)]
    [InlineData(PanelKind.Controls)]
    [InlineData(PanelKind.Console)]
    [InlineData(PanelKind.Scene)]
    [InlineData(PanelKind.Inspector)]
    [InlineData(PanelKind.Save)]
    public void UnknownActionCodeIsIgnoredForEveryKind(PanelKind kind)
    {
        var decoder = new MessageDecoder();
        decoder.Decode(ListOf(new PanelInfo(0, "p", "P", kind)).ToArray());

        byte[] action = [5, 0, 0, 200, 4, 5];
        var unknown = Assert.IsType<UnknownAction>(Assert.IsType<PanelAction>(decoder.Decode(action)).Action);
        Assert.Equal(200, unknown.RawCode);
        Assert.Equal(new byte[] { 4, 5 }, unknown.Raw.ToArray());
        Assert.Equal(action, decoder.Decode(action).ToArray());
    }

    [Fact]
    public void ActionCodesAreScopedToThePanelKind()
    {
        var decoder = new MessageDecoder();
        decoder.Decode(ListOf(
            new PanelInfo(0, "logs", "Logs", PanelKind.Logs),
            new PanelInfo(1, "console", "Console", PanelKind.Console),
            new PanelInfo(2, "stats", "Stats", PanelKind.Stats)).ToArray());

        Assert.IsType<LogsClearAction>(Assert.IsType<PanelAction>(decoder.Decode([5, 0, 0, 1])).Action);
        Assert.IsType<ConsoleRunAction>(Assert.IsType<PanelAction>(decoder.Decode([5, 1, 0, 1, 0, 0])).Action);
        Assert.IsType<UnknownAction>(Assert.IsType<PanelAction>(decoder.Decode([5, 2, 0, 1])).Action);
    }

    [Fact]
    public void ModelsEncodeToTheDocumentedBytes()
    {
        Assert.Equal(new byte[] { 6, 5, 0, 0, 0, 0, 0, 0, 0 }, new Ping(5).ToArray());
        Assert.Equal(new byte[] { 7, 255, 255, 255, 255, 255, 255, 255, 255 }, new Pong(-1).ToArray());
        Assert.Equal(new byte[] { 2, 1, 0, 0, 0 }, new HelloAck(ProtocolConstants.VersionMajor, ProtocolConstants.VersionMinor).ToArray());
        Assert.Equal(
            new byte[] { 5, 3, 0, 5, 1 },
            new PanelAction(3, new ControlsSetRateAction(1)).ToArray());
        Assert.Equal(
            new byte[] { 5, 3, 0, A.Controls.SetPaused, 1 },
            new PanelAction(3, new ControlsSetPausedAction(true)).ToArray());
    }

    [Fact]
    public void HelloSurvivesAppendedFields()
    {
        var hello = new Hello(1, 1, "android", "Pixel", 1080, 2400, "Game", "2.0");
        byte[] bytes = [.. hello.ToArray(), 7, 7, 7];
        Assert.Equal(hello.ToArray(), new MessageDecoder().Decode(bytes).ToArray());
        Assert.Equal("android", Assert.IsType<Hello>(new MessageDecoder().Decode(bytes)).Platform);
    }

    [Fact]
    public void PanelListWithBogusCountThrows()
    {
        byte[] bytes = [3, 0xFF, 0xFF, 1, 2, 3];
        Assert.Throws<ProtocolException>(() => new MessageDecoder().Decode(bytes));
    }

    [Fact]
    public void LargeArraysRoundTrip()
    {
        var nodes = Enumerable.Range(0, 5000)
            .Select(i => new SceneNode(i, i - 1, "Type" + i, "label " + i, i % 2 == 0))
            .ToArray();
        var payload = new PanelData(0, new ScenePayload(nodes));

        var decoder = new MessageDecoder();
        decoder.SetPanels([new PanelInfo(0, "scene", "Scene", PanelKind.Scene)]);
        var decoded = Assert.IsType<ScenePayload>(Assert.IsType<PanelData>(decoder.Decode(payload.ToArray())).Payload);
        Assert.Equal(nodes, decoded.Nodes);
    }

    [Fact]
    public void ConstantsMatchTheSpec()
    {
        Assert.Equal(1, ProtocolConstants.VersionMajor);
        Assert.Equal(0, ProtocolConstants.VersionMinor);
        Assert.Equal(6510, ProtocolConstants.DefaultPort);
        Assert.Equal("ws://127.0.0.1:6510/", ProtocolConstants.DefaultUrl);
        Assert.Equal(1, (byte)MessageType.Hello);
        Assert.Equal(7, (byte)MessageType.Pong);
        Assert.Equal(0, (byte)PanelKind.Custom);
        Assert.Equal(10, (byte)PanelKind.Save);
        Assert.Equal(1, (byte)ValueKind.Bool);
        Assert.Equal(7, (byte)ValueKind.Error);
        Assert.Equal(255, (byte)LogLevel.Default);
        Assert.Equal(2, (byte)ConsoleStatus.UnknownCommand);
        Assert.Equal(6, A.Controls.SetLogLevel);
        Assert.Equal(7, A.Inspector.RequestThumbnail);
        Assert.Equal(2, ProtocolConstants.Subtypes.Inspector.Thumbnail);
    }
}
