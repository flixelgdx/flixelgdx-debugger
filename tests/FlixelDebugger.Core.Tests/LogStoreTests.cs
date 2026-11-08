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
using FlixelDebugger.Protocol;

namespace FlixelDebugger.Core.Tests;

public class LogStoreTests
{
  private static long[] Seqs(LogStore store) => [.. store.GetView().Select(e => e.Seq)];

  [Fact]
  public void RingEvictsTheOldestLineAfterFiveHundred()
  {
    var store = new LogStore();
    for (var i = 1; i <= 501; i++)
    {
      store.Add(TestData.Log(i));
    }

    var view = store.GetView();
    Assert.Equal(LogStore.Capacity, store.Count);
    Assert.Equal(500, view.Count);
    Assert.Equal(2, view[0].Seq);
    Assert.Equal(501, view[^1].Seq);
    Assert.False(store.TryGet(1, out _));
    Assert.True(store.TryGet(2, out _));
  }

  [Fact]
  public void NormalBatchesAppend()
  {
    var store = new LogStore();
    store.Apply(TestData.Logs(false, TestData.Log(1), TestData.Log(2)));
    store.Apply(TestData.Logs(false, TestData.Log(3)));
    Assert.Equal([1L, 2, 3], Seqs(store));
  }

  [Fact]
  public void BacklogBatchReplacesTheContents()
  {
    var store = new LogStore();
    store.Apply(TestData.Logs(false, TestData.Log(1), TestData.Log(2)));
    store.Apply(TestData.Logs(true, TestData.Log(7), TestData.Log(8)));
    Assert.Equal([7L, 8], Seqs(store));
  }

  [Fact]
  public void BacklogLargerThanTheRingKeepsTheNewestLines()
  {
    var store = new LogStore();
    var lines = Enumerable.Range(1, 600).Select(i => TestData.Log(i)).ToArray();
    store.Apply(TestData.Logs(true, lines));
    var view = store.GetView();
    Assert.Equal(500, view.Count);
    Assert.Equal(101, view[0].Seq);
  }

  [Fact]
  public void SummaryHasTimeLevelTagAndFirstLineOfTheMessage()
  {
    var line = TestData.Log(1, "first\nsecond", LogLevel.Warn, "Audio", time: 12.3456);
    Assert.Equal("[12.346] [WARN] [Audio] first", LogStore.FormatSummary(line));
    Assert.Equal(
        "[1.500] [DEBUG] [Game] hi", LogStore.FormatSummary(TestData.Log(2, "hi", LogLevel.Debug)));
    Assert.Equal(
        "[1.500] [ERROR] [Game] one",
        LogStore.FormatSummary(TestData.Log(3, "one\r\ntwo", LogLevel.Error)));
  }

  [Fact]
  public void DetailsHaveLevelTagCallSiteAndFullMessage()
  {
    var line = TestData.Log(1, "first\nsecond", LogLevel.Info, "Audio", callSite: "Sound.java:42");
    Assert.Equal("[INFO] [Audio] [Sound.java:42] first\nsecond", LogStore.FormatDetails(line));
  }

  [Fact]
  public void DetailsAppendTheStackTrace()
  {
    var line = TestData.Log(
        1, "boom", LogLevel.Error, "Game", stack: "at A.b(A.java:1)\nat C.d(C.java:2)");
    Assert.Equal(
        "[ERROR] [Game] [Main.java:10] boom\nat A.b(A.java:1)\nat C.d(C.java:2)",
        LogStore.FormatDetails(line));
  }

