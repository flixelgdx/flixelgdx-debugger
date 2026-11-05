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
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class CustomPanelViewModelTests
{
  private static CustomPayload Payload(int hp = 5) => new(
      [
          TestData.Row("hp", PropertyValue.FromInt(hp), true),
          TestData.Row("alive", PropertyValue.FromBool(true), true),
          TestData.Row("note", PropertyValue.FromString("fixed")),
          TestData.Row("bad", PropertyValue.FromError("boom")),
      ],
      [new CustomButton(0, "Heal"), new CustomButton(7, "Kill")]);

  [Fact]
  public void ShowsTheRowsAndTheButtonsOfItsOwnPanel()
  {
    var f = new PanelFixture();
    f.Game.Customs["custom.player"] = Payload();

    var vm = f.Custom();

    Assert.Equal("custom.player", vm.Id);
    Assert.Equal("Player", vm.Title);
    Assert.Equal(["hp", "alive", "note", "bad"], vm.Grid.Rows.Select(r => r.Name));
    Assert.True(vm.Grid.Rows[0].IsTextEditor);
    Assert.True(vm.Grid.Rows[1].IsBoolEditor);
    Assert.True(vm.Grid.Rows[2].IsPlainText);
    Assert.Equal("error: boom", vm.Grid.Rows[3].DisplayText);
    Assert.Equal(["Heal", "Kill"], vm.Buttons.Select(b => b.Label));
  }

  [Fact]
  public async Task EditsAreSentWithThePanelIdAndTheRowIndex()
  {
    var f = new PanelFixture();
    f.Game.Customs["custom.player"] = Payload();
    var vm = f.Custom();

    var hp = vm.Grid.Rows[0];
    hp.BeginEdit();
    hp.EditText = "8";
    await hp.CommitAsync();
    vm.Grid.Rows[1].BoolValue = false;

    Assert.Equal(
        ["customSet:custom.player:0:Int: 8", "customSet:custom.player:1:Bool: False"],
        f.Game.Actions);
  }

  [Fact]
  public void AButtonSendsTheActionIndexTheGameGaveItNotItsPosition()
  {
    var f = new PanelFixture();
    f.Game.Customs["custom.player"] = Payload();
    var vm = f.Custom();

    vm.Buttons[0].PressCommand.Execute(null);
    vm.Buttons[1].PressCommand.Execute(null);

    Assert.Equal(["customPress:custom.player:0", "customPress:custom.player:7"], f.Game.Actions);
  }

  [Fact]
  public void RowsUpdateInPlaceAndButtonsOnlyChangeWhenTheyDo()
  {
    var f = new PanelFixture();
    f.Game.Customs["custom.player"] = Payload();
    var vm = f.Custom();
    var hp = vm.Grid.Rows[0];
    var heal = vm.Buttons[0];

    f.Game.Customs["custom.player"] = Payload(hp: 9);
    f.Tick();

    Assert.Same(hp, vm.Grid.Rows[0]);
    Assert.Equal("9", hp.DisplayText);
    Assert.Same(heal, vm.Buttons[0]);

    f.Game.Customs["custom.player"] = new CustomPayload([], [new CustomButton(1, "Reset")]);
    f.Tick();

    Assert.Empty(vm.Grid.Rows);
    Assert.Equal(["Reset"], vm.Buttons.Select(b => b.Label));
  }

  [Fact]
  public void ARowBeingEditedIsNotOverwritten()
  {
    var f = new PanelFixture();
    f.Game.Customs["custom.player"] = Payload();
    var vm = f.Custom();
    var hp = vm.Grid.Rows[0];
    hp.BeginEdit();
    hp.EditText = "123";

    f.Game.Customs["custom.player"] = Payload(hp: 6);
    f.Tick();

    Assert.Equal("123", hp.EditText);
  }

  [Fact]
  public void ThePanelIsEmptyWithoutAPayloadOrAGame()
  {
    var f = new PanelFixture(connect: false);
    var vm = f.Custom();

    Assert.Empty(vm.Grid.Rows);
    Assert.Empty(vm.Buttons);
  }

  [Fact]
  public void TheRegistryCreatesThisViewModelForGamePanels()
  {
    var h = new Harness();
    h.Registry.RegisterCustom("custom.boss", "Boss");

    var vm = h.Registry.GetViewModel("custom.boss");

    Assert.IsType<FlixelDebugger.App.ViewModels.CustomPanelViewModel>(vm);
    Assert.Equal("Boss", vm.Title);
  }
}
