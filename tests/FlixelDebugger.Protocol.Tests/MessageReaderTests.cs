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

namespace FlixelDebugger.Protocol.Tests;

public class MessageReaderTests
{
  [Fact]
  public void ReadsLittleEndianPrimitives()
  {
    byte[] data =
    [
        0xFF,
        0x02, 0x01,
        0x04, 0x03, 0x02, 0x01,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
        0x00, 0x00, 0x80, 0x3F,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F,
        0x02,
        0x00,
    ];
    var reader = new MessageReader(data);
    Assert.Equal(255, reader.ReadU8());
    Assert.Equal(0x0102, reader.ReadU16());
    Assert.Equal(0x01020304, reader.ReadI32());
    Assert.Equal(0x0102030405060708L, reader.ReadI64());
    Assert.Equal(1.0f, reader.ReadF32());
    Assert.Equal(1.0, reader.ReadF64());
    Assert.True(reader.ReadBool());
    Assert.False(reader.ReadBool());
    Assert.Equal(0, reader.Remaining);
    Assert.Equal(data.Length, reader.Position);
  }

  [Fact]
  public void AnyNonZeroByteIsTrue()
  {
    var reader = new MessageReader(new byte[] { 0, 1, 2, 255 });
    Assert.False(reader.ReadBool());
    Assert.True(reader.ReadBool());
    Assert.True(reader.ReadBool());
    Assert.True(reader.ReadBool());
  }

  [Fact]
  public void ShortInputThrowsForEveryPrimitive()
  {
    Assert.Throws<ProtocolException>(() => new MessageReader([]).ReadU8());
    Assert.Throws<ProtocolException>(() => new MessageReader([1]).ReadU16());
    Assert.Throws<ProtocolException>(() => new MessageReader([1, 2, 3]).ReadI32());
    Assert.Throws<ProtocolException>(() => new MessageReader([1, 2, 3, 4, 5, 6, 7]).ReadI64());
    Assert.Throws<ProtocolException>(() => new MessageReader([1, 2, 3]).ReadF32());
    Assert.Throws<ProtocolException>(() => new MessageReader([1, 2, 3, 4, 5, 6, 7]).ReadF64());
    Assert.Throws<ProtocolException>(() => new MessageReader([]).ReadBool());
    Assert.Throws<ProtocolException>(() => new MessageReader([5]).ReadCount());
  }

  [Fact]
  public void TruncatedInputLeavesNothingConsumedBeyondTheFailure()
  {
    var reader = new MessageReader(new byte[] { 1, 2, 3 });
    Assert.Throws<ProtocolException>(() =>
    {
      var inner = new MessageReader(new byte[] { 1, 2, 3 });
      inner.ReadI32();
    });
    Assert.Equal(0x0201, reader.ReadU16());
    Assert.Equal(1, reader.Remaining);
  }

  [Fact]
  public void RejectsStringLongerThanMessage()
  {
    var bytes = new byte[] { 0xFF, 0x00, (byte)'a' };
    Assert.Throws<ProtocolException>(() => new MessageReader(bytes).ReadString());
  }

  [Fact]
  public void RejectsNegativeByteBlock()
  {
    var bytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
    Assert.Throws<ProtocolException>(() => new MessageReader(bytes).ReadBytes());
  }

  [Fact]
  public void RejectsByteBlockLongerThanMessage()
  {
    var bytes = new byte[] { 5, 0, 0, 0, 1, 2 };
    Assert.Throws<ProtocolException>(() => new MessageReader(bytes).ReadBytes());
  }

  [Fact]
  public void ReadsByteBlock()
  {
    var reader = new MessageReader(new byte[] { 3, 0, 0, 0, 9, 8, 7, 99 });
    Assert.Equal(new byte[] { 9, 8, 7 }, reader.ReadBytes().ToArray());
    Assert.Equal(1, reader.Remaining);
    Assert.Equal(new byte[] { 99 }, reader.ReadRemaining().ToArray());
    Assert.Equal(0, reader.Remaining);
  }

