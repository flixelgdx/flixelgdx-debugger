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
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class InspectorPanelViewModelTests
{
  private static InspectorStatePayload Sprite(int id = 5, float x = 10f, bool hasTexture = false) =>
      TestData.Inspector(
          id,
          "state.1",
          hasTexture,
          TestData.Section(
              "FlixelSprite",
              TestData.Row("x", PropertyValue.FromFloat(x), true),
              TestData.Row("name", PropertyValue.FromString("hero"))),
          TestData.Section(
              "FlixelObject",
              TestData.Row("alive", PropertyValue.FromBool(true), true),
              TestData.Row("bad", PropertyValue.FromError("boom"))));

  [Fact]
  public void WithNothingSelectedThePanelShowsOnlyTheHint()
  {
    var f = new PanelFixture();
    f.Game.Inspector = TestData.Inspector(-1, string.Empty);

    var vm = f.Inspector();

    Assert.False(vm.HasSelection);
    Assert.Empty(vm.Sections);
    Assert.Equal(-1, vm.SelectedId);
    Assert.False(string.IsNullOrEmpty(InspectorPanelViewModel.EmptyHint));
  }

  [Fact]
  public void ShowsThePathAndOneSectionPerComponentInPayloadOrder()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite();

    var vm = f.Inspector();

    Assert.True(vm.HasSelection);
    Assert.Equal("state.1", vm.Path);
    Assert.Equal(["FlixelSprite", "FlixelObject"], vm.Sections.Select(s => s.DisplayName));
    Assert.Equal(["x", "name"], vm.Sections[0].Grid.Rows.Select(r => r.Name));
    Assert.Equal("10", vm.Sections[0].Grid.Rows[0].DisplayText);
    Assert.True(vm.Sections[0].Grid.Rows[0].IsTextEditor);
    Assert.True(vm.Sections[0].Grid.Rows[1].IsPlainText);
    Assert.True(vm.Sections[1].Grid.Rows[0].IsBoolEditor);
    Assert.Equal("error: boom", vm.Sections[1].Grid.Rows[1].DisplayText);
    Assert.True(vm.Sections[1].Grid.Rows[1].IsError);
  }

  [Fact]
  public async Task AnEditNamesTheSelectedObjectTheSectionAndTheRow()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite();
    var vm = f.Inspector();

    var x = vm.Sections[0].Grid.Rows[0];
    x.BeginEdit();
    x.EditText = "12.5";
    await x.CommitAsync();
    vm.Sections[1].Grid.Rows[0].BoolValue = false;

    Assert.Equal(
        ["setProperty:5:0:0:Float: 12.5", "setProperty:5:1:0:Bool: False"], f.Game.Actions);
  }

  [Fact]
  public void ANewStateOfTheSameObjectUpdatesTheRowsInPlace()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite();
    var vm = f.Inspector();
    var section = vm.Sections[0];
    var row = section.Grid.Rows[0];

    f.Game.Inspector = Sprite(x: 11);
    f.Tick();

    Assert.Same(section, vm.Sections[0]);
    Assert.Same(row, section.Grid.Rows[0]);
    Assert.Equal("11", row.DisplayText);
  }

  [Fact]
  public void AnIncomingStateDoesNotOverwriteTheRowBeingEdited()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite();
    var vm = f.Inspector();
    var row = vm.Sections[0].Grid.Rows[0];
    row.BeginEdit();
    row.EditText = "99";

    f.Game.Inspector = Sprite(x: 11);
    f.Tick();

    Assert.Equal("99", row.EditText);
    Assert.True(row.IsEditing);
  }

  [Fact]
  public void TheOpenStateOfASectionSurvivesAnotherSelection()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5);
    var vm = f.Inspector();
    vm.Sections[0].IsExpanded = false;

    f.Game.Inspector = Sprite(6);
    f.Tick();

    Assert.False(vm.Sections[0].IsExpanded);
    Assert.True(vm.Sections[1].IsExpanded);
  }

  [Fact]
  public void ParentGoToAndClearSendTheirActions()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite();
    var vm = f.Inspector();

    vm.SelectParentCommand.Execute(null);
    vm.GoToCommand.Execute(null);
    vm.ClearCommand.Execute(null);

    Assert.Equal(["selectParent", "goTo", "clearSelection"], f.Game.Actions);
  }

  [Fact]
  public void EnterInThePathBoxSelectsThePathAndIgnoresBlankText()
  {
    var f = new PanelFixture();
    var vm = f.Inspector();

    vm.PathEntry = "   ";
    vm.SelectPathCommand.Execute(null);
    vm.PathEntry = " state.3.0 ";
    vm.SelectPathCommand.Execute(null);

    Assert.Equal(["selectPath:state.3.0"], f.Game.Actions);
    Assert.Equal(string.Empty, vm.PathEntry);
  }

  [Fact]
  public void TheTabOpensOnlyWhenTheGamesSelectionChanges()
  {
    var f = new PanelFixture();
    var vm = f.Inspector();
    Assert.Empty(f.Layout.Revealed);

    f.Game.Inspector = Sprite(5);
    f.Tick();
    Assert.Equal(["inspector"], f.Layout.Revealed);

    // The same object with other values is not a new selection.
    f.Game.Inspector = Sprite(5, x: 99);
    f.Tick();
    Assert.Single(f.Layout.Revealed);

    f.Game.Inspector = Sprite(6);
    f.Tick();
    Assert.Equal(2, f.Layout.Revealed.Count);

    f.Game.Inspector = TestData.Inspector(-1, string.Empty);
    f.Tick();
    Assert.Equal(2, f.Layout.Revealed.Count);

    // Going from nothing to an object counts, even the same object as before.
    f.Game.Inspector = Sprite(6);
    f.Tick();
    Assert.Equal(3, f.Layout.Revealed.Count);
    Assert.NotNull(vm);
  }

  [Fact]
  public void ASelectionThatAlreadyExistedWhenThePanelWasCreatedDoesNotOpenTheTab()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5);

    f.Inspector();
    f.Tick();

    Assert.Empty(f.Layout.Revealed);
  }

  [Fact]
  public void ASelectionTheUserMadeInTheScenePanelDoesNotStealTheTab()
  {
    var f = new PanelFixture();
    f.Inspector();
    f.Origin.MarkLocal(7);

    f.Game.Inspector = Sprite(7);
    f.Tick();
    Assert.Empty(f.Layout.Revealed);

    f.Game.Inspector = Sprite(8);
    f.Tick();
    Assert.Single(f.Layout.Revealed);
  }

  [Fact]
  public void ThePreviewRequestsAThumbnailWhenItOpensAndWhenTheSelectionChanges()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5, hasTexture: true);
    var vm = f.Inspector();
    Assert.True(vm.HasTexture);
    f.Tick();
    Assert.Empty(f.Game.Actions);

    vm.PreviewExpanded = true;
    f.Tick();
    f.Tick();
    Assert.Equal(["inspectorThumbnail:5"], f.Game.Actions);

    f.Game.Inspector = Sprite(6, hasTexture: true);
    f.Tick();
    Assert.Equal(["inspectorThumbnail:5", "inspectorThumbnail:6"], f.Game.Actions);
  }

  [Fact]
  public void ThePreviewNeverRequestsWithoutATextureOrWhileClosed()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5, hasTexture: false);
    var vm = f.Inspector();

    vm.PreviewExpanded = true;
    f.Tick();

    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void ThePreviewShowsTheThumbnailWhenItArrivesAndClearsWhenClosed()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5, hasTexture: true);
    var vm = f.Inspector();
    vm.PreviewExpanded = true;
    Assert.False(vm.Preview.HasImage);
    Assert.Equal("Waiting for the game...", vm.Preview.Status);

    f.Game.InspectorThumbnails.Set(new Thumbnail(5, 8, 4, TestData.Png));
    f.Tick();

    Assert.True(vm.Preview.HasImage);
    Assert.Equal(8, vm.Preview.ImageWidth);
    Assert.Equal(4, vm.Preview.ImageHeight);
    Assert.Equal(TestData.Png, vm.Preview.Image);
    Assert.Equal("8 x 4 px", vm.Preview.Status);

    vm.Preview.Zoom = 2;
    Assert.Equal(16, vm.Preview.DisplayWidth);
    Assert.Equal(8, vm.Preview.DisplayHeight);

    vm.PreviewExpanded = false;

    Assert.False(vm.Preview.HasImage);
  }

  [Fact]
  public void ThePreviewGivesUpWhenTheGameNeverAnswers()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5, hasTexture: true);
    var vm = f.Inspector();
    vm.PreviewExpanded = true;

    for (var i = 0; i < 95; i++)
    {
      f.Tick();
    }

    Assert.Equal("The game did not send a preview.", vm.Preview.Status);
  }

  [Fact]
  public void RefreshPreviewAsksAgain()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5, hasTexture: true);
    var vm = f.Inspector();
    vm.PreviewExpanded = true;

    vm.RefreshPreviewCommand.Execute(null);

    Assert.Equal(["inspectorThumbnail:5", "inspectorThumbnail:5"], f.Game.Actions);
  }

  [Fact]
  public void SwitchingTheGameClearsTheSections()
  {
    var f = new PanelFixture();
    f.Game.Inspector = Sprite(5);
    var vm = f.Inspector();

    f.Active.Current = new FakeGameSession("Other");

    Assert.Empty(vm.Sections);
    Assert.False(vm.HasSelection);
  }
}
