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

using FlixelDebugger.Core.Completion;
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Core.Tests;

public class CommandCompleterTests
{
  private static readonly ConsoleCommand[] _gameCommands =
  [
      new("select", "select [path]", "Selects an object."),
      new("freecam", "freecam [on|off]", "Turns the free camera on or off."),
      new("goto", "goto <x> <y>", "Centers the camera."),
      new("Gone", "gone", "Goes away."),
      new("go", "go", "Just goes."),
  ];

  private static CommandCompleter Create(params ConsoleCommand[] commands)
  {
    var completer = new CommandCompleter();
    completer.SetCommands(commands.Length == 0 ? _gameCommands : commands);
    return completer;
  }

  // Ten commands that share a prefix, to exercise the six-name window.
  private static CommandCompleter CreateMany()
  {
    var completer = new CommandCompleter();
    completer.SetCommands(
        Enumerable.Range(1, 10).Select(i => new ConsoleCommand($"cmd{i:00}", $"cmd{i:00}", "d")));
    return completer;
  }

  private static string[] Names(CommandCompleter completer) =>
      [.. completer.Commands.Select(c => c.Name)];

  [Fact]
  public void CommandListIsSortedByNameIgnoringCase()
  {
    Assert.Equal(
        ["freecam", "go", "Gone", "goto", "select"],
        Names(Create()));
  }

  [Fact]
  public void MatchingIsACaseInsensitivePrefixOnTheFirstWord()
  {
    var completer = Create();
    Assert.Equal(["go", "Gone", "goto"], completer.FindMatches("GO").Select(c => c.Name));
    Assert.Equal(["goto"], completer.FindMatches("goT 1 2").Select(c => c.Name));
    Assert.Empty(completer.FindMatches("xyz"));
    Assert.Equal(completer.Commands.Count, completer.FindMatches(string.Empty).Count);
  }

  [Fact]
  public void SuggestionIsBlankWhenNoWordIsTyped()
  {
    var completer = Create();
    Assert.Equal(SuggestionKind.Blank, completer.GetSuggestion(string.Empty).Kind);
    Assert.Equal(string.Empty, completer.GetSuggestion(string.Empty).Line);
    Assert.Equal(string.Empty, completer.GetSuggestion("   ").Line);
  }

  [Fact]
  public void SuggestionForExactlyOneMatchIsUsageAndDescription()
  {
    var suggestion = Create().GetSuggestion("sel");
    Assert.Equal(SuggestionKind.Usage, suggestion.Kind);
    Assert.Equal("select [path]  -  Selects an object.", suggestion.Line);
  }

  [Fact]
  public void SuggestionOnceASpaceFollowsTheWordIsTheFirstMatchUsage()
  {
    var completer = Create();
    Assert.Equal("goto <x> <y>  -  Centers the camera.", completer.GetSuggestion("goto 1").Line);
    Assert.Equal("goto <x> <y>  -  Centers the camera.", completer.GetSuggestion("goto ").Line);

    // Several commands start with "go", and the first one wins once arguments have started.
    Assert.Equal("go  -  Just goes.", completer.GetSuggestion("go 1 2").Line);
  }

  [Fact]
  public void SuggestionForSeveralMatchesJoinsTheNamesWithThreeSpaces()
  {
    var suggestion = Create().GetSuggestion("go");
    Assert.Equal(SuggestionKind.Names, suggestion.Kind);
    Assert.Equal("go   Gone   goto", suggestion.Line);
    Assert.Equal(["go", "Gone", "goto"], suggestion.Names);
    Assert.Equal(-1, suggestion.HighlightIndex);
  }

  [Fact]
  public void SuggestionForNoMatchSaysSo()
  {
    var completer = Create();
    Assert.Equal(SuggestionKind.NoMatch, completer.GetSuggestion("zzz").Kind);
    Assert.Equal("No matching command", completer.GetSuggestion("zzz").Line);
    Assert.Equal("No matching command", completer.GetSuggestion("zzz 1 2").Line);
  }

  [Fact]
  public void SuggestionOmitsTheSeparatorWhenThereIsNoDescription()
  {
    var completer = Create(new ConsoleCommand("ping", "ping", string.Empty));
    Assert.Equal("ping", completer.GetSuggestion("pin").Line);
  }