  [Fact]
  public void LevelTogglesHideLinesFromTheViewOnly()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1, level: LogLevel.Debug));
    store.Add(TestData.Log(2, level: LogLevel.Info));
    store.Add(TestData.Log(3, level: LogLevel.Warn));
    store.Add(TestData.Log(4, level: LogLevel.Error));

    store.SetLevelShown(LogLevel.Debug, false);
    store.SetLevelShown(LogLevel.Warn, false);

    Assert.Equal([2L, 4], Seqs(store));
    Assert.Equal(4, store.Count);
    Assert.False(store.IsLevelShown(LogLevel.Debug));
    Assert.True(store.IsLevelShown(LogLevel.Info));

    store.SetLevelShown(LogLevel.Debug, true);
    Assert.Equal([1L, 2, 4], Seqs(store));
  }

  [Fact]
  public void UncaughtLinesFollowOnlyTheUncaughtToggle()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1, level: LogLevel.Error, uncaught: true));
    store.Add(TestData.Log(2, level: LogLevel.Error));

    store.SetLevelShown(LogLevel.Error, false);
    Assert.Equal([1L], Seqs(store));

    store.ShowUncaught = false;
    Assert.Empty(Seqs(store));

    store.SetLevelShown(LogLevel.Error, true);
    Assert.Equal([2L], Seqs(store));
  }

  [Fact]
  public void SearchIgnoresCaseAndMatchesTheWholeSummary()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1, "Loaded texture", tag: "Assets"));
    store.Add(TestData.Log(2, "Played sound", tag: "Audio"));
    store.Add(TestData.Log(3, "Second line\nmentions texture", tag: "Other"));

    store.Search = "TEXTURE";
    Assert.Equal([1L], Seqs(store));

    store.Search = "audio";
    Assert.Equal([2L], Seqs(store));

    store.Search = "[info]";
    Assert.Equal(3, store.GetView().Count);

    store.Search = string.Empty;
    Assert.Equal(3, store.GetView().Count);
  }

  [Fact]
  public void SearchOnlySeesTheFirstLineOfAMessage()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1, "headline\nhidden detail"));
    store.Search = "hidden";
    Assert.Empty(store.GetView());
  }

  [Fact]
  public void CopyViewReusesTheCallersList()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1));
    store.Add(TestData.Log(2));
    var list = new List<LogEntry> { default };
    store.CopyView(list);
    Assert.Equal(2, list.Count);
    Assert.Equal(1, list[0].Seq);
  }

  [Fact]
  public void SelectionIsRememberedBySequenceNumber()
  {
    var store = new LogStore();
    store.Add(TestData.Log(5, "selected"));
    Assert.True(store.Select(5));
    Assert.Equal(5, store.SelectedSeq);
    Assert.Equal("selected", store.SelectedEntry!.Value.Line.Message);
    Assert.Equal("[INFO] [Game] [Main.java:10] selected", store.SelectedDetails);
  }

  [Fact]
  public void SelectingAMissingLineClearsTheSelection()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1));
    store.Select(1);
    Assert.False(store.Select(99));
    Assert.Null(store.SelectedSeq);
    Assert.Null(store.SelectedDetails);
  }

  [Fact]
  public void SelectionClearsWhenTheLineIsEvicted()
  {
    var store = new LogStore();
    for (var i = 1; i <= 500; i++)
    {
      store.Add(TestData.Log(i));
    }

    store.Select(2);
    store.Add(TestData.Log(501));
    Assert.Equal(2, store.SelectedSeq);

    store.Add(TestData.Log(502));
    Assert.Null(store.SelectedSeq);
  }

  [Fact]
  public void SelectionSurvivesWhileHiddenByAFilter()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1, level: LogLevel.Debug));
    store.Select(1);
    store.SetLevelShown(LogLevel.Debug, false);
    Assert.Equal(1, store.SelectedSeq);
  }

  [Fact]
  public void BacklogKeepsASelectionThatIsStillPresent()
  {
    var store = new LogStore();
    store.Apply(TestData.Logs(false, TestData.Log(1), TestData.Log(2)));
    store.Select(2);
    store.Apply(TestData.Logs(true, TestData.Log(2), TestData.Log(3)));
    Assert.Equal(2, store.SelectedSeq);

    store.Apply(TestData.Logs(true, TestData.Log(8)));
    Assert.Null(store.SelectedSeq);
  }

  [Fact]
  public void ClearRemovesLinesAndSelection()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1));
    store.Select(1);
    store.Clear();
    Assert.Equal(0, store.Count);
    Assert.Null(store.SelectedSeq);
  }

  [Fact]
  public void VersionChangesWhenAnythingChanges()
  {
    var store = new LogStore();
    var version = store.Version;
    store.Add(TestData.Log(1));
    Assert.NotEqual(version, store.Version);
    version = store.Version;
    store.Search = "x";
    Assert.NotEqual(version, store.Version);
  }

  [Fact]
  public void LocalLinesKeepTheirPlaceBetweenGameLines()
  {
    var store = new LogStore();
    store.Add(TestData.Log(1, time: 4.0));
    store.AddLocal("> clear");
    store.Add(TestData.Log(2, time: 5.0));

    var view = store.GetView();
    Assert.Equal([1L, -1, 2], [.. view.Select(e => e.Seq)]);
    Assert.False(view[0].IsLocal);
    Assert.True(view[1].IsLocal);
    Assert.Equal(4.0, view[1].Line.Time);
    Assert.Equal("[4.000] [INFO] [debugger] > clear", view[1].Summary);
  }

  [Fact]
  public void LocalLinesGetDecreasingNegativeNumbers()
  {
    var store = new LogStore();
    store.AddLocal("a");
    store.AddLocal("b", LogLevel.Warn);
    Assert.Equal([-1L, -2], Seqs(store));
    Assert.Equal(LogLevel.Warn, store.GetView()[1].Line.Level);
    Assert.Equal(0.0, store.GetView()[0].Line.Time);
  }

  [Fact]
  public void LevelFiltersNeverHideLocalLinesButTheSearchDoes()
  {
    var store = new LogStore();
    store.AddLocal("> hello");
    store.AddLocal("warning text", LogLevel.Warn);
    store.SetLevelShown(LogLevel.Info, false);
    store.SetLevelShown(LogLevel.Warn, false);
    store.ShowUncaught = false;
    Assert.Equal(2, store.GetView().Count);

    store.Search = "WARNING";
    Assert.Equal([-2L], Seqs(store));
  }

  [Fact]
  public void LocalLinesCanBeSelectedAndAreEvictedLikeOthers()
  {
    var store = new LogStore();
    store.AddLocal("first");
    Assert.True(store.Select(-1));
    for (var i = 1; i <= LogStore.Capacity; i++)
    {
      store.Add(TestData.Log(i));
    }

    Assert.Null(store.SelectedSeq);
    Assert.False(store.TryGet(-1, out _));
  }

  [Fact]
  public void LocalLinesChangeTheVersionAndClearWithTheRest()
  {
    var store = new LogStore();
    var version = store.Version;
    store.AddLocal("x");
    Assert.NotEqual(version, store.Version);
    store.Clear();
    Assert.Equal(0, store.Count);
  }

  [Fact]
  public void CopySinceIgnoresTheViewFiltersAndLocalLines()
  {
    var store = new LogStore();
    store.Apply(TestData.Logs(
        false,
        TestData.Log(1, "a", LogLevel.Debug),
        TestData.Log(2, "b", LogLevel.Error),
        TestData.Log(3, "c", LogLevel.Info, uncaught: true)));
    store.AddLocal("echo");
    store.SetLevelShown(LogLevel.Debug, false);
    store.ShowUncaught = false;
    store.Search = "zzz";
    var lines = new List<LogEntry>();

    store.CopySince(0, lines);
    Assert.Equal([1L, 2, 3], lines.Select(e => e.Seq));

    store.CopySince(2, lines);
    Assert.Equal([3L], lines.Select(e => e.Seq));

    store.CopySince(3, lines);
    Assert.Empty(lines);
  }
}
