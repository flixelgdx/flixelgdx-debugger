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

using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Protocol.Tests;

public class FixturePanelTests
{
  private static readonly string[] _removedKeys = ["old.key"];
  private static readonly string[] _viewportTypes = ["FlixelSprite", "FlixelSpriteGroup"];
  private static readonly string[] _saveLabels = ["Game/settings", "Game/profile1", "(not bound)"];
  private static readonly byte[] _pngBytes = [0x89, (byte)'P', (byte)'N', (byte)'G'];

  [Fact]
  public void PanelStats()
  {
    var stats = Fixture.Payload<StatsPayload>("panel_stats", 0);
    Assert.Equal(59.5f, stats.Fps);
    Assert.Equal(16.25f, stats.FrameMs);
    Assert.Equal(3.5f, stats.CpuMs);
    Assert.Equal(0.75f, stats.PresentMs);
    Assert.Equal(120, stats.Draws);
    Assert.Equal(4000, stats.Triangles);
    Assert.Equal(8000, stats.Vertices);
    Assert.Equal(3, stats.Passes);
    Assert.Equal(2, stats.Submits);
    Assert.Equal(9, stats.Pipelines);
    Assert.Equal(64.5f, stats.HeapMb);
    Assert.Equal(32.25f, stats.NativeMb);
    Assert.Equal(12.5f, stats.TextureMb);
    Assert.Equal(256.0f, stats.RingKb);
    Assert.Equal(777, stats.ObjectCount);
    Assert.Equal(1.5f, stats.SendKbps);
    Assert.Equal(0.5f, stats.RecvKbps);
    Assert.True(stats.Paused);
    Assert.Equal(12.5, stats.Elapsed);
    Assert.Equal(100.25f, stats.CamX);
    Assert.Equal(-50.5f, stats.CamY);
    Assert.Equal(2.0f, stats.CamZoom);
  }

  [Fact]
  public void PanelLogs()
  {
    var logs = Fixture.Payload<LogsPayload>("panel_logs", 1);
    Assert.True(logs.Backlog);
    Assert.Equal(
        new LogLine[]
        {
                new(1, 0.5, LogLevel.Info, false, "Game", "Main.java:10", "hello", string.Empty),
                new(
                    2,
                    1.25,
                    LogLevel.Error,
                    true,
                    "Err",
                    "Foo.bar",
                    "boom",
                    "java.lang.Error\n\tat Foo.bar"),
        },
        logs.Lines);
  }

  [Fact]
  public void PanelWatches()
  {
    var watches = Fixture.Payload<WatchesPayload>("panel_watches", 2);
    Assert.Equal(
        new NamedValue[]
        {
                new("fps", PropertyValue.FromFloat(60.0f)),
                new("name", PropertyValue.FromString("hero")),
                new("broken", PropertyValue.FromError("NullPointerException")),
        },
        watches.Rows);
  }

  [Fact]
  public void PanelTrackers()
  {
    var trackers = Fixture.Payload<TrackersPayload>("panel_trackers", 3);
    Assert.Equal(2, trackers.Blocks.Count);

    Assert.Equal("Player", trackers.Blocks[0].Name);
    Assert.Equal(
        new NamedValue[]
        {
                new("x", PropertyValue.FromFloat(10.5f)),
                new("alive", PropertyValue.FromBool(true)),
        },
        trackers.Blocks[0].Rows);

    Assert.Equal("World", trackers.Blocks[1].Name);
    Assert.Equal(
        new NamedValue[] { new("seed", PropertyValue.FromLong(42)) }, trackers.Blocks[1].Rows);
  }

  [Fact]
  public void PanelTexturesList()
  {
    var list = Fixture.Payload<TexturesListPayload>("panel_textures_list", 4);
    Assert.Equal(
        new TextureEntry[]
        {
                new(7, "assets/hero.png", 64, 32, "RGBA8", 8192, 2, false),
                new(9, "assets/hero_copy.png", 64, 32, "RGBA8", 8192, 1, true),
        },
        list.Entries);
    Assert.Equal(2, list.TextureCount);
    Assert.Equal(16384L, list.TotalGpuBytes);
    Assert.Equal(1, list.DuplicateCount);
  }

  [Fact]
  public void PanelTexturesThumbnail()
  {
    var thumbnail = Fixture.Payload<TexturesThumbnailPayload>("panel_textures_thumbnail", 4);
    Assert.Equal(7, thumbnail.Id);
    Assert.Equal(2, thumbnail.Width);
    Assert.Equal(1, thumbnail.Height);
    Assert.Equal(_pngBytes, thumbnail.Png.ToArray());
  }

  [Fact]
  public void PanelControls()
  {
    var controls = Fixture.Payload<ControlsPayload>("panel_controls", 5);
    Assert.True(controls.Paused);
    Assert.False(controls.Hitboxes);
    Assert.True(controls.FreeCam);
    Assert.Equal(20, controls.Rate);
    Assert.Equal(LogLevel.Default, controls.LogLevel);
    Assert.Equal(
        new Keybind[] { new("F1", "Toggle overlay"), new("F2", "Pause") },
        controls.Keybinds);
  }

  [Fact]
  public void PanelConsoleCommands()
  {
    var commands = Fixture.Payload<ConsoleCommandsPayload>("panel_console_commands", 6);
    Assert.Equal(
        new ConsoleCommand[]
        {
                new("god", "god", "Toggles invincibility"),
                new("quit", "quit [code]", "Exits the game"),
        },
        commands.Commands);
  }

