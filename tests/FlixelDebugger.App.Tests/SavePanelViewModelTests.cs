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
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class SavePanelViewModelTests
{
  private static SavePayload Sample(string status = "3 values", bool isError = false) => new(
      [
          new SaveRow("player.coins", "12", ValueKind.Double, false),
          new SaveRow("player.name", "\"Bob\"", ValueKind.String, true),
          new SaveRow("settings.volume", "0.5", ValueKind.Double, false),
      ],
      ["old.key"],
      status,
      isError);

  private static SavePanelViewModel Make(PanelFixture f)
  {
    f.Game.Save = Sample();
    return f.Save();
  }

  [Fact]
  public void ShowsUnsavedRowsWithAStarAndRemovedPathsAtTheEnd()
  {
    var vm = Make(new PanelFixture());

    Assert.Equal(
        ["player.coins", "* player.name", "settings.volume", "old.key"],
        vm.Rows.Select(r => r.Label));
    Assert.Equal(["12", "\"Bob\"", "0.5", "(removed)"], vm.Rows.Select(r => r.Display));
    Assert.Equal([false, false, false, true], vm.Rows.Select(r => r.IsRemoved));
    Assert.Equal("player.name", vm.Rows[1].Path);
  }

  [Fact]
  public void TheStatusLineIsRedOnlyForAnError()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.Equal("3 values", vm.Status);
    Assert.False(vm.IsError);

    f.Game.Save = Sample("Could not parse", isError: true);
    f.Tick();

    Assert.Equal("Could not parse", vm.Status);
    Assert.True(vm.IsError);
  }

  [Fact]
  public void TheFilterIsACaseInsensitiveSubstringOfThePath()
  {
    var vm = Make(new PanelFixture());

    vm.Filter = "PLAYER";
    Assert.Equal(["player.coins", "* player.name"], vm.Rows.Select(r => r.Label));

    vm.Filter = "old";
    Assert.Equal(["old.key"], vm.Rows.Select(r => r.Label));

    vm.Filter = "nothing";
    Assert.Empty(vm.Rows);

    vm.Filter = string.Empty;
    Assert.Equal(4, vm.Rows.Count);
  }

  [Fact]
  public void ClickingARowFillsThePathAndValueBoxes()
  {
    var vm = Make(new PanelFixture());

    vm.SelectedRow = vm.Rows[1];

    Assert.Equal("player.name", vm.PathText);
    Assert.Equal("\"Bob\"", vm.ValueText);

    vm.SelectedRow = vm.Rows[3];

    Assert.Equal("old.key", vm.PathText);
    Assert.Equal(string.Empty, vm.ValueText);
  }

  [Fact]
  public void SetSendsThePathAndTheTypedText()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.False(vm.SetCommand.CanExecute(null));

    vm.PathText = "player.coins";
    vm.ValueText = "99";
    Assert.True(vm.SetCommand.CanExecute(null));
    vm.SetCommand.Execute(null);

    Assert.Equal(["saveSet:player.coins=99"], f.Game.Actions);
  }

  [Fact]
  public void TextLongerThanTheGameAcceptsCannotBeSent()
  {
    var vm = Make(new PanelFixture());

    vm.PathText = "a";
    vm.ValueText = new string('x', SavePanelViewModel.MaxTextLength + 1);

    Assert.False(vm.SetCommand.CanExecute(null));
  }

  [Fact]
  public void ResetKeyAndSaveNowSendTheirActions()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.False(vm.ResetKeyCommand.CanExecute(null));

    vm.PathText = "player.coins";
    vm.ResetKeyCommand.Execute(null);
    vm.SaveNowCommand.Execute(null);

    Assert.Equal(["saveResetKey:player.coins", "saveNow"], f.Game.Actions);
  }

  [Fact]
  public void ResetAllNeedsASecondClickWithinThreeSeconds()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.Equal("Reset all", vm.ResetAllText);

    vm.ResetAllCommand.Execute(null);

    Assert.Empty(f.Game.Actions);
    Assert.True(vm.IsResetAllArmed);
    Assert.Equal("Click again to reset", vm.ResetAllText);

    f.Clock.Advance(TimeSpan.FromSeconds(2));
    f.Tick();
    Assert.Equal("Click again to reset", vm.ResetAllText);
    vm.ResetAllCommand.Execute(null);

    Assert.Equal(["saveResetAll"], f.Game.Actions);
    Assert.False(vm.IsResetAllArmed);
    Assert.Equal("Reset all", vm.ResetAllText);
  }

  [Fact]
  public void TheArmedButtonExpiresByItselfAndThenNeedsTwoClicksAgain()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    vm.ResetAllCommand.Execute(null);

    f.Clock.Advance(TimeSpan.FromSeconds(3.5));
    f.Tick();

    Assert.False(vm.IsResetAllArmed);
    Assert.Equal("Reset all", vm.ResetAllText);

    vm.ResetAllCommand.Execute(null);

    Assert.Empty(f.Game.Actions);
    Assert.True(vm.IsResetAllArmed);
  }

  [Fact]
  public void SwitchingTheGameDisarmsResetAll()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    vm.ResetAllCommand.Execute(null);

    f.Active.Current = new FakeGameSession("Other");

    Assert.False(vm.IsResetAllArmed);
    Assert.Empty(vm.Rows);
  }

  private static SavePayload WithSaves(ushort selected, params string[] saves) => Sample() with
  {
    Saves = saves,
    Selected = selected,
  };

  [Fact]
  public void AnOlderGameWithoutASaveListHidesThePicker()
  {
    var vm = Make(new PanelFixture());

    Assert.Empty(vm.Saves);
    Assert.False(vm.HasManySaves);
    Assert.Equal(0, vm.SelectedSaveIndex);
  }

  [Fact]
  public void OneSaveHidesThePickerAndMoreShowIt()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(0, "Game/settings");
    var vm = f.Save();
    Assert.Single(vm.Saves);
    Assert.False(vm.HasManySaves);

    f.Game.Save = WithSaves(1, "Game/settings", "Game/profile1", "(not bound)");
    f.Tick();

    Assert.True(vm.HasManySaves);
    Assert.Equal(["Game/settings", "Game/profile1", "(not bound)"], vm.Saves.Select(c => c.Label));
    Assert.Equal(1, vm.SelectedSaveIndex);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void PickingASaveSendsSelectOnce()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1");
    var vm = f.Save();
    vm.SelectedRow = vm.Rows[0];

    vm.SelectedSaveIndex = 1;
    vm.SelectedSaveIndex = 1;

    Assert.Equal(["saveSelect:1"], f.Game.Actions);
    Assert.Equal(string.Empty, vm.PathText);
    Assert.Null(vm.SelectedRow);
  }

  [Fact]
  public void TheGamesSelectionIsCopiedWithoutBeingSentBack()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1", "Game/profile2");
    var vm = f.Save();

    f.Game.Save = WithSaves(2, "Game/settings", "Game/profile1", "Game/profile2");
    f.Tick();

    Assert.Equal(2, vm.SelectedSaveIndex);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void APickStaysOnScreenUntilTheGameConfirmsIt()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1");
    var vm = f.Save();

    vm.SelectedSaveIndex = 1;

    // A payload sent before the game saw the pick still says 0.
    f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1");
    f.Tick();
    Assert.Equal(1, vm.SelectedSaveIndex);

    f.Game.Save = WithSaves(1, "Game/settings", "Game/profile1");
    f.Tick();
    Assert.Equal(1, vm.SelectedSaveIndex);

    // Once confirmed, the game decides again, for example when it drops the save.
    f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1");
    f.Tick();
    Assert.Equal(0, vm.SelectedSaveIndex);
    Assert.Equal(["saveSelect:1"], f.Game.Actions);
  }

  [Fact]
  public void AnUnconfirmedPickGivesUpAfterAWhile()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1");
    var vm = f.Save();
    vm.SelectedSaveIndex = 1;

    for (var i = 0; i < 30; i++)
    {
      f.Game.Save = WithSaves(0, "Game/settings", "Game/profile1");
      f.Tick();
    }

    Assert.Equal(0, vm.SelectedSaveIndex);
  }

  [Fact]
  public void EntriesAreRenamedInPlaceAndANullFromTheComboBoxIsIgnored()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(1, "Game/settings", "Game/profile1");
    var vm = f.Save();
    var first = vm.Saves[0];
    var changes = 0;
    vm.Saves.CollectionChanged += (_, _) => changes++;

    f.Game.Save = WithSaves(1, "(no save)", "Game/profile1");
    f.Tick();

    Assert.Same(first, vm.Saves[0]);
    Assert.Equal("(no save)", first.Label);
    Assert.Equal(0, changes);

    // A ComboBox reports -1 while it rebuilds its items. That must not reach the game.
    var raised = 0;
    vm.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(SavePanelViewModel.SelectedSaveIndex))
      {
        raised++;
      }
    };
    vm.SelectedSaveIndex = -1;

    Assert.Equal(1, vm.SelectedSaveIndex);
    Assert.Equal(1, raised);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void ASmallerListMovesTheSelectionBackToTheMainSave()
  {
    var f = new PanelFixture();
    f.Game.Save = WithSaves(2, "Game/settings", "Game/profile1", "Game/profile2");
    var vm = f.Save();

    f.Game.Save = WithSaves(0, "Game/settings");
    f.Tick();

    Assert.Single(vm.Saves);
    Assert.False(vm.HasManySaves);
    Assert.Equal(0, vm.SelectedSaveIndex);
    Assert.Empty(f.Game.Actions);
  }
}
