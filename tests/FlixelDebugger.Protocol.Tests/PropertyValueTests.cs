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

public class PropertyValueTests
{
  [Fact]
  public void FactoriesSetTheKindAndRoundTripTheData()
  {
    Assert.Equal(ValueKind.Bool, PropertyValue.FromBool(true).Kind);
    Assert.True(PropertyValue.FromBool(true).AsBool());
    Assert.False(PropertyValue.FromBool(false).AsBool());
    Assert.Equal(int.MinValue, PropertyValue.FromInt(int.MinValue).AsInt());
    Assert.Equal(long.MinValue, PropertyValue.FromLong(long.MinValue).AsLong());
    Assert.Equal(float.MaxValue, PropertyValue.FromFloat(float.MaxValue).AsFloat());
    Assert.Equal(double.Epsilon, PropertyValue.FromDouble(double.Epsilon).AsDouble());
    Assert.Equal("text", PropertyValue.FromString("text").AsString());
    Assert.Equal("oops", PropertyValue.FromError("oops").AsError());
  }

  [Fact]
  public void NullTextBecomesEmpty()
  {
    Assert.Equal(string.Empty, PropertyValue.FromString(null).AsString());
    Assert.Equal(string.Empty, PropertyValue.FromError(null).AsError());
  }

  [Fact]
  public void WrongAccessorThrows()
  {
    var value = PropertyValue.FromInt(3);
    Assert.Throws<InvalidOperationException>(() => _ = value.AsBool());
    Assert.Throws<InvalidOperationException>(() => _ = value.AsLong());
    Assert.Throws<InvalidOperationException>(() => _ = value.AsFloat());
    Assert.Throws<InvalidOperationException>(() => _ = value.AsDouble());
    Assert.Throws<InvalidOperationException>(() => _ = value.AsString());
    Assert.Throws<InvalidOperationException>(() => _ = value.AsError());
    Assert.Throws<InvalidOperationException>(() => _ = PropertyValue.FromString("x").AsError());
    Assert.Throws<InvalidOperationException>(() => _ = PropertyValue.FromError("x").AsString());
  }

  [Fact]
  public void EqualityComparesKindAndData()
  {
    Assert.Equal(PropertyValue.FromInt(1), PropertyValue.FromInt(1));
    Assert.NotEqual(PropertyValue.FromInt(1), PropertyValue.FromInt(2));
    Assert.NotEqual(PropertyValue.FromInt(1), PropertyValue.FromLong(1));
    Assert.NotEqual(PropertyValue.FromString("a"), PropertyValue.FromError("a"));
    Assert.Equal(PropertyValue.FromFloat(float.NaN), PropertyValue.FromFloat(float.NaN));
    Assert.Equal(
        PropertyValue.FromString("a").GetHashCode(), PropertyValue.FromString("a").GetHashCode());
  }

  [Fact]
  public void FloatsKeepTheirExactBits()
  {
    var payload = BitConverter.Int32BitsToSingle(0x7FC12345);
    var writer = new MessageWriter();
    PropertyValue.FromFloat(payload).Write(writer.BeginMessage(MessageType.Ping));
    var bytes = writer.Finish().ToArray();
    Assert.Equal(new byte[] { 6, (byte)ValueKind.Float, 0x45, 0x23, 0xC1, 0x7F }, bytes);
  }

  [Fact]
  public void UnknownKindThrowsWhenRead()
  {
    Assert.Throws<ProtocolException>(() =>
    {
      var reader = new MessageReader(new byte[] { 99, 0, 0, 0, 0 });
      PropertyValue.Read(ref reader);
    });
  }

  [Fact]
  public void ToStringIsReadable()
  {
    Assert.Equal("Float: 2.5", PropertyValue.FromFloat(2.5f).ToString());
    Assert.Equal("String: \"hi\"", PropertyValue.FromString("hi").ToString());
    Assert.Equal("Invalid", default(PropertyValue).ToString());
  }
}
