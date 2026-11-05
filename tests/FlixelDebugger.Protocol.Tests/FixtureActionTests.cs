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

using FlixelDebugger.Protocol.Actions;
using FlixelDebugger.Protocol.Messages;
using A = FlixelDebugger.Protocol.ProtocolConstants.Actions;

namespace FlixelDebugger.Protocol.Tests;

public class FixtureActionTests
{
  [Fact]
  public void ActionLogsClear() =>
      Fixture.Action<LogsClearAction>("action_logs_clear", 1, A.Logs.Clear);

  [Fact]
  public void ActionTexturesRequestThumbnail()
  {
    var action = Fixture.Action<TexturesRequestThumbnailAction>(
        "action_textures_request_thumbnail", 4, A.Textures.RequestThumbnail);
    Assert.Equal(7, action.Id);
  }

  [Fact]
  public void ActionControlsSetPaused()
  {
    var action = Fixture.Action<ControlsSetPausedAction>(
        "action_controls_set_paused", 5, A.Controls.SetPaused);
    Assert.True(action.Paused);
  }

  [Fact]
  public void ActionControlsStep() =>
      Fixture.Action<ControlsStepAction>("action_controls_step", 5, A.Controls.Step);

  [Fact]
  public void ActionControlsSetHitboxes()
  {
    var action = Fixture.Action<ControlsSetHitboxesAction>(
        "action_controls_set_hitboxes", 5, A.Controls.SetHitboxes);
    Assert.True(action.Hitboxes);
  }

  [Fact]
  public void ActionControlsSetFreeCam()
  {
    var action = Fixture.Action<ControlsSetFreeCamAction>(
        "action_controls_set_freecam", 5, A.Controls.SetFreeCam);
    Assert.False(action.FreeCam);
  }

  [Fact]
  public void ActionControlsSetRate()
  {
    var action = Fixture.Action<ControlsSetRateAction>(
        "action_controls_set_rate", 5, A.Controls.SetRate);
    Assert.Equal(30, action.Rate);
  }

  [Fact]
  public void ActionControlsSetLogLevel()
  {
    var action = Fixture.Action<ControlsSetLogLevelAction>(
        "action_controls_set_log_level", 5, A.Controls.SetLogLevel);
    Assert.Equal(LogLevel.Warn, action.Level);
  }

  [Fact]
  public void ActionConsoleRun()
  {
    var action = Fixture.Action<ConsoleRunAction>("action_console_run", 6, A.Console.Run);
    Assert.Equal("god on", action.Line);
  }

  [Fact]
  public void ActionSceneRequestTree() =>
      Fixture.Action<SceneRequestTreeAction>("action_scene_request_tree", 7, A.Scene.RequestTree);

  [Fact]
  public void ActionInspectorSelect()
  {
    var action = Fixture.Action<InspectorSelectAction>(
        "action_inspector_select", 8, A.Inspector.Select);
    Assert.Equal(2, action.Id);
  }

  [Fact]
  public void ActionInspectorSelectPath()
  {
    var action = Fixture.Action<InspectorSelectPathAction>(
        "action_inspector_select_path", 8, A.Inspector.SelectPath);
    Assert.Equal("play/player", action.Path);
  }

  [Fact]
  public void ActionInspectorSelectParent() =>
      Fixture.Action<InspectorSelectParentAction>(
          "action_inspector_select_parent", 8, A.Inspector.SelectParent);

  [Fact]
  public void ActionInspectorClear() =>
      Fixture.Action<InspectorClearAction>("action_inspector_clear", 8, A.Inspector.Clear);

  [Fact]
  public void ActionInspectorGoTo() =>
      Fixture.Action<InspectorGoToAction>("action_inspector_go_to", 8, A.Inspector.GoTo);

  [Fact]
  public void ActionInspectorSetProperty()
  {
    var action = Fixture.Action<InspectorSetPropertyAction>(
        "action_inspector_set_property", 8, A.Inspector.SetProperty);
    Assert.Equal(2, action.Id);
    Assert.Equal(0, action.SectionIndex);
    Assert.Equal(0, action.PropertyIndex);
    Assert.Equal(PropertyValue.FromFloat(12.5f), action.Value);
  }

  [Fact]
  public void ActionInspectorRequestThumbnail()
  {
    var action = Fixture.Action<InspectorRequestThumbnailAction>(
        "action_inspector_request_thumbnail", 8, A.Inspector.RequestThumbnail);
    Assert.Equal(2, action.Id);
  }

  [Fact]
  public void ActionSaveSet()
  {
    var action = Fixture.Action<SaveSetAction>("action_save_set", 9, A.Save.Set);
    Assert.Equal("player.level", action.Path);
    Assert.Equal("13", action.Text);
  }

  [Fact]
  public void ActionSaveResetKey()
  {
    var action = Fixture.Action<SaveResetKeyAction>("action_save_reset_key", 9, A.Save.ResetKey);
    Assert.Equal("player.level", action.Path);
  }

  [Fact]
  public void ActionSaveSaveNow() =>
      Fixture.Action<SaveSaveNowAction>("action_save_save_now", 9, A.Save.SaveNow);

  [Fact]
  public void ActionSaveResetAll() =>
      Fixture.Action<SaveResetAllAction>("action_save_reset_all", 9, A.Save.ResetAll);

  [Fact]
  public void ActionSaveSelect()
  {
    var action = Fixture.Action<SaveSelectAction>("action_save_select", 9, A.Save.Select);
    Assert.Equal(1, action.Index);
  }

  [Fact]
  public void SaveSelectEncodesToTheFixtureBytes()
  {
    var bytes = new PanelAction(9, new SaveSelectAction(1)).ToArray();
    Assert.Equal(new byte[] { 5, 9, 0, 5, 1, 0 }, bytes);
    Assert.Equal(Fixture.Load("action_save_select"), bytes);
  }

  [Fact]
  public void ActionCustomPress()
  {
    var action = Fixture.Action<CustomPressAction>("action_custom_press", 10, A.Custom.Press);
    Assert.Equal(1, action.ActionIndex);
  }

  [Fact]
  public void ActionCustomSetProperty()
  {
    var action = Fixture.Action<CustomSetPropertyAction>(
        "action_custom_set_property", 10, A.Custom.SetProperty);
    Assert.Equal(0, action.PropertyIndex);
    Assert.Equal(PropertyValue.FromFloat(3.0f), action.Value);
  }
}
