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

using System.Collections.Specialized;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.Tests;

public class LogsPanelViewModelTests
{
  private static (Harness Harness, FakeGameSession Game, LogsPanelViewModel Logs) Start()
  {
    var h = new Harness();
    _ = h.Main;
    var game = h.Connect();
    return (h, game, h.Logs);
  }

  private static void AddLines(FakeGameSession game, int from, int to)
  {
    for (var i = from; i <= to; i++)
    {
      game.Logs.Add(TestData.Log(i, "line " + i));
    }
  }

  [Fact]
  public void TheListOnlyChangesWhenTheTimerTicks()
  {
    var (h, game, logs) = Start();

    AddLines(game, 1, 1000);
    Assert.Empty(logs.Rows);

    h.Scheduler.Tick();
    Assert.Equal(LogStore.Capacity, logs.Rows.Count);
    Assert.Equal("line 501", logs.Rows[0].Entry.Line.Message);
  }

  [Fact]
  public void ATickWithoutNewDataChangesNothing()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, 10);
    h.Scheduler.Tick();
    var changes = 0;
    logs.Rows.CollectionChanged += (_, _) => changes++;

    h.Scheduler.Tick();
    h.Scheduler.Tick();

    Assert.Equal(0, changes);
  }

  [Fact]
  public void NewLinesAreAppendedAndEvictedLinesLeaveFromTheFrontWithoutAReset()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, LogStore.Capacity);
    h.Scheduler.Tick();
    var first = logs.Rows[0];
    var actions = new List<NotifyCollectionChangedAction>();
    logs.Rows.CollectionChanged += (_, e) => actions.Add(e.Action);

    AddLines(game, LogStore.Capacity + 1, LogStore.Capacity + 3);
    h.Scheduler.Tick();

    Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    Assert.Equal(LogStore.Capacity, logs.Rows.Count);
    Assert.DoesNotContain(first, logs.Rows);
    Assert.Equal(LogStore.Capacity + 3, logs.Rows[^1].Seq);
    Assert.Equal(4, logs.Rows[0].Seq);
  }

  [Fact]
  public void LevelTogglesAndTheFilterTextAreForwardedToTheStore()
  {
    var (h, game, logs) = Start();
    game.Logs.Add(TestData.Log(1, "disk full", LogLevel.Error));
    game.Logs.Add(TestData.Log(2, "loaded", LogLevel.Info));
    game.Logs.Add(TestData.Log(3, "slow", LogLevel.Warn));
    game.Logs.Add(TestData.Log(4, "boom", LogLevel.Error, uncaught: true));
    h.Scheduler.Tick();
    Assert.Equal(4, logs.Rows.Count);

    logs.ShowError = false;
    Assert.False(game.Logs.IsLevelShown(LogLevel.Error));
    h.Scheduler.Tick();
    Assert.Equal([2L, 3, 4], logs.Rows.Select(r => r.Seq));

    logs.ShowUncaught = false;
    Assert.False(game.Logs.ShowUncaught);
    logs.ShowInfo = false;
    logs.ShowDebug = false;
    logs.ShowWarn = false;
    h.Scheduler.Tick();
    Assert.Empty(logs.Rows);

    logs.ShowInfo = true;
    logs.FilterText = "LOAD";
    Assert.Equal("LOAD", game.Logs.Search);
    h.Scheduler.Tick();
    Assert.Equal([2L], logs.Rows.Select(r => r.Seq));
  }

  [Fact]
  public void NewLinesAreFollowedOnlyWhileAutoScrollIsOnAndTheListIsAtTheBottom()
  {
    var (h, game, logs) = Start();
    var requests = 0;
    logs.ScrollToBottomRequested += (_, _) => requests++;

    AddLines(game, 1, 5);
    h.Scheduler.Tick();
    Assert.Equal(1, requests);

    // Scrolled up by 100 pixels: stop following.
    logs.ReportScroll(100);
    AddLines(game, 6, 10);
    h.Scheduler.Tick();
    Assert.Equal(1, requests);

    // Back within 2 pixels of the bottom: follow again.
    logs.ReportScroll(1.5);
    Assert.True(logs.IsAtBottom);
    AddLines(game, 11, 15);
    h.Scheduler.Tick();
    Assert.Equal(2, requests);

    // 3 pixels is too far.
    logs.ReportScroll(3);
    Assert.False(logs.IsAtBottom);

    // With auto-scroll off nothing is followed even at the bottom.
    logs.ReportScroll(0);
    logs.AutoScroll = false;
    AddLines(game, 16, 20);
    h.Scheduler.Tick();
    Assert.Equal(2, requests);
  }

  [Fact]
  public void TurningAutoScrollOnJumpsToTheBottom()
  {
    var (_, _, logs) = Start();
    logs.AutoScroll = false;
    logs.ReportScroll(500);
    var requests = 0;
    logs.ScrollToBottomRequested += (_, _) => requests++;

    logs.AutoScroll = true;

    Assert.Equal(1, requests);
    Assert.True(logs.IsAtBottom);
  }

  [Fact]
  public void ClickingARowSelectsItAndClickingAgainDeselects()
  {
    var (h, game, logs) = Start();
    game.Logs.Add(TestData.Log(1, "crash", LogLevel.Error, stack: "at Foo.bar(Foo.java:3)"));
    game.Logs.Add(TestData.Log(2, "ok"));
    h.Scheduler.Tick();

    logs.ToggleSelect(logs.Rows[0]);

    Assert.Equal(1, game.Logs.SelectedSeq);
    Assert.True(logs.Rows[0].IsSelected);
    Assert.False(logs.Rows[1].IsSelected);
    Assert.True(logs.HasSelection);
    Assert.Contains("crash", logs.SelectedDetails);
    Assert.Contains("at Foo.bar(Foo.java:3)", logs.SelectedDetails);

    logs.ToggleSelect(logs.Rows[0]);

    Assert.Null(game.Logs.SelectedSeq);
    Assert.False(logs.Rows[0].IsSelected);
    Assert.Equal(string.Empty, logs.SelectedDetails);
    Assert.False(logs.HasSelection);
  }

  [Fact]
  public void HasSelectionShowsTheDetailsBoxOnlyWhileALineIsSelected()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, 3);
    h.Scheduler.Tick();
    var raised = new List<bool>();
    logs.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(LogsPanelViewModel.HasSelection))
      {
        raised.Add(logs.HasSelection);
      }
    };

    Assert.False(logs.HasSelection);

    logs.ToggleSelect(logs.Rows[0]);
    logs.ToggleSelect(logs.Rows[1]);
    AddLines(game, 4, 5);
    h.Scheduler.Tick();
    logs.ToggleSelect(logs.Rows[1]);

    // Moving the selection or receiving new lines keeps it selected, so only the two real changes
    // are raised.
    Assert.Equal([true, false], raised);

    logs.ToggleSelect(logs.Rows[2]);
    game.Logs.Clear();
    h.Scheduler.Tick();

    Assert.False(logs.HasSelection);
    Assert.Equal([true, false, true, false], raised);
  }

  [Fact]
  public void SelectingAnotherRowMovesTheSelection()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, 2);
    h.Scheduler.Tick();

    logs.ToggleSelect(logs.Rows[0]);
    logs.ToggleSelect(logs.Rows[1]);

    Assert.Equal(2, game.Logs.SelectedSeq);
    Assert.False(logs.Rows[0].IsSelected);
    Assert.True(logs.Rows[1].IsSelected);
  }

  [Fact]
  public void AnEvictedSelectionIsCleared()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, 3);
    h.Scheduler.Tick();
    logs.ToggleSelect(logs.Rows[0]);

    AddLines(game, 4, LogStore.Capacity + 3);
    h.Scheduler.Tick();

    Assert.Null(game.Logs.SelectedSeq);
    Assert.Equal(string.Empty, logs.SelectedDetails);
    Assert.DoesNotContain(logs.Rows, r => r.IsSelected);
  }

  [Fact]
  public void ClearingTheLogClearsTheSelectionAndTheRows()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, 3);
    h.Scheduler.Tick();
    logs.ToggleSelect(logs.Rows[1]);

    game.Logs.Clear();
    h.Scheduler.Tick();

    Assert.Empty(logs.Rows);
    Assert.Equal(string.Empty, logs.SelectedDetails);
    Assert.False(logs.HasSelection);
  }

  [Fact]
  public async Task TheClearButtonAsksTheGameToClearItsLog()
  {
    var (h, game, logs) = Start();
    AddLines(game, 1, 3);
    h.Scheduler.Tick();

    await logs.ClearCommand.ExecuteAsync(null);
    h.Scheduler.Tick();

    Assert.Equal(["clearLogs"], game.Actions);
    Assert.Empty(logs.Rows);
  }

  [Fact]
  public void LocalConsoleLinesAppearInTheListAndAreMarkedLocal()
  {
    var (h, game, logs) = Start();
    game.Logs.Add(TestData.Log(1));
    game.Logs.AddLocal("> help");
    h.Scheduler.Tick();

    Assert.Equal([false, true], logs.Rows.Select(r => r.IsLocal));
  }

  [Fact]
  public void RowsKnowTheirColorClass()
  {
    var (h, game, logs) = Start();
    game.Logs.Add(TestData.Log(1, level: LogLevel.Debug));
    game.Logs.Add(TestData.Log(2, level: LogLevel.Warn));
    game.Logs.Add(TestData.Log(3, level: LogLevel.Error));
    game.Logs.Add(TestData.Log(4, level: LogLevel.Info, uncaught: true));
    h.Scheduler.Tick();

    Assert.True(logs.Rows[0].IsDebug);
    Assert.True(logs.Rows[1].IsWarn);
    Assert.True(logs.Rows[2].IsError);
    Assert.True(logs.Rows[3].IsUncaught);
    Assert.False(logs.Rows[3].IsWarn);
  }

  [Fact]
  public void SwitchingGamesShowsTheOtherLogAndKeepsTheToolbarState()
  {
    var (h, first, logs) = Start();
    var second = h.Connect("Two");
    AddLines(first, 1, 3);
    second.Logs.Add(TestData.Log(9, "other", LogLevel.Warn));
    logs.ShowWarn = false;
    h.Scheduler.Tick();
    Assert.Equal(3, logs.Rows.Count);

    h.Main.SelectedSession = h.Main.Sessions[1];

    Assert.False(second.Logs.IsLevelShown(LogLevel.Warn));
    Assert.Empty(logs.Rows);

    logs.ShowWarn = true;
    h.Scheduler.Tick();
    Assert.Equal([9L], logs.Rows.Select(r => r.Seq));
  }

  [Fact]
  public void CopyTextComesFromTheRowOrTheSelection()
  {
    var (h, game, logs) = Start();
    game.Logs.Add(TestData.Log(1, "crash", LogLevel.Error, stack: "at A.b(A.java:1)"));
    game.Logs.Add(TestData.Log(2, "fine"));
    h.Scheduler.Tick();

    Assert.Null(logs.GetLineText(null));
    Assert.Equal(logs.Rows[1].Summary, logs.GetLineText(logs.Rows[1]));
    Assert.Contains("at A.b(A.java:1)", logs.GetDetailsText(logs.Rows[0]));

    logs.ToggleSelect(logs.Rows[0]);
    Assert.Equal(logs.Rows[0].Summary, logs.GetLineText(null));
    Assert.Contains("crash", logs.GetDetailsText(null));
  }

  [Fact]
  public void WithoutAGameThePanelIsEmpty()
  {
    var h = new Harness();
    _ = h.Main;
    h.Scheduler.Tick();

    Assert.Empty(h.Logs.Rows);
  }
}
