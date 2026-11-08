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

using System.Text.RegularExpressions;
using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Protocol.Tests;

/// <summary>
/// Checks that apply to every fixture file, so a newly copied fixture is covered without new test
/// code.
/// </summary>
public partial class FixtureCoverageTests
{
  public static TheoryData<string> Names => Fixture.AllNamesData();

  [GeneratedRegex(@"^## (\S+)\.bin$", RegexOptions.Multiline)]
  private static partial Regex ReadmeHeading();

  [Fact]
  public void FixtureFolderMatchesItsReadme()
  {
    var readme = File.ReadAllText(Path.Combine(Fixture.Directory, "README.md"));
    var documented = ReadmeHeading().Matches(readme)
        .Select(m => m.Groups[1].Value)
        .Order(StringComparer.Ordinal);
    Assert.Equal(Fixture.AllNames(), documented);
    Assert.Equal(67, Fixture.AllNames().Count());
  }

  [Theory]
  [MemberData(nameof(Names))]
  public void DecodesAndReEncodesByteForByte(string name)
  {
    var bytes = Fixture.Load(name);
    if (name.StartsWith("value_", StringComparison.Ordinal))
    {
      Fixture.Value(name);
      return;
    }

    var message = Fixture.NewDecoder().Decode(bytes);
    Assert.IsNotType<UnknownMessage>(message);
    if (message is PanelData data)
    {
      Assert.IsNotType<UnknownPayload>(data.Payload);
    }

    if (message is PanelAction action)
    {
      Assert.IsNotType<UnknownAction>(action.Action);
    }

    Assert.Equal(bytes, message.ToArray());
  }

  [Theory]
  [MemberData(nameof(Names))]
  public void TruncatedInputThrows(string name)
  {
    var bytes = Fixture.Load(name);
    var cut = bytes[..^1];
    if (name.StartsWith("value_", StringComparison.Ordinal))
    {
      Assert.Throws<ProtocolException>(() =>
      {
        var reader = new MessageReader(cut);
        PropertyValue.Read(ref reader);
      });
      return;
    }

    var decoder = Fixture.NewDecoder();
    Assert.Throws<ProtocolException>(() => decoder.Decode(cut));
  }

  [Theory]
  [MemberData(nameof(Names))]
  public void TrailingBytesAreIgnored(string name)
  {
    var bytes = Fixture.Load(name);
    byte[] padded = [.. bytes, 1, 2, 3, 4, 5];
    if (name.StartsWith("value_", StringComparison.Ordinal))
    {
      var reader = new MessageReader(padded);
      PropertyValue.Read(ref reader);
      Assert.Equal(5, reader.Remaining);
      return;
    }

    var message = Fixture.NewDecoder().Decode(padded);
    Assert.Equal(bytes, message.ToArray());
  }
}