  [Fact]
  public void WindowShowsAtMostSixNamesAndCountsTheRest()
  {
    var suggestion = CreateMany().GetSuggestion("cmd");
    Assert.Equal("cmd01   cmd02   cmd03   cmd04   cmd05   cmd06   (+4 more)", suggestion.Line);
    Assert.Equal(6, suggestion.Names.Count);
    Assert.Equal(0, suggestion.HiddenBefore);
    Assert.Equal(4, suggestion.HiddenAfter);
  }

  [Fact]
  public void WindowSlidesForwardToKeepThePickVisible()
  {
    var completer = CreateMany();
    var text = "cmd";
    for (var i = 0; i < 7; i++)
    {
      text = completer.Tab(text)!;
    }

    // Seven presses leave the pick on cmd07, the seventh name.
    Assert.Equal("cmd07", text);
    var suggestion = completer.GetSuggestion(text);
    Assert.Equal(
        "(+1)   cmd02   cmd03   cmd04   cmd05   cmd06   cmd07   (+3 more)", suggestion.Line);
    Assert.Equal(1, suggestion.HiddenBefore);
    Assert.Equal(3, suggestion.HiddenAfter);
    Assert.Equal(5, suggestion.HighlightIndex);
  }

  [Fact]
  public void WindowDoesNotMoveWhileThePickStaysInside()
  {
    var completer = CreateMany();
    var text = "cmd";
    for (var i = 0; i < 7; i++)
    {
      text = completer.Tab(text)!;
    }

    // Going back one step keeps cmd06 inside the current window, so the window stays where it is.
    text = completer.Tab(text, backward: true)!;
    Assert.Equal("cmd06", text);
    var suggestion = completer.GetSuggestion(text);
    Assert.Equal(
        "(+1)   cmd02   cmd03   cmd04   cmd05   cmd06   cmd07   (+3 more)", suggestion.Line);
    Assert.Equal(4, suggestion.HighlightIndex);
  }

  [Fact]
  public void WindowEndsWithoutAnAfterMarkerAtTheLastName()
  {
    var completer = CreateMany();
    var text = completer.Tab("cmd", backward: true)!;
    Assert.Equal("cmd10", text);
    var suggestion = completer.GetSuggestion(text);
    Assert.Equal("(+4)   cmd05   cmd06   cmd07   cmd08   cmd09   cmd10", suggestion.Line);
    Assert.Equal(5, suggestion.HighlightIndex);
    Assert.Equal(0, suggestion.HiddenAfter);
  }

  [Fact]
  public void HighlightPointsAtThePickInsideTheLine()
  {
    var completer = Create();
    var text = completer.Tab("go")!;
    var suggestion = completer.GetSuggestion(text);
    Assert.Equal("Gone", text);
    Assert.Equal(1, suggestion.HighlightIndex);
    Assert.Equal(
        "Gone", suggestion.Line.Substring(suggestion.HighlightStart, suggestion.HighlightLength));
    Assert.Equal(1, completer.GetHighlightedIndex(text));
  }

  [Fact]
  public void TabCompletesASingleMatchWithATrailingSpace()
  {
    var completer = Create();
    Assert.Equal("select ", completer.Tab("sel"));
    Assert.Equal("freecam ", completer.Tab("FREE"));
    Assert.False(completer.IsCycling("select "));
    Assert.Equal(-1, completer.GetHighlightedIndex("select "));
  }

  [Fact]
  public void TabDoesNothingWithoutAMatch()
  {
    Assert.Null(Create().Tab("zzz"));
  }

  [Fact]
  public void TabDoesNothingOnceArgumentsHaveStarted()
  {
    var completer = Create();
    Assert.Null(completer.Tab("goto "));
    Assert.Null(completer.Tab("goto 1"));
    Assert.Null(completer.Tab("select  "));
  }

  [Fact]
  public void TabDoesNothingAfterTheSingleMatchWasInserted()
  {
    var completer = Create();
    var text = completer.Tab("sel")!;
    Assert.Null(completer.Tab(text));
  }

