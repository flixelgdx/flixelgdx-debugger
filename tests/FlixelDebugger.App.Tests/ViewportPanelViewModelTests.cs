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
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class ViewportPanelViewModelTests
{
  // With this canvas the 320 x 180 view fills 640 x 360 pixels: two pixels per view unit, with the
  // view's top left corner at pixel 8, 8 (the margin).
  private const double CanvasWidth = 656;
  private const double CanvasHeight = 376;

  private static double Px(double view) => 8 + (view * 2);

  // Box 1 is a group, box 3 is selected and drawn on top of it, and box 4 is off to the side.
  private static ViewportPayload Scene() => TestData.Viewport(
      TestData.Box(1, 10, 10, 50, 40, ViewportBoxState.Group),
      TestData.Box(3, 20, 20, 16, 16, ViewportBoxState.Selected),
      TestData.Box(4, 80, 60, 30, 30, ViewportBoxState.Inactive));

  private static ViewportPanelViewModel Make(PanelFixture f, ViewportPayload? payload = null)
  {
    f.Game.Viewport = payload ?? Scene();
    var vm = f.Viewport();
    vm.Resize(CanvasWidth, CanvasHeight);
    return vm;
  }

  private static void Drag(
      ViewportPanelViewModel vm,
      ViewportButton button,
      double fromX,
      double fromY,
      params (double X, double Y)[] path)
  {
    vm.Press(button, fromX, fromY);
    foreach (var (x, y) in path)
    {
      vm.Move(x, y);
    }
  }

  [Fact]
  public void ThePayloadFillsTheToolbarWithoutSendingAnything()
  {
    var f = new PanelFixture();
    var vm = Make(
        f,
        Scene() with
        {
          Zoom = 2,
          ScrollX = 12.5f,
          ScrollY = -4,
          FreeCam = true,
          Paused = true,
          Hitboxes = true,
        });

    Assert.True(vm.HasViewport);
    Assert.Equal(string.Empty, vm.Hint);
    Assert.True(vm.FreeCam);
    Assert.True(vm.Paused);
    Assert.Equal("Resume", vm.PauseText);
    Assert.True(vm.Hitboxes);
    Assert.Equal("Zoom 2x   Scroll 12.5, -4", vm.Readout);
    Assert.Equal(3, vm.Payload!.Boxes.Count);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void TheHintExplainsWhyThereIsNoMap()
  {
    var f = new PanelFixture(connect: false);
    var vm = f.Viewport();
    Assert.Equal(ViewportPanelViewModel.NoGameHint, vm.Hint);
    Assert.False(vm.HasViewport);

    f.Active.Current = f.Game;
    Assert.Equal(ViewportPanelViewModel.WaitingHint, vm.Hint);

    f.Game.Viewport = Scene() with { CameraCount = 0, Boxes = [] };
    f.Tick();
    Assert.Equal(ViewportPanelViewModel.NoCameraHint, vm.Hint);
    Assert.False(vm.HasViewport);
    Assert.True(vm.Fit.IsEmpty);
  }

  [Fact]
  public void TheViewIsLetterboxedToKeepItsAspectRatio()
  {
    var wide = ViewportFit.Compute(1016, 196, 320, 180, 8);
    Assert.Equal(1, wide.Scale, 6);
    Assert.Equal(348, wide.Left, 6);
    Assert.Equal(8, wide.Top, 6);
    Assert.Equal(320, wide.Width, 6);

    var tall = ViewportFit.Compute(336, 1000, 320, 180, 8);
    Assert.Equal(1, tall.Scale, 6);
    Assert.Equal(8, tall.Left, 6);
    Assert.Equal(410, tall.Top, 6);

    Assert.Equal(50, tall.ToViewX(tall.ToCanvasX(50)), 6);
    Assert.True(ViewportFit.Compute(0, 100, 320, 180, 8).IsEmpty);
    Assert.True(ViewportFit.Compute(400, 300, 0, 180, 8).IsEmpty);
    Assert.True(ViewportFit.Compute(400, 300, float.NaN, 180, 8).IsEmpty);
  }

  [Fact]
  public void AClickOnABoxPicksThatPointOfTheView()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    vm.Press(ViewportButton.Left, Px(15), Px(12));
    vm.Move(Px(15) + 1, Px(12) + 1);
    vm.Release();

    Assert.Equal(["pick:15:12"], f.Game.Actions);
  }

  [Fact]
  public void APickHereDoesNotPullTheInspectorInFront()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    var inspector = f.Inspector();

    // Box 4 is the topmost box at that point, so the game will select it.
    vm.Press(ViewportButton.Left, Px(90), Px(70));
    vm.Release();
    f.Game.Inspector = TestData.Inspector(4);
    f.Tick();

    Assert.Equal(4, inspector.SelectedId);
    Assert.Empty(f.Layout.Revealed);

    // A selection the game makes on its own still brings the inspector forward.
    f.Game.Inspector = TestData.Inspector(1);
    f.Tick();
    Assert.Equal(["inspector"], f.Layout.Revealed);
  }

  [Fact]
  public void ADoubleClickOnEmptySpaceClearsTheSelection()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    // One click on empty space does nothing, so a pan can start there.
    vm.Press(ViewportButton.Left, Px(200), Px(150));
    vm.Release();
    Assert.Empty(f.Game.Actions);

    vm.Press(ViewportButton.Left, Px(200), Px(150), clickCount: 2);
    vm.Release();
    Assert.Equal(["pick:200:150"], f.Game.Actions);
  }

  [Fact]
  public void DraggingTheSelectedBoxMovesItByTheDragDistanceInWorldUnits()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    var box = vm.Payload!.Boxes[1];

    // 20 pixels right and 10 down is 10 and 5 view units, which are 10 and 5 world units.
    Drag(
        vm,
        ViewportButton.Left,
        Px(25),
        Px(25),
        (Px(25) + 8, Px(25)),
        (Px(25) + 20, Px(25) + 10));

    // The box follows the pointer at once, before the game hears about it.
    Assert.Equal(3, vm.DraggedId);
    Assert.Equal(30, vm.Shown(box).X, 4);
    Assert.Equal(25, vm.Shown(box).Y, 4);
    Assert.Empty(f.Game.Actions);

    f.Tick();
    Assert.Equal(["move:3:10:5"], f.Game.Actions);
  }

  [Fact]
  public void MoveIsSentAboutThirtyTimesASecondWithTheDistancesAddedUp()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.Equal(TimeSpan.FromMilliseconds(33), ViewportPanelViewModel.ViewportInterval);

    vm.Press(ViewportButton.Left, Px(25), Px(25));
    for (var i = 1; i <= 10; i++)
    {
      vm.Move(Px(25) + (i * 2), Px(25));
    }

    Assert.Empty(f.Game.Actions);
    f.Tick();
    f.Tick();
    Assert.Equal(["move:3:10:0"], f.Game.Actions);

    // Letting go sends what is left right away.
    vm.Move(Px(25) + 24, Px(25) - 2);
    vm.Release();
    Assert.Equal(["move:3:10:0", "move:3:2:-1"], f.Game.Actions);
  }

  [Fact]
  public void ATinyWiggleIsStillAClick()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    Drag(vm, ViewportButton.Left, Px(25), Px(25), (Px(25) + 2, Px(25) + 2));
    vm.Release();
    f.Tick();

    Assert.Equal(["pick:25:25"], f.Game.Actions);
    Assert.Equal(-1, vm.DraggedId);
  }

  [Fact]
  public void ADroppedBoxStaysWhereItWasDroppedUntilTheGameCatchesUp()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    var box = vm.Payload!.Boxes[1];

    Drag(vm, ViewportButton.Left, Px(25), Px(25), (Px(25) + 20, Px(25)));
    vm.Release();

    // The game's next state still has the old position, so the drop is kept on screen.
    f.Game.Viewport = Scene() with { ScrollX = 1 };
    f.Tick();
    Assert.Equal(30, vm.Shown(box).X, 4);

    var moved = Scene();
    f.Game.Viewport = moved with
    {
      Boxes = [moved.Boxes[0], moved.Boxes[1] with { X = 30 }, moved.Boxes[2]],
    };
    f.Tick();
    Assert.Equal(-1, vm.DraggedId);
  }

  [Fact]
  public void DraggingEmptySpaceOrABoxThatIsNotSelectedPansWithTheSignFlipped()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    // Dragging right by 40 pixels (20 view units) slides the world right: the camera goes left.
    Drag(vm, ViewportButton.Left, Px(200), Px(150), (Px(200) + 40, Px(150) - 10));
    f.Tick();
    vm.Release();
    Assert.Equal(["pan:-20:5"], f.Game.Actions);

    // Box 4 is not selected, so a drag on it pans too and picks nothing.
    f.Game.Actions.Clear();
    Drag(vm, ViewportButton.Left, Px(90), Px(70), (Px(90) - 20, Px(70)));
    vm.Release();
    Assert.Equal(["pan:10:0"], f.Game.Actions);
  }

  [Fact]
  public void TheMiddleAndRightButtonsPanEvenOnTheSelectedBox()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    Drag(vm, ViewportButton.Right, Px(25), Px(25), (Px(25), Px(25) + 30));
    vm.Release();
    Drag(vm, ViewportButton.Middle, Px(25), Px(25), (Px(25) - 30, Px(25)));
    vm.Release();

    // A right or middle click picks nothing either.
    vm.Press(ViewportButton.Right, Px(25), Px(25));
    vm.Release();

    Assert.Equal(["pan:0:-15", "pan:15:0"], f.Game.Actions);
    Assert.Equal(-1, vm.DraggedId);
  }

  [Fact]
  public void TheWheelZoomsAroundThePointer()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    vm.Wheel(1, Px(100), Px(50));
    f.Tick();
    vm.Wheel(-1, Px(40), Px(30));
    f.Tick();

    // Two notches before the next refresh become one zoom of 1.25 * 1.25.
    vm.Wheel(1, Px(10), Px(20));
    vm.Wheel(1, Px(10), Px(20));
    f.Tick();

    Assert.Equal(["zoom:1.25:100:50", "zoom:0.8:40:30", "zoom:1.5625:10:20"], f.Game.Actions);
  }

  [Fact]
  public void TheCameraPickerOnlyShowsWithSeveralCameras()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.False(vm.HasManyCameras);
    Assert.Equal(["Camera 0 (main)"], vm.Cameras);

    f.Game.Viewport = Scene() with { CameraCount = 3, Camera = 1 };
    f.Tick();
    Assert.True(vm.HasManyCameras);
    Assert.Equal(["Camera 0 (main)", "Camera 1", "Camera 2"], vm.Cameras);
    Assert.Equal(1, vm.SelectedCameraIndex);
    Assert.Empty(f.Game.Actions);

    f.Game.Viewport = Scene();
    f.Tick();
    Assert.False(vm.HasManyCameras);
    Assert.Equal(0, vm.SelectedCameraIndex);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void PickingACameraSendsItAndKeepsItUntilTheGameConfirms()
  {
    var f = new PanelFixture();
    var vm = Make(f, Scene() with { CameraCount = 3 });

    vm.SelectedCameraIndex = 2;
    Assert.Equal(["viewportCamera:2"], f.Game.Actions);

    // A state sent before the game switched does not move the picker back.
    f.Game.Viewport = Scene() with { CameraCount = 3, ScrollX = 5 };
    f.Tick();
    Assert.Equal(2, vm.SelectedCameraIndex);

    f.Game.Viewport = Scene() with { CameraCount = 3, Camera = 2 };
    f.Tick();
    Assert.Equal(2, vm.SelectedCameraIndex);
    Assert.Single(f.Game.Actions);
  }

  [Fact]
  public void TheComboBoxClearingItsSelectionIsIgnored()
  {
    var f = new PanelFixture();
    var vm = Make(f, Scene() with { CameraCount = 3, Camera = 1 });
    var raised = new List<string?>();
    vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    vm.SelectedCameraIndex = -1;
    vm.SelectedCameraIndex = 7;

    Assert.Equal(1, vm.SelectedCameraIndex);
    Assert.Empty(f.Game.Actions);
    Assert.Equal(2, raised.Count(p => p == nameof(ViewportPanelViewModel.SelectedCameraIndex)));
  }

  [Fact]
  public void TheToolbarButtonsUseTheControlsActions()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Assert.False(vm.StepCommand.CanExecute(null));

    vm.TogglePauseCommand.Execute(null);
    vm.Hitboxes = true;
    vm.ResetCameraCommand.Execute(null);

    f.Game.Viewport = Scene() with { Paused = true };
    f.Tick();
    Assert.Equal("Resume", vm.PauseText);
    Assert.True(vm.StepCommand.CanExecute(null));
    vm.StepCommand.Execute(null);
    vm.TogglePauseCommand.Execute(null);

    Assert.Equal(
        ["pause:True", "hitboxes:True", "resetCamera", "step", "pause:False"], f.Game.Actions);
  }

  [Fact]
  public void HoveringABoxNamesItsTypeAndId()
  {
    var f = new PanelFixture();
    var vm = Make(f);

    vm.Move(Px(12), Px(12));
    Assert.Equal(1, vm.HoveredId);
    Assert.Equal("FlixelGroup #1 (group)", vm.HoverText);

    vm.Move(Px(25), Px(25));
    Assert.Equal("FlixelSprite #3", vm.HoverText);

    vm.Move(Px(100), Px(80));
    Assert.Equal("FlixelSprite #4 (inactive)", vm.HoverText);

    vm.Leave();
    Assert.Equal(-1, vm.HoveredId);
    Assert.Equal(string.Empty, vm.HoverText);
  }

  [Fact]
  public void SwitchingGamesForgetsTheDrag()
  {
    var f = new PanelFixture();
    var vm = Make(f);
    Drag(vm, ViewportButton.Left, Px(25), Px(25), (Px(25) + 20, Px(25)));

    var other = new FakeGameSession("Other");
    f.Active.Current = other;
    f.Tick();

    Assert.Equal(-1, vm.DraggedId);
    Assert.Null(vm.Payload);
    Assert.Empty(f.Game.Actions);
    Assert.Empty(other.Actions);
  }
}