  [Fact]
  public void RejectsArrayCountLargerThanRemainingBytes()
  {
    var bytes = new byte[] { 0xFF, 0xFF, 1, 2, 3 };
    Assert.Throws<ProtocolException>(() =>
    {
      var reader = new MessageReader(bytes);
      reader.ReadArray(static (ref MessageReader r) => r.ReadU8());
    });
  }

  [Fact]
  public void ReadsAnArrayOfElements()
  {
    var reader = new MessageReader(new byte[] { 3, 0, 10, 20, 30, 77 });
    Assert.Equal(new byte[] { 10, 20, 30 }, reader.ReadArray(static (ref MessageReader r) => r.ReadU8()));
    Assert.Equal(1, reader.Remaining);
  }

  [Theory]
  [InlineData(new byte[] { 2, 0, 0xC3, (byte)'a' }, "�")]
  [InlineData(new byte[] { 3, 0, (byte)'a', 0x80, (byte)'b' }, "a�b")]
  [InlineData(new byte[] { 2, 0, 0xE4, 0xB8 }, "�")]
  [InlineData(new byte[] { 3, 0, 0xF5, (byte)'a', (byte)'b' }, "�")]
  [InlineData(new byte[] { 3, 0, (byte)'a', 0xC3, 0xA9 }, "aé")]
  [InlineData(new byte[] { 3, 0, 0xE4, 0xB8, 0x96 }, "世")]
  [InlineData(new byte[] { 4, 0, 0xF0, 0x9F, 0x98, 0x80 }, "\U0001F600")]
  [InlineData(new byte[] { 4, 0, 0xF4, 0x90, 0x80, 0x80 }, "�")]
  [InlineData(new byte[] { 0, 0 }, "")]
  public void MalformedUtf8BecomesReplacementCharacter(byte[] bytes, string expected)
  {
    var reader = new MessageReader(bytes);
    Assert.Equal(expected, reader.ReadString());
    Assert.Equal(0, reader.Remaining);
  }

  [Fact]
  public void EncodedSurrogateCodePointIsKeptLikeJava()
  {
    // The Java reader only rejects code points above U+10FFFF, so ED A0 80 (U+D800) comes through.
    var reader = new MessageReader(new byte[] { 3, 0, 0xED, 0xA0, 0x80 });
    Assert.Equal("\uD800", reader.ReadString());
  }

  [Fact]
  public void ReadsLongStrings()
  {
    var ascii = new string('x', 1000);
    var unicode = string.Concat(Enumerable.Repeat("é世\U0001F600", 100));
    foreach (var text in new[] { ascii, unicode })
    {
      var writer = new MessageWriter();
      writer.BeginMessage(MessageType.Ping).WriteString(text);
      var reader = new MessageReader(writer.Finish().Span);
      reader.ReadU8();
      Assert.Equal(text, reader.ReadString());
    }
  }

  [Fact]
  public void SkipMovesPastBytes()
  {
    var reader = new MessageReader(new byte[] { 1, 2, 3, 4 });
    reader.Skip(3);
    Assert.Equal(1, reader.Remaining);
    Assert.Throws<ProtocolException>(() => new MessageReader(new byte[] { 1 }).Skip(2));
    Assert.Throws<ProtocolException>(() => new MessageReader(new byte[] { 1 }).Skip(-1));
  }

  [Fact]
  public void SkipValueWalksEveryKind()
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
    for (var i = 0; i < 7; i++)
    {
      reader.SkipValue();
    }

    Assert.Equal(0, reader.Remaining);
  }

  [Fact]
  public void SkipValueRejectsUnknownKindAndShortData()
  {
    Assert.Throws<ProtocolException>(() => new MessageReader(new byte[] { 99 }).SkipValue());
    Assert.Throws<ProtocolException>(() => new MessageReader(new byte[] { (byte)ValueKind.Long, 1, 2 }).SkipValue());
    Assert.Throws<ProtocolException>(() => new MessageReader(new byte[] { (byte)ValueKind.String, 5, 0, 1 }).SkipValue());
  }
}
