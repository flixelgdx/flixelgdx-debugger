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

using System.Buffers.Binary;
using System.Text;

namespace FlixelDebugger.Protocol.Tests;

public class MessageWriterTests
{
    private static byte[] Encode(string? text)
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping).WriteString(text);
        return writer.Finish().ToArray();
    }

    private static string Decode(byte[] message)
    {
        var reader = new MessageReader(message);
        reader.ReadU8();
        return reader.ReadString();
    }

    [Fact]
    public void WritesLittleEndian()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Hello)
            .WriteU16(0x0102)
            .WriteI32(0x01020304)
            .WriteI64(0x0102030405060708L)
            .WriteF32(1.0f)
            .WriteF64(1.0)
            .WriteBool(true)
            .WriteBool(false);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x02, 0x01,
                0x04, 0x03, 0x02, 0x01,
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
                0x00, 0x00, 0x80, 0x3F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F,
                0x01,
                0x00,
            },
            writer.Finish().ToArray());
    }

    [Fact]
    public void WritesEveryValueKind()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping)
            .WriteValue(true)
            .WriteValue(1)
            .WriteValue(2L)
            .WriteValue(3f)
            .WriteValue(4.0)
            .WriteValue("five")
            .WriteErrorValue("six");

        var reader = new MessageReader(writer.Finish().Span);
        reader.ReadU8();
        Assert.Equal(PropertyValue.FromBool(true), PropertyValue.Read(ref reader));
        Assert.Equal(PropertyValue.FromInt(1), PropertyValue.Read(ref reader));
        Assert.Equal(PropertyValue.FromLong(2), PropertyValue.Read(ref reader));
        Assert.Equal(PropertyValue.FromFloat(3), PropertyValue.Read(ref reader));
        Assert.Equal(PropertyValue.FromDouble(4), PropertyValue.Read(ref reader));
        Assert.Equal(PropertyValue.FromString("five"), PropertyValue.Read(ref reader));
        Assert.Equal(PropertyValue.FromError("six"), PropertyValue.Read(ref reader));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void WritesBytesWithLengthPrefix()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping).WriteBytes([9, 8, 7]);
        Assert.Equal(new byte[] { 6, 3, 0, 0, 0, 9, 8, 7 }, writer.Finish().ToArray());
    }

    [Fact]
    public void GrowsByDoublingAndKeepsData()
    {
        var writer = new MessageWriter(16);
        Assert.Equal(16, writer.Capacity);

        writer.BeginMessage(MessageType.Ping);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteI32(i);
        }

        Assert.Equal(401, writer.Size);
        Assert.Equal(512, writer.Capacity);

        var reader = new MessageReader(writer.Finish().Span);
        reader.ReadU8();
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(i, reader.ReadI32());
        }
    }

    [Fact]
    public void ReusesItsBufferBetweenMessages()
    {
        var writer = new MessageWriter(16);
        writer.BeginMessage(MessageType.Ping).WriteString(new string('a', 500));
        var capacity = writer.Capacity;

        writer.BeginMessage(MessageType.Pong).WriteI64(7);
        Assert.Equal(capacity, writer.Capacity);
        Assert.Equal(9, writer.Size);
        Assert.Equal(new byte[] { 7, 7, 0, 0, 0, 0, 0, 0, 0 }, writer.Finish().ToArray());
    }

    [Fact]
    public void ArrayCountBackpatchSurvivesGrowth()
    {
        var writer = new MessageWriter(16);
        writer.BeginMessage(MessageType.PanelList).BeginArray();
        for (var i = 0; i < 50; i++)
        {
            writer.WriteString("element " + i);
        }

        writer.EndArray(50);

        var reader = new MessageReader(writer.Finish().Span);
        reader.ReadU8();
        Assert.Equal(50, reader.ReadCount());
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal("element " + i, reader.ReadString());
        }
    }

    [Fact]
    public void NestedArraysFillTheirOwnCounts()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.PanelData).BeginArray();
        writer.WriteU8(1).BeginArray().WriteU8(10).WriteU8(11).EndArray(2);
        writer.WriteU8(2).BeginArray().WriteU8(20).EndArray(1);
        writer.EndArray(2);

        Assert.Equal(
            new byte[] { 4, 2, 0, 1, 2, 0, 10, 11, 2, 1, 0, 20 },
            writer.Finish().ToArray());
    }

    [Fact]
    public void RejectsBadArrayUse()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping);
        Assert.Throws<ProtocolException>(() => writer.EndArray(0));

        writer.BeginArray();
        Assert.Throws<ProtocolException>(() => _ = writer.Finish());
        Assert.Throws<ProtocolException>(() => writer.EndArray(-1));
        Assert.Throws<ProtocolException>(() => writer.EndArray(65536));
        writer.EndArray(65535);
        writer.Finish();

        writer.BeginMessage(MessageType.Ping);
        for (var i = 0; i < MessageWriter.MaxDepth; i++)
        {
            writer.BeginArray();
        }

        Assert.Throws<ProtocolException>(writer.BeginArray);
    }

    [Fact]
    public void WriteArrayRejectsMoreThan65535Elements()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping);
        var tooMany = new byte[65536];
        Assert.Throws<ProtocolException>(() => writer.WriteArray(tooMany, static (w, b) => w.WriteU8(b)));
    }

    [Fact]
    public void StringTruncatesOnACharacterBoundary()
    {
        // Three bytes per character: 21845 characters fill 65535 bytes exactly, so one more must be cut.
        var three = Encode(new string('世', 21846));
        Assert.Equal(65535, BinaryPrimitives.ReadUInt16LittleEndian(three.AsSpan(1)));
        Assert.Equal(new string('世', 21845), Decode(three));

        // Four bytes per character: 16383 characters is 65532 bytes, and the next one would need 65536.
        var emoji = char.ConvertFromUtf32(0x1F600);
        var four = Encode(string.Concat(Enumerable.Repeat(emoji, 16384)));
        Assert.Equal(65532, BinaryPrimitives.ReadUInt16LittleEndian(four.AsSpan(1)));
        Assert.Equal(string.Concat(Enumerable.Repeat(emoji, 16383)), Decode(four));

        // A pair must never be split: ASCII filler then a pair that straddles the limit.
        var straddle = Encode(new string('a', 65534) + emoji);
        Assert.Equal(65534, BinaryPrimitives.ReadUInt16LittleEndian(straddle.AsSpan(1)));
        Assert.Equal(new string('a', 65534), Decode(straddle));

        // Plain ASCII is simply cut at 65535 bytes.
        var ascii = Encode(new string('a', 70000));
        Assert.Equal(65535, BinaryPrimitives.ReadUInt16LittleEndian(ascii.AsSpan(1)));
        Assert.Equal(1 + 2 + 65535, ascii.Length);
    }

    [Fact]
    public void StringTruncationNeverProducesInvalidUtf8()
    {
        var strict = new UTF8Encoding(false, true);
        foreach (var unit in new[] { "a", "é", "世", char.ConvertFromUtf32(0x1F600) })
        {
            var text = string.Concat(Enumerable.Repeat(unit, 70000));
            var encoded = Encode(text);
            var body = encoded.AsSpan(3);
            Assert.Equal(body.Length, BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(1)));
            Assert.True(body.Length <= 65535);
            _ = strict.GetString(body);
        }
    }

    public static TheoryData<string, string> SurrogateCases => new()
    {
        { "a\uD83Db", "a?b" },
        { "a\uDE00b", "a?b" },
        { "\uD83D", "?" },
        { "\uDE00\uD83D", "??" },
        { "\uD83D\uD83D\uDE00", "?\U0001F600" },
    };

    [Theory]
    [MemberData(nameof(SurrogateCases))]
    public void UnpairedSurrogateBecomesQuestionMark(string input, string expected)
    {
        Assert.Equal(expected, Decode(Encode(input)));
    }

    [Fact]
    public void NullStringIsEmpty()
    {
        Assert.Equal(new byte[] { 6, 0, 0 }, Encode(null));
        Assert.Equal(string.Empty, Decode(Encode(null)));
        Assert.Equal(new byte[] { 6, 0, 0 }, Encode(string.Empty));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData(Fixture.Unicode)]
    [InlineData("é߿ࠀ￿")]
    public void StringMatchesStandardUtf8Encoding(string text)
    {
        var encoded = Encode(text);
        var expected = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, encoded[3..]);
        Assert.Equal(expected.Length, BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(1)));
    }

    [Fact]
    public void WriteRawCopiesBytesWithoutAPrefix()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping).WriteRaw([1, 2, 3]);
        Assert.Equal(new byte[] { 6, 1, 2, 3 }, writer.Finish().ToArray());
    }

    [Fact]
    public void DefaultPropertyValueCannotBeWritten()
    {
        var writer = new MessageWriter();
        writer.BeginMessage(MessageType.Ping);
        Assert.Throws<ProtocolException>(() => writer.WriteValue(default(PropertyValue)));
    }
}
