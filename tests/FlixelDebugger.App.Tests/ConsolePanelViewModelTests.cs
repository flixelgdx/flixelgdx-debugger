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

using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Completion;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class ConsolePanelViewModelTests
{
  private static (Harness Harness, FakeGameSession Game, ConsolePanelViewModel Console) Start()
  {
    var h = new Harness();
    _ = h.Main;
    var game = h.Connect();
    return (h, game, h.Console);
  }

  private static void SetGameCommands(FakeGameSession game, params string[] names)
  {
    game.ConsoleCommands = new ConsoleCommandsPayload(
        [.. names.Select(n => new ConsoleCommand(n, n + " [x]", "does " + n))]);
    game.RaisePanelUpdated(PanelKind.Console, "console");
  }

  private static string[] LogTexts(FakeGameSession game) =>
      [.. game.Logs.GetView().Select(e => e.Line.Message)];

  [Fact]
  public void TheHintLineTellsWhichKeysToUse()
  {
    Assert.Equal(
        "Type a command. Tab completes, Up/Down history, Enter runs.",
        ConsolePanelViewModel.HintText);
  }

  [Fact]
  public void GameCommandsFromThePayloadAreSuggested()
  {
    var (_, game, console) = Start();
    SetGameCommands(game, "spawn", "quit");

    console.Text = "sp";

    Assert.Equal(SuggestionKind.Usage, console.Suggestion.Kind);
    Assert.Equal("spawn [x]  -  does spawn", console.Suggestion.Line);
  }

  [Fact]
  public void TypingUpdatesTheSuggestionAndUnknownTextSaysSo()
  {
    var (_, _, console) = Start();

    console.Text = "zzz";
    Assert.Equal(SuggestionKind.NoMatch, console.Suggestion.Kind);
    Assert.Equal("No matching command", console.Suggestion.Line);

    console.Text = string.Empty;
    Assert.Equal(SuggestionKind.Blank, console.Suggestion.Kind);
  }

  [Fact]
  public void TabCompletesAUniqueNameWithATrailingSpace()
  {
    var (_, _, console) = Start();
    console.Text = "lay";

    Assert.True(console.Tab(backward: false));

    Assert.Equal("layout.reset ", console.Text);
  }

  [Fact]
  public void TabAndShiftTabCycleThroughSeveralMatches()
  {
    var (_, _, console) = Start();
    console.Text = "c";

    console.Tab(false);
    Assert.Equal("clear", console.Text);
    Assert.Equal(SuggestionKind.Names, console.Suggestion.Kind);
    Assert.Equal(0, console.Suggestion.HighlightIndex);

    console.Tab(false);
    Assert.Equal("close", console.Text);

    console.Tab(true);
    Assert.Equal("clear", console.Text);
  }

  [Fact]
  public void TabDoesNothingWhenNothingMatches()
  {
    var (_, _, console) = Start();
    console.Text = "zzz";

    Assert.False(console.Tab(false));
    Assert.Equal("zzz", console.Text);
  }

  [Fact]
  public async Task UpAndDownWalkTheHistory()
  {
    var (_, _, console) = Start();
    console.Text = "foo";
    await console.RunAsync();
    console.Text = "bar";
    await console.RunAsync();

    Assert.True(console.HistoryUp());
    Assert.Equal("bar", console.Text);
    Assert.True(console.HistoryUp());
    Assert.Equal("foo", console.Text);
    Assert.False(console.HistoryUp());
    Assert.True(console.HistoryDown());
    Assert.Equal("bar", console.Text);
    Assert.True(console.HistoryDown());
    Assert.Equal(string.Empty, console.Text);
  }

  [Fact]
  public async Task EnterRunsTheLineInTheGameClearsTheBoxAndEchoesIt()
  {
    var (_, game, console) = Start();
    console.Text = "  spawn 3 ";

    await console.RunCommand.ExecuteAsync(null);

    Assert.Equal(["spawn 3"], game.RunLines);
    Assert.Equal(string.Empty, console.Text);
    var echo = game.Logs.GetView().Single();
    Assert.True(echo.IsLocal);
    Assert.Equal("> spawn 3", echo.Line.Message);
  }

  [Fact]
  public async Task AnEmptyLineDoesNothing()
  {
    var (_, game, console) = Start();
    console.Text = "   ";

    await console.RunAsync();

    Assert.Empty(game.RunLines);
    Assert.Equal(0, game.Logs.Count);
    Assert.Equal(string.Empty, console.Text);
  }

  [Fact]
  public async Task ALineThatCouldNotBeSentWarnsThatThereIsNoConnection()
  {
    var (_, game, console) = Start();
    game.RunResult = false;
    console.Text = "spawn";

    await console.RunAsync();

    Assert.Equal(["> spawn", "Not connected to a game."], LogTexts(game));
    Assert.Equal(LogLevel.Warn, game.Logs.GetView()[1].Line.Level);
  }

  [Fact]
  public async Task ATooLongLineIsNotSentAndStaysInTheBox()
  {
    var (_, game, console) = Start();
    var line = new string('x', CommandCompleter.MaxLineChars + 1);
    console.Text = line;

    await console.RunAsync();

    Assert.Empty(game.RunLines);
    Assert.Equal(line, console.Text);
    Assert.Contains("too long", LogTexts(game).Single());
  }

  [Fact]
  public async Task ClearEmptiesTheLogViewWithoutAnEcho()
  {
    var (_, game, console) = Start();
    game.Logs.Add(TestData.Log(1));
    console.Text = "clear";

    await console.RunAsync();

    Assert.Equal(0, game.Logs.Count);
    Assert.Empty(game.RunLines);
  }

  [Fact]
  public async Task ShowAndCloseControlPanelsByIdOrTitle()
  {
    var (h, game, console) = Start();
    Assert.True(h.Layout.IsVisible("scene"));

    console.Text = "close scene";
    await console.RunAsync();
    Assert.False(h.Layout.IsVisible("scene"));
    Assert.Contains("Hiding Scene.", LogTexts(game));

    console.Text = "show Scene";
    await console.RunAsync();
    Assert.True(h.Layout.IsVisible("scene"));
    Assert.Empty(game.RunLines);
  }

  [Fact]
  public async Task ShowWithAnUnknownPanelWarnsAndListsThePanels()
  {
    var (h, game, console) = Start();
    console.Text = "show nothing";

    await console.RunAsync();

    var warning = game.Logs.GetView()[1];
    Assert.Equal(LogLevel.Warn, warning.Line.Level);
    Assert.StartsWith("Unknown panel 'nothing'.", warning.Line.Message);
    Assert.Contains("scene", warning.Line.Message);
    Assert.True(h.Layout.IsVisible("scene"));
  }

  [Fact]
  public async Task LayoutResetRestoresTheDefaultLayout()
  {
    var (h, _, console) = Start();
    h.Layout.SetVisible("stats", false);
    console.Text = "layout.reset";

    await console.RunAsync();

    Assert.True(h.Layout.IsVisible("stats"));
  }

  [Theory]
  [InlineData(ConsoleStatus.Failed, true)]
  [InlineData(ConsoleStatus.UnknownCommand, true)]
  [InlineData(ConsoleStatus.Ran, false)]
  public void FailedAndUnknownResultsBecomeLocalWarnings(ConsoleStatus status, bool warns)
  {
    var (_, game, _) = Start();

    game.ConsoleResult = new ConsoleResultPayload(status, "result text");
    game.RaisePanelUpdated(PanelKind.Console, "console");
    game.RaisePanelUpdated(PanelKind.Console, "console");

    if (warns)
    {
      var line = game.Logs.GetView().Single();
      Assert.True(line.IsLocal);
      Assert.Equal(LogLevel.Warn, line.Line.Level);
      Assert.Equal("result text", line.Line.Message);
    }
    else
    {
      Assert.Equal(0, game.Logs.Count);
    }
  }

  [Fact]
  public async Task WithoutAGameLocalCommandsStillWorkAndGameCommandsDoNotCrash()
  {
    var h = new Harness();
    _ = h.Main;
    var console = h.Console;

    console.Text = "close scene";
    await console.RunAsync();
    Assert.False(h.Layout.IsVisible("scene"));

    console.Text = "spawn";
    await console.RunAsync();
    Assert.Equal(string.Empty, console.Text);
  }
}