  [Fact]
  public void ForwardTabStartsAtTheFirstMatch()
  {
    // "go", "Gone", and "goto" all start with "g", and the typed "g" is not equal to the first
    // match.
    Assert.Equal("go", Create().Tab("g"));
  }

  [Fact]
  public void ForwardTabSkipsTheFirstMatchWhenTheTypedWordEqualsIt()
  {
    var completer = Create();

    // "go" is already exactly the first match, so the first press moves on to the second one.
    Assert.Equal("Gone", completer.Tab("go"));
  }

  [Fact]
  public void ExactEqualityIgnoresCase()
  {
    Assert.Equal("Gone", Create().Tab("GO"));
  }

  [Fact]
  public void TabDoesNotSkipWhenTheTypedWordOnlyStartsTheFirstMatch()
  {
    // "g" is not equal to "go", so the cycle starts at "go".
    Assert.Equal("go", Create().Tab("g"));
  }

  [Fact]
  public void TabCyclesAndWrapsAround()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    Assert.Equal("go", text);
    text = completer.Tab(text)!;
    Assert.Equal("Gone", text);
    text = completer.Tab(text)!;
    Assert.Equal("goto", text);
    text = completer.Tab(text)!;
    Assert.Equal("go", text);
  }

  [Fact]
  public void CycleKeepsTheOriginalMatchesEvenThoughTheFieldHoldsAFullName()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    text = completer.Tab(text)!;

    // The field now says "Gone", which alone would match only one command, but the cycle goes on
    // through all three.
    Assert.Equal("goto", completer.Tab(text));
    Assert.Equal("go   Gone   goto", completer.GetSuggestion("goto").Line);
  }

  [Fact]
  public void ShiftTabStartsAtTheLastMatchAndWraps()
  {
    var completer = Create();
    var text = completer.Tab("g", backward: true)!;
    Assert.Equal("goto", text);
    text = completer.Tab(text, backward: true)!;
    Assert.Equal("Gone", text);
    text = completer.Tab(text, backward: true)!;
    Assert.Equal("go", text);
    text = completer.Tab(text, backward: true)!;
    Assert.Equal("goto", text);
  }

  [Fact]
  public void ShiftTabStartsAtTheLastMatchEvenWhenTheWordEqualsTheFirst()
  {
    Assert.Equal("goto", Create().Tab("go", backward: true));
  }

  [Fact]
  public void ShiftTabWithOneMatchStillInsertsTheNameAndASpace()
  {
    Assert.Equal("select ", Create().Tab("sel", backward: true));
  }

  [Fact]
  public void TabAndShiftTabMixInOneCycle()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    text = completer.Tab(text)!;
    Assert.Equal("Gone", text);
    Assert.Equal("go", completer.Tab(text, backward: true));
  }

  [Fact]
  public void AnEmptyLineMatchesEveryCommand()
  {
    var completer = Create();
    var text = completer.Tab(string.Empty)!;
    Assert.Equal("freecam", text);
    var seen = new List<string> { text };
    for (var i = 1; i < completer.Commands.Count; i++)
    {
      text = completer.Tab(text)!;
      seen.Add(text);
    }

    Assert.Equal(Names(completer), seen);
    Assert.Equal("freecam", completer.Tab(text));
  }

  [Fact]
  public void AnEmptyLineShiftTabStartsAtTheLastCommand()
  {
    var completer = Create();
    Assert.Equal("select", completer.Tab(string.Empty, backward: true));
  }

  [Fact]
  public void CycleIsActiveOnlyWhileTheFieldHoldsWhatTabPutThere()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    Assert.True(completer.IsCycling(text));
    Assert.False(completer.IsCycling(text + "x"));
    Assert.False(completer.IsCycling("g"));
  }

  [Fact]
  public void AnEditEndsTheCycleAndTheNextTabStartsAgainFromTheTypedText()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    text = completer.Tab(text)!;
    Assert.Equal("Gone", text);

    // The person edits the field to "got". The old cycle is over, so Tab starts again from the
    // typed text.
    Assert.Equal("goto ", completer.Tab("got"));
    Assert.False(completer.IsCycling("got"));
    Assert.Equal(-1, completer.GetHighlightedIndex("got"));
  }

  [Fact]
  public void AfterAnEditTheSuggestionIsBuiltFromTheEditedText()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    Assert.Equal("go   Gone   goto", completer.GetSuggestion(text).Line);
    Assert.Equal("goto <x> <y>  -  Centers the camera.", completer.GetSuggestion("goto").Line);
    Assert.Equal("No matching command", completer.GetSuggestion("gotos").Line);
  }

  [Fact]
  public void ChangingTheCommandListEndsTheCycle()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    completer.SetCommands(_gameCommands);
    Assert.False(completer.IsCycling(text));
  }

  [Fact]
  public void HistoryKeepsSixtyFourEntries()
  {
    var completer = Create();
    for (var i = 0; i < 70; i++)
    {
      completer.Submit($"line{i}");
    }

    Assert.Equal(64, completer.History.Count);
    Assert.Equal("line6", completer.History[0]);
    Assert.Equal("line69", completer.History[^1]);
  }

  [Fact]
  public void HistorySkipsADuplicateOfTheLastEntry()
  {
    var completer = Create();
    completer.Submit("a");
    completer.Submit("a");
    completer.Submit("b");
    completer.Submit("a");

    Assert.Equal(["a", "b", "a"], completer.History);
  }

  [Fact]
  public void UpAndDownWalkIndicesZeroToNWhereNIsTheEmptyLine()
  {
    var completer = Create();
    foreach (var line in new[] { "a", "b", "c" })
    {
      completer.Submit(line);
    }

    Assert.Null(completer.HistoryDown());
    Assert.Equal("c", completer.HistoryUp());
    Assert.Equal("b", completer.HistoryUp());
    Assert.Equal("a", completer.HistoryUp());
    Assert.Null(completer.HistoryUp());
    Assert.Equal("b", completer.HistoryDown());
    Assert.Equal("c", completer.HistoryDown());
    Assert.Equal(string.Empty, completer.HistoryDown());
    Assert.Null(completer.HistoryDown());
    Assert.Equal("c", completer.HistoryUp());
  }

  [Fact]
  public void HistoryIsEmptyAtFirst()
  {
    var completer = Create();
    Assert.Null(completer.HistoryUp());
    Assert.Null(completer.HistoryDown());
  }

  [Fact]
  public void SubmittingResetsThePositionToTheEmptyLine()
  {
    var completer = Create();
    completer.Submit("a");
    completer.Submit("b");
    completer.HistoryUp();
    completer.HistoryUp();
    completer.Submit("c");

    Assert.Equal("c", completer.HistoryUp());
  }

  [Fact]
  public void SubmitTrimsAndStripsTabsAndLineBreaks()
  {
    var completer = Create();
    Assert.Equal("go now", completer.Submit("  go\t now\r\n "));
    Assert.Equal(["go now"], completer.History);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t\n")]
  public void SubmitDoesNotRememberAnEmptyLine(string line)
  {
    var completer = Create();
    Assert.Equal(string.Empty, completer.Submit(line));
    Assert.Empty(completer.History);
  }

  [Fact]
  public void SubmitEndsTheTabCycle()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    Assert.True(completer.IsCycling(text));
    completer.Submit(text);
    Assert.False(completer.IsCycling(text));
  }

  [Fact]
  public void TheHistoryLimitCanShrinkAndTurnTheHistoryOff()
  {
    var completer = Create();
    foreach (var line in new[] { "a", "b", "c", "d" })
    {
      completer.Submit(line);
    }

    completer.HistoryLimit = 2;
    Assert.Equal(["c", "d"], completer.History);
    completer.HistoryLimit = 0;
    Assert.Empty(completer.History);
    completer.Submit("e");
    Assert.Empty(completer.History);
    Assert.Null(completer.HistoryUp());
  }

  [Fact]
  public void ANameThatAppearsTwiceIsKeptOnce()
  {
    var completer = Create(
        new ConsoleCommand("spawn", "spawn", "First."),
        new ConsoleCommand("SPAWN", "SPAWN", "Second."));
    Assert.Single(completer.Commands);
  }
}
