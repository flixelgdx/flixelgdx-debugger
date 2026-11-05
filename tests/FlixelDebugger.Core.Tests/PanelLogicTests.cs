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

using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Core.Graphs;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Tests;

public class PingHistoryTests
{
  [Fact]
  public void KeepsTheLastHundredTwentyValuesOldestFirst()
  {
    var history = new PingHistory();
    for (var i = 1; i <= 130; i++)
    {
      history.Add(i);
    }

    var buffer = new double[PingHistory.Capacity];
    var count = history.CopyTo(buffer);

    Assert.Equal(120, history.Count);
    Assert.Equal(120, count);
    Assert.Equal(11, buffer[0]);
    Assert.Equal(130, buffer[119]);
    Assert.Equal(130, history.Latest);
    Assert.Equal(130, history.Max());
  }

  [Fact]
  public void ASmallBufferGetsTheNewestValues()
  {
    var history = new PingHistory();
    history.Add(1);
    history.Add(2);
    history.Add(3);

    var buffer = new double[2];

    Assert.Equal(2, history.CopyTo(buffer));
    Assert.Equal([2.0, 3.0], buffer);
  }

  [Fact]
  public void ClearForgetsEverything()
  {
    var history = new PingHistory();
    history.Add(5);

    history.Clear();

    Assert.Equal(0, history.Count);
    Assert.Equal(0, history.Latest);
    Assert.Equal(0, history.Max());
  }
}

public class ConfirmArmTests
{
  private sealed class Clock : TimeProvider
  {
    private long _now;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _now;

    public void Advance(double seconds) => _now += (long)(seconds * 1000);
  }

  [Fact]
  public void TheFirstPressArmsAndTheSecondWithinTheWindowConfirms()
  {
    var clock = new Clock();
    var arm = new ConfirmArm(clock);

    Assert.False(arm.Press());
    Assert.True(arm.IsArmed);
    clock.Advance(2.9);
    Assert.True(arm.Press());
    Assert.False(arm.IsArmed);
  }

  [Fact]
  public void ThePressAfterTheWindowOnlyArmsAgain()
  {
    var clock = new Clock();
    var arm = new ConfirmArm(clock);
    arm.Press();

    clock.Advance(3.1);

    Assert.False(arm.IsArmed);
    Assert.False(arm.Press());
    Assert.True(arm.IsArmed);
  }

  [Fact]
  public void ConfirmingDisarmsSoAThirdPressStartsOver()
  {
    var arm = new ConfirmArm(new Clock());
    arm.Press();
    arm.Press();

    Assert.False(arm.Press());
  }

  [Fact]
  public void DisarmCancelsAndTheWindowCanBeChanged()
  {
    var clock = new Clock();
    var arm = new ConfirmArm(clock, TimeSpan.FromSeconds(1));
    Assert.Equal(TimeSpan.FromSeconds(1), arm.Window);
    arm.Press();
    arm.Disarm();
    Assert.False(arm.IsArmed);

    arm.Press();
    clock.Advance(1.5);

    Assert.False(arm.IsArmed);
    Assert.Equal(TimeSpan.FromSeconds(3), ConfirmArm.DefaultWindow);
  }
}

public class TextureSorterTests
{
  private static TextureEntry T(
      int id, string name, int w, int h, string format, long bytes, int users, bool duplicate) =>
      new(id, name, w, h, format, bytes, users, duplicate);

  private static readonly TextureEntry[] _sample =
  [
      T(1, "b.png", 32, 8, "RGBA8", 300, 3, false),
      T(2, "A.png", 16, 16, "BC7", 100, 1, true),
      T(3, "a.png", 64, 64, "RGBA8", 200, 2, true),
  ];

  private static int[] Ids(TextureSortKey key, bool descending) =>
      [.. TextureSorter.Sort(_sample, key, descending).Select(t => t.Id)];

  [Fact]
  public void SortsByEveryColumnWithTiesBrokenById()
  {
    Assert.Equal([1, 2, 3], Ids(TextureSortKey.Id, false));
    Assert.Equal([3, 2, 1], Ids(TextureSortKey.Id, true));
    Assert.Equal([2, 3, 1], Ids(TextureSortKey.Name, false));
    Assert.Equal([1, 2, 3], Ids(TextureSortKey.Name, true));
    Assert.Equal([1, 2, 3], Ids(TextureSortKey.Size, false));
    Assert.Equal([3, 1, 2], Ids(TextureSortKey.Size, true));
    Assert.Equal([2, 1, 3], Ids(TextureSortKey.Format, false));
    Assert.Equal([2, 3, 1], Ids(TextureSortKey.GpuBytes, false));
    Assert.Equal([1, 3, 2], Ids(TextureSortKey.GpuBytes, true));
    Assert.Equal([2, 3, 1], Ids(TextureSortKey.Users, false));
    Assert.Equal([1, 2, 3], Ids(TextureSortKey.Duplicate, false));
    Assert.Equal([2, 3, 1], Ids(TextureSortKey.Duplicate, true));
  }

