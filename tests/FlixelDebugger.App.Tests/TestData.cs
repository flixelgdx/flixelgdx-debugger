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

using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

internal static class TestData
{
  public static StatsPayload Stats(float fps, float heapMb = 64f, bool paused = false) => new(
      fps, 16.6f, 5f, 1f, 100, 2000, 6000, 3, 2, 4, heapMb, 32f, 16f, 8f, 250, 1.5f, 2.5f, paused,
      12.5, 10f, 20f, 1.5f);

  public static ControlsPayload Controls(
      bool paused = false, bool hitboxes = false, bool freeCam = false) =>
      new(paused, hitboxes, freeCam, 20, LogLevel.Default, []);

  public static LogLine Log(
      long seq,
      string message = "hello",
      LogLevel level = LogLevel.Info,
      bool uncaught = false,
      string stack = "") => new(seq, 1.5, level, uncaught, "Game", "Main.java:10", message, stack);

  public static PropertyRow Row(string name, PropertyValue value, bool editable = false) =>
      new(name, value, editable);

  public static InspectorSection Section(string type, params PropertyRow[] rows) =>
      new(type, type, rows);

  public static InspectorStatePayload Inspector(
      int id,
      string path = "state.0",
      bool hasTexture = false,
      params InspectorSection[] sections) =>
      new(id, path, sections, hasTexture);

  public static NamedValue Named(string name, PropertyValue value) => new(name, value);

  public static SceneTree Tree() => new(
  [
      new SceneNode(1, -1, "State", "state", false),
      new SceneNode(2, 1, "Group", "0", false),
      new SceneNode(3, 1, "Sprite", "1", false),
      new SceneNode(4, 2, "Sprite", "0", false),
      new SceneNode(9, -1, "Manager", "managers", true),
  ]);

  public static TextureEntry Texture(
      int id, string name = "a.png", int width = 16, int height = 16, long bytes = 1024,
      int users = 1, bool duplicate = false) =>
      new(id, name, width, height, "RGBA8", bytes, users, duplicate);

  public static TexturesListPayload Textures(params TextureEntry[] entries) =>
      new(entries, entries.Length, entries.Sum(e => e.GpuBytes), entries.Count(e => e.Duplicate));

  public static byte[] Png { get; } = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
}
