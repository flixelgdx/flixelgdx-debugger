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
using FlixelDebugger.Protocol.Panels;

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
    completer.SetGameCommands(commands.Length == 0 ? _gameCommands : commands);
    return completer;
  }

  // Ten commands that share a prefix, to exercise the six-name window.
  private static CommandCompleter CreateMany()
  {
    var completer = new CommandCompleter();
    completer.SetGameCommands(
        Enumerable.Range(1, 10).Select(i => new ConsoleCommand($"cmd{i:00}", $"cmd{i:00}", "d")));
    return completer;
  }

  private static string[] Names(CommandCompleter completer) =>
      [.. completer.Commands.Select(c => c.Name)];

  [Fact]
  public void CommandListIsSortedByNameIgnoringCase()
  {
    Assert.Equal(
        ["clear", "close", "freecam", "go", "Gone", "goto", "layout.reset", "select", "show"],
        Names(Create()));
  }

  [Fact]
  public void LocalCommandsAreAlwaysPresent()
  {
    Assert.Equal(["clear", "close", "layout.reset", "show"], Names(new CommandCompleter()));
    Assert.Equal(
        ["clear", "close", "layout.reset", "show"],
        CommandCompleter.LocalCommands.Select(c => c.Name).Order());
  }

  [Fact]
  public void LocalCommandUsagesMatchTheSpec()
  {
    var usages = CommandCompleter.LocalCommands.ToDictionary(c => c.Name, c => c.Usage);
    Assert.Equal("clear", usages["clear"]);
    Assert.Equal("show <panel>", usages["show"]);
    Assert.Equal("close <panel>", usages["close"]);
    Assert.Equal("layout.reset", usages["layout.reset"]);
  }

  [Fact]
  public void LocalCommandWinsOverAGameCommandWithTheSameName()
  {
    var completer = Create(new ConsoleCommand("CLEAR", "CLEAR the game", "Game version."));
    var clear = Assert.Single(
        completer.Commands,
        c => string.Equals(c.Name, "clear", StringComparison.OrdinalIgnoreCase));
    Assert.Equal("clear", clear.Name);
    Assert.Equal("clear  -  Clears the console output.", completer.GetSuggestion("clear").Line);
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
    // "clear" and "close" both start with "c", and the typed "c" is not equal to the first match.
    Assert.Equal("clear", Create().Tab("c"));
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
    Assert.Equal("clear", text);
    var seen = new List<string> { text };
    for (var i = 1; i < completer.Commands.Count; i++)
    {
      text = completer.Tab(text)!;
      seen.Add(text);
    }

    Assert.Equal(Names(completer), seen);
    Assert.Equal("clear", completer.Tab(text));
  }

  [Fact]
  public void AnEmptyLineShiftTabStartsAtTheLastCommand()
  {
    var completer = Create();
    Assert.Equal("show", completer.Tab(string.Empty, backward: true));
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
    completer.SetGameCommands(_gameCommands);
    Assert.False(completer.IsCycling(text));
  }

  [Fact]
  public async Task HistoryKeepsSixtyFourEntries()
  {
    var completer = Create();
    for (var i = 0; i < 70; i++)
    {
      await completer.SubmitAsync($"line{i}", null, TestContext.Current.CancellationToken);
    }

    Assert.Equal(64, completer.History.Count);
    Assert.Equal("line6", completer.History[0]);
    Assert.Equal("line69", completer.History[^1]);
  }

  [Fact]
  public async Task HistorySkipsADuplicateOfTheLastEntry()
  {
    var completer = Create();
    await completer.SubmitAsync("a", null, TestContext.Current.CancellationToken);
    await completer.SubmitAsync("a", null, TestContext.Current.CancellationToken);
    await completer.SubmitAsync("b", null, TestContext.Current.CancellationToken);
    await completer.SubmitAsync("a", null, TestContext.Current.CancellationToken);

    Assert.Equal(["a", "b", "a"], completer.History);
  }

  [Fact]
  public async Task UpAndDownWalkIndicesZeroToNWhereNIsTheEmptyLine()
  {
    var completer = Create();
    foreach (var line in new[] { "a", "b", "c" })
    {
      await completer.SubmitAsync(line, null, TestContext.Current.CancellationToken);
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
  public async Task SubmittingResetsThePositionToTheEmptyLine()
  {
    var completer = Create();
    await completer.SubmitAsync("a", null, TestContext.Current.CancellationToken);
    await completer.SubmitAsync("b", null, TestContext.Current.CancellationToken);
    completer.HistoryUp();
    completer.HistoryUp();
    await completer.SubmitAsync("c", null, TestContext.Current.CancellationToken);

    Assert.Equal("c", completer.HistoryUp());
  }

  [Fact]
  public async Task SubmitTrimsAndStripsTabsAndLineBreaks()
  {
    var runner = new FakeRunner();
    var completer = Create();

    var result = await completer.SubmitAsync(
        "  goto 1 2\r\n", runner, TestContext.Current.CancellationToken);
    Assert.Equal("goto 1 2", result.Line);

    result = await completer.SubmitAsync(
        "go\tto\n 3", runner, TestContext.Current.CancellationToken);
    Assert.Equal("goto 3", result.Line);
    Assert.Equal(["goto 1 2", "goto 3"], runner.Lines);
  }

  [Fact]
  public async Task SubmitEchoesTheLineWithAPrompt()
  {
    var result = await Create().SubmitAsync(
        "  goto 1 2 ", new FakeRunner(), TestContext.Current.CancellationToken);
    Assert.Equal("> goto 1 2", result.Echo);
    Assert.Equal(SubmitKind.Run, result.Kind);
    Assert.True(result.Sent);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t\r\n")]
  public async Task SubmitIgnoresAnEmptyLine(string line)
  {
    var runner = new FakeRunner();
    var completer = Create();

    var result = await completer.SubmitAsync(line, runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Ignored, result.Kind);
    Assert.Null(result.Echo);
    Assert.Empty(runner.Lines);
    Assert.Empty(completer.History);
  }

  [Fact]
  public async Task SubmitAcceptsExactlyFiveHundredCharacters()
  {
    var runner = new FakeRunner();
    var line = new string('x', 500);

    var result = await Create().SubmitAsync(line, runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Run, result.Kind);
    Assert.Equal([line], runner.Lines);
  }

  [Fact]
  public async Task SubmitRejectsMoreThanFiveHundredCharacters()
  {
    var runner = new FakeRunner();
    var completer = Create();

    var result = await completer.SubmitAsync(
        new string('x', 501), runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.TooLong, result.Kind);
    Assert.False(result.Sent);
    Assert.Null(result.Echo);
    Assert.Contains("501", result.Message, StringComparison.Ordinal);
    Assert.Empty(runner.Lines);
    Assert.Empty(completer.History);
  }

  [Fact]
  public async Task TheLengthLimitAppliesAfterTrimming()
  {
    var runner = new FakeRunner();
    var line = "  " + new string('x', 500) + "\t\n  ";

    var result = await Create().SubmitAsync(line, runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Run, result.Kind);
  }

  [Fact]
  public async Task LocalCommandsGoToTheHandlerAndNotToTheGame()
  {
    var runner = new FakeRunner();
    var completer = Create();
    var calls = new List<(string Name, string Arguments)>();
    completer.LocalHandler = (name, arguments) =>
    {
      calls.Add((name, arguments));
      return "ok";
    };

    var result = await completer.SubmitAsync(
        "show   stats panel ", runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Local, result.Kind);
    Assert.Equal("> show   stats panel", result.Echo);
    Assert.Equal("ok", result.Message);
    Assert.Equal([("show", "stats panel")], calls);
    Assert.Empty(runner.Lines);
    Assert.Equal(["show   stats panel"], completer.History);
  }

  [Theory]
  [InlineData("clear", "clear")]
  [InlineData("CLEAR", "clear")]
  [InlineData("close stats", "close")]
  [InlineData("layout.reset", "layout.reset")]
  public async Task EveryLocalCommandIsRoutedLocally(string line, string expectedName)
  {
    var completer = Create();
    string? seen = null;
    completer.LocalHandler = (name, _) =>
    {
      seen = name;
      return null;
    };

    var result = await completer.SubmitAsync(
        line, new FakeRunner(), TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Local, result.Kind);
    Assert.Equal(expectedName, seen);
  }

  [Fact]
  public async Task OnlyAnExactLocalNameIsLocal()
  {
    var runner = new FakeRunner();

    var result = await Create().SubmitAsync(
        "clearall now", runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Run, result.Kind);
    Assert.Equal(["clearall now"], runner.Lines);
  }

  [Fact]
  public async Task ALocalCommandWithoutAHandlerDoesNothing()
  {
    var result = await Create().SubmitAsync(
        "clear", new FakeRunner(), TestContext.Current.CancellationToken);
    Assert.Equal(SubmitKind.Local, result.Kind);
    Assert.Null(result.Message);
  }

  [Fact]
  public async Task ALocalCommandWinsOverAGameCommandOfTheSameName()
  {
    var runner = new FakeRunner();
    var completer = Create(new ConsoleCommand("clear", "clear", "Game clear."));
    completer.LocalHandler = (_, _) => null;

    var result = await completer.SubmitAsync(
        "clear", runner, TestContext.Current.CancellationToken);

    Assert.Equal(SubmitKind.Local, result.Kind);
    Assert.Empty(runner.Lines);
  }

  [Fact]
  public async Task SubmitWithoutAGameSaysItWasNotSent()
  {
    var result = await Create().SubmitAsync(
        "goto 1 2", null, TestContext.Current.CancellationToken);
    Assert.Equal(SubmitKind.Run, result.Kind);
    Assert.False(result.Sent);
    Assert.Equal("> goto 1 2", result.Echo);
    Assert.NotNull(result.Message);
  }

  [Fact]
  public async Task SubmitEndsTheTabCycle()
  {
    var completer = Create();
    var text = completer.Tab("g")!;
    await completer.SubmitAsync(text, null, TestContext.Current.CancellationToken);
    Assert.False(completer.IsCycling(text));
  }

  private sealed class FakeRunner : ICommandRunner
  {
    public List<string> Lines { get; } = [];

    public Task<bool> RunCommandAsync(string line, CancellationToken cancellationToken = default)
    {
      Lines.Add(line);
      return Task.FromResult(true);
    }
  }
}