  [Fact]
  public void DoesNotChangeTheSourceList()
  {
    var before = _sample.Select(t => t.Id).ToArray();

    TextureSorter.Sort(_sample, TextureSortKey.GpuBytes, true);

    Assert.Equal(before, _sample.Select(t => t.Id));
  }
}

public class SaveListingTests
{
  private static SavePayload Sample() => new(
      [
          new SaveRow("player.coins", "12", ValueKind.Double, false),
          new SaveRow("player.name", "\"Bob\"", ValueKind.String, true),
          new SaveRow("Settings.Volume", "0.5", ValueKind.Double, false),
      ],
      ["old.key"],
      "ok",
      false);

  [Fact]
  public void BuildsRowsThenRemovedPathsAndPrefixesUnsavedOnes()
  {
    var rows = SaveListing.Build(Sample(), null);

    Assert.Equal(
        ["player.coins", "* player.name", "Settings.Volume", "old.key"], rows.Select(r => r.Label));
    Assert.Equal(
        ["player.coins", "player.name", "Settings.Volume", "old.key"], rows.Select(r => r.Path));
    Assert.Equal([false, true, false, false], rows.Select(r => r.Unsaved));
    Assert.Equal([false, false, false, true], rows.Select(r => r.Removed));
    Assert.Equal(SaveListing.RemovedText, rows[3].Display);
  }

  [Theory]
  [InlineData("PLAYER", 2)]
  [InlineData("settings.v", 1)]
  [InlineData("  old ", 1)]
  [InlineData(".", 4)]
  [InlineData("", 4)]
  [InlineData("zzz", 0)]
  public void FiltersByACaseInsensitiveSubstringOfThePath(string filter, int expected)
  {
    Assert.Equal(expected, SaveListing.Build(Sample(), filter).Count);
  }

  [Fact]
  public void ANullPayloadHasNoRows()
  {
    Assert.Empty(SaveListing.Build(null, "x"));
  }
}

public class ValueFormatTests
{
  [Theory]
  [InlineData(3.0, "3")]
  [InlineData(-3.0, "-3")]
  [InlineData(1.5, "1.50")]
  [InlineData(0.004, "0.00")]
  [InlineData(1234567.891, "1234567.89")]
  [InlineData(0.0, "0")]
  [InlineData(double.NaN, "NaN")]
  public void NumbersAreIntegersWhenWholeAndTwoDecimalsOtherwise(double value, string expected)
  {
    Assert.Equal(expected, ValueFormat.Number(value));
  }

  [Fact]
  public void DisplayCoversEveryKind()
  {
    Assert.Equal("true", ValueFormat.Display(PropertyValue.FromBool(true)));
    Assert.Equal("-4", ValueFormat.Display(PropertyValue.FromInt(-4)));
    Assert.Equal("9000000000", ValueFormat.Display(PropertyValue.FromLong(9000000000)));
    Assert.Equal("2.50", ValueFormat.Display(PropertyValue.FromFloat(2.5f)));
    Assert.Equal("7", ValueFormat.Display(PropertyValue.FromDouble(7.0)));
    Assert.Equal("hi", ValueFormat.Display(PropertyValue.FromString("hi")));
    Assert.Equal("error: boom", ValueFormat.Display(PropertyValue.FromError("boom")));
    Assert.True(ValueFormat.IsError(PropertyValue.FromError("x")));
    Assert.False(ValueFormat.IsError(PropertyValue.FromInt(1)));
  }

  [Fact]
  public void EditTextKeepsAllDigits()
  {
    Assert.Equal("0.1", ValueFormat.EditText(PropertyValue.FromFloat(0.1f)));
    Assert.Equal("1.2345678", ValueFormat.EditText(PropertyValue.FromDouble(1.2345678)));
    Assert.Equal("5", ValueFormat.EditText(PropertyValue.FromInt(5)));
  }

  [Theory]
  [InlineData(ValueKind.Int, "42", true)]
  [InlineData(ValueKind.Int, " -7 ", true)]
  [InlineData(ValueKind.Int, "1.5", false)]
  [InlineData(ValueKind.Int, "99999999999", false)]
  [InlineData(ValueKind.Long, "99999999999", true)]
  [InlineData(ValueKind.Float, "1.5", true)]
  [InlineData(ValueKind.Float, "1e3", true)]
  [InlineData(ValueKind.Float, "NaN", false)]
  [InlineData(ValueKind.Double, "abc", false)]
  [InlineData(ValueKind.Double, "", false)]
  [InlineData(ValueKind.Bool, "True", true)]
  [InlineData(ValueKind.Bool, "yes", false)]
  [InlineData(ValueKind.String, "", true)]
  [InlineData(ValueKind.Error, "x", false)]
  public void TryParseAcceptsOnlyValidTextOfTheRowsKind(ValueKind kind, string text, bool valid)
  {
    Assert.Equal(valid, ValueFormat.TryParse(kind, text, out var value));
    if (valid)
    {
      Assert.Equal(kind, value.Kind);
    }
  }