  [Fact]
  public void PanelConsoleResult()
  {
    var result = Fixture.Payload<ConsoleResultPayload>("panel_console_result", 6);
    Assert.Equal(ConsoleStatus.Failed, result.Status);
    Assert.Equal("bad arguments", result.Message);
  }

  [Fact]
  public void PanelScene()
  {
    var scene = Fixture.Payload<ScenePayload>("panel_scene", 7);
    Assert.Equal(
        new SceneNode[]
        {
                new(1, -1, "PlayState", "play", false),
                new(2, 1, "FlixelSprite", "player", true),
                new(3, 2, "FlixelText", "label", false),
        },
        scene.Nodes);
  }

  [Fact]
  public void PanelInspectorState()
  {
    var state = Fixture.Payload<InspectorStatePayload>("panel_inspector_state", 8);
    Assert.Equal(2, state.SelectedId);
    Assert.Equal("play/player", state.Path);
    Assert.True(state.HasTexture);
    Assert.Equal(2, state.Sections.Count);

    Assert.Equal("FlixelSprite", state.Sections[0].TypeName);
    Assert.Equal("Sprite", state.Sections[0].DisplayName);
    Assert.Equal(
        new PropertyRow[]
        {
                new("x", PropertyValue.FromFloat(10.5f), true),
                new("visible", PropertyValue.FromBool(true), true),
                new("id", PropertyValue.FromInt(2), false),
        },
        state.Sections[0].Properties);

    Assert.Equal("FlixelObject", state.Sections[1].TypeName);
    Assert.Equal("Object", state.Sections[1].DisplayName);
    Assert.Equal(
        new PropertyRow[] { new("tag", PropertyValue.FromString("p"), true) },
        state.Sections[1].Properties);
  }

  [Fact]
  public void PanelInspectorThumbnail()
  {
    var thumbnail = Fixture.Payload<InspectorThumbnailPayload>("panel_inspector_thumbnail", 8);
    Assert.Equal(2, thumbnail.Id);
    Assert.Equal(2, thumbnail.Width);
    Assert.Equal(1, thumbnail.Height);
    Assert.Equal(new byte[] { 9, 8, 7 }, thumbnail.Png.ToArray());
  }

  [Fact]
  public void PanelSave()
  {
    var save = Fixture.Payload<SavePayload>("panel_save", 9);
    Assert.Equal(
        new SaveRow[]
        {
                new("player.name", "\"Kori\"", ValueKind.String, false),
                new("player.level", "12", ValueKind.Int, true),
        },
        save.Rows);
    Assert.Equal(_removedKeys, save.Removed);
    Assert.Equal("Saved", save.Status);
    Assert.False(save.IsError);
    Assert.Equal(_saveLabels, save.Saves);
    Assert.Equal(1, save.Selected);
  }

  [Fact]
  public void PanelSaveFromAnOlderGameHasNoSaveList()
  {
    // The fixture without its trailing save list (3 labels and the u16 index) is what a game
    // older than the save picker sends.
    var bytes = Fixture.Load("panel_save");
    var listStart = bytes.Length - 2 - (2 + 2 + 13 + 2 + 13 + 2 + 11);
    var older = bytes[..listStart];

    var data = Assert.IsType<PanelData>(Fixture.NewDecoder().Decode(older));
    var save = Assert.IsType<SavePayload>(data.Payload);

    Assert.Equal(2, save.Rows.Count);
    Assert.Equal("Saved", save.Status);
    Assert.False(save.IsError);
    Assert.Empty(save.Saves);
    Assert.Equal(0, save.Selected);
    Assert.Equal(older, data.ToArray());
  }

  [Fact]
  public void PanelViewport()
  {
    var viewport = Fixture.Payload<ViewportPayload>("panel_viewport", 11);
    Assert.Equal(0, viewport.Camera);
    Assert.Equal(2, viewport.CameraCount);
    Assert.Equal(320f, viewport.ViewWidth);
    Assert.Equal(180f, viewport.ViewHeight);
    Assert.Equal(2f, viewport.Zoom);
    Assert.Equal(100.5f, viewport.ScrollX);
    Assert.Equal(-40f, viewport.ScrollY);
    Assert.True(viewport.FreeCam);
    Assert.False(viewport.Paused);
    Assert.True(viewport.Hitboxes);
    Assert.Equal(3, viewport.SelectedId);
    Assert.Equal(_viewportTypes, viewport.Types);
    Assert.Equal(
        new ViewportBox[]
        {
          new(2, 10f, 20f, 64f, 32f, 1, ViewportBoxState.Group | ViewportBoxState.Inactive),
          new(3, -8.5f, 150.25f, 16f, 16f, 0, ViewportBoxState.Selected),
        },
        viewport.Boxes);

    var group = viewport.Boxes[0];
    Assert.True(group.IsGroup);
    Assert.True(group.IsInactive);
    Assert.False(group.IsSelected);
    Assert.Equal("FlixelSpriteGroup", viewport.TypeOf(group));
    Assert.True(viewport.Boxes[1].IsSelected);
    Assert.Equal("FlixelSprite", viewport.TypeOf(viewport.Boxes[1]));
    Assert.True(group.Contains(10f, 52f));
    Assert.False(group.Contains(9.9f, 30f));
  }

  [Fact]
  public void PanelCustom()
  {
    var custom = Fixture.Payload<CustomPayload>("panel_custom", 10);
    Assert.Equal(
        new PropertyRow[]
        {
                new("speed", PropertyValue.FromFloat(2.5f), true),
                new("label", PropertyValue.FromString("hi"), false),
        },
        custom.Rows);
    Assert.Equal(
        new CustomButton[] { new(0, "Reset"), new(1, "Spawn") },
        custom.Buttons);
  }
}
