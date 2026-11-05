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

public class FixtureValueTests
{
  [Fact]
  public void ValueBool()
  {
    var value = Fixture.Value("value_bool");
    Assert.Equal(ValueKind.Bool, value.Kind);
    Assert.True(value.AsBool());
  }

  [Fact]
  public void ValueInt()
  {
    var value = Fixture.Value("value_int");
    Assert.Equal(ValueKind.Int, value.Kind);
    Assert.Equal(-123456, value.AsInt());
  }

  [Fact]
  public void ValueLong()
  {
    var value = Fixture.Value("value_long");
    Assert.Equal(ValueKind.Long, value.Kind);
    Assert.Equal(0x1122334455667788L, value.AsLong());
    Assert.Equal(1234605616436508552L, value.AsLong());
  }

  [Fact]
  public void ValueFloat()
  {
    var value = Fixture.Value("value_float");
    Assert.Equal(ValueKind.Float, value.Kind);
    Assert.Equal(-1.5f, value.AsFloat());
  }

  [Fact]
  public void ValueDouble()
  {
    var value = Fixture.Value("value_double");
    Assert.Equal(ValueKind.Double, value.Kind);
    Assert.Equal(3.141592653589793, value.AsDouble());
  }

  [Fact]
  public void ValueString()
  {
    var value = Fixture.Value("value_string");
    Assert.Equal(ValueKind.String, value.Kind);
    Assert.Equal("hello", value.AsString());
  }

  [Fact]
  public void ValueStringUnicode()
  {
    var value = Fixture.Value("value_string_unicode");
    Assert.Equal(ValueKind.String, value.Kind);
    Assert.Equal(Fixture.Unicode, value.AsString());
  }

  [Fact]
  public void ValueError()
  {
    var value = Fixture.Value("value_error");
    Assert.Equal(ValueKind.Error, value.Kind);
    Assert.Equal("getter threw", value.AsError());
  }
}