  [Fact]
  public void TryParseUsesTheInvariantCultureAndKeepsStringsAsTyped()
  {
    Assert.True(ValueFormat.TryParse(ValueKind.Double, "2.5", out var number));
    Assert.Equal(2.5, number.AsDouble());
    Assert.True(ValueFormat.TryParse(ValueKind.String, "  spaced  ", out var text));
    Assert.Equal("  spaced  ", text.AsString());
  }
}

public class StatsTableTests
{
  [Fact]
  public void BuildsThirteenRowsInAFixedOrder()
  {
    var rows = StatsTable.Build(null, null);

    Assert.Equal(StatsTable.RowCount, rows.Length);
    Assert.Equal(
        ["FPS", "Frame ms", "Java heap MB", "Native MB", "Objects", "Draw calls", "Triangles",
            "Ping", "State", "Elapsed", "X", "Y", "Zoom"],
        rows.Select(r => r.Name));
    Assert.Equal(8, rows.Count(r => r.Group == StatsGroup.Client));
    Assert.Equal(2, rows.Count(r => r.Group == StatsGroup.World));
    Assert.Equal(3, rows.Count(r => r.Group == StatsGroup.Camera));
  }

  [Fact]
  public void FormatsASampleAndTheTone()
  {
    var sample = new StatsPayload(
        59.6f, 16.6f, 5, 1, 100, 2000, 6000, 3, 2, 4, 64, 32, 16, 8, 250, 1.5f, 2.5f, false, 12.34,
        10, 20, 1.5f);

    var rows = StatsTable.Build(sample, 4.6).ToDictionary(r => r.Name);

    Assert.Equal("60", rows["FPS"].Value);
    Assert.Equal("16.60", rows["Frame ms"].Value);
    Assert.Equal("5 ms", rows["Ping"].Value);
    Assert.Equal("RUNNING", rows["State"].Value);
    Assert.Equal(StatsTone.Good, rows["State"].Tone);
    Assert.Equal("12.3 s", rows["Elapsed"].Value);
    Assert.Equal("1.50", rows["Zoom"].Value);

    var paused = StatsTable.Build(sample with { Paused = true }, null).ToDictionary(r => r.Name);
    Assert.Equal("PAUSED", paused["State"].Value);
    Assert.Equal(StatsTone.Warning, paused["State"].Tone);
    Assert.Equal("n/a", paused["Ping"].Value);
  }
}

public class GraphCatalogTests
{
  [Fact]
  public void ListsTheFourSectionsWithTheirMinimumScales()
  {
    var sections = GraphCatalog.Sections;

    Assert.Equal(["Core", "Render", "Memory", "Network"], sections.Select(s => s.Title));
    Assert.Equal([true, false, false, false], sections.Select(s => s.ExpandedByDefault));
    Assert.Equal(["FPS", "Frame ms", "Draw calls"], sections[0].Graphs.Select(g => g.Title));
    Assert.Equal(10, sections[0].Graphs[0].MinScale);
    Assert.Equal(10, sections[0].Graphs[2].MinScale);
    Assert.Equal(100, sections[1].Graphs.Single(g => g.Title == "Triangles").MinScale);
    Assert.Equal(10, sections[2].Graphs.Single(g => g.Title == "Heap MB").MinScale);
    Assert.Null(sections[3].Graphs.Single(g => g.Title == "Ping ms").Field);
    Assert.All(
        sections.SelectMany(s => s.Graphs).Where(g => g.Title != "Ping ms"),
        g => Assert.NotNull(g.Field));
  }
}

public class GraphScaleHorizontalTests
{
  [Fact]
  public void TheNewestSampleSitsAtTheRightEdge()
  {
    Assert.Equal(119, GraphScale.MapX(119, 120, 119), 6);
    Assert.Equal(0, GraphScale.MapX(0, 120, 119), 6);
    Assert.Equal(200, GraphScale.MapX(0, 1, 200), 6);
    Assert.Equal(200 - (200.0 / 119), GraphScale.MapX(0, 2, 200), 6);
  }

  [Fact]
  public void IndexAtIsTheInverseOfMapXAndClamps()
  {
    for (var i = 0; i < 120; i++)
    {
      Assert.Equal(i, GraphScale.IndexAt(GraphScale.MapX(i, 120, 300), 120, 300));
    }

    Assert.Equal(49, GraphScale.IndexAt(300, 50, 300));
    Assert.Equal(0, GraphScale.IndexAt(0, 50, 300));
    Assert.Equal(49, GraphScale.IndexAt(9999, 50, 300));
    Assert.Equal(-1, GraphScale.IndexAt(10, 0, 300));
  }
}
