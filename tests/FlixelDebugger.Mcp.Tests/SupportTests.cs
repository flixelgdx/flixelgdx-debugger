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
using FlixelDebugger.Mcp.Support;
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Mcp.Tests;

public class SupportTests
{
  [Theory]
  [InlineData("SPACE", 62)]
  [InlineData("space", 62)]
  [InlineData("A", 29)]
  [InlineData("left", 21)]
  [InlineData("page up", 92)]
  [InlineData("PAGE-UP", 92)]
  [InlineData("esc", 111)]
  [InlineData("ctrl", 129)]
  [InlineData("0", 7)]
  [InlineData("F12", 142)]
  [InlineData("NUMPAD_5", 149)]
  public void KeyNamesMapToFrameworkCodes(string name, int code)
  {
    Assert.True(KeyNames.TryGet(name, out var found));
    Assert.Equal(code, found);
  }

  [Theory]
  [InlineData("")]
  [InlineData("banana")]
  [InlineData("0000")]
  [InlineData("256")]
  [InlineData("-3")]
  public void UnknownKeysAreRejected(string name) => Assert.False(KeyNames.TryGet(name, out _));

  [Fact]
  public void ARawCodeIsAccepted()
  {
    Assert.True(KeyNames.TryGet("200", out var code));
    Assert.Equal(200, code);
  }

  [Fact]
  public void ScenePathsJoinLabelsFromTheRoot()
  {
    var tree = new SceneTree(
    [
      new SceneNode(1, -1, "State", "state", false),
      new SceneNode(2, 1, "Group", "3", false),
      new SceneNode(3, 2, "Sprite", "0", false),
      new SceneNode(4, -1, "Root", "managers", true),
    ]);

    var paths = SceneAddress.BuildPaths(tree);

    Assert.Equal("state", paths[1]);
    Assert.Equal("state.3", paths[2]);
    Assert.Equal("state.3.0", paths[3]);
    Assert.Equal("managers", paths[4]);
    Assert.True(SceneAddress.TryResolveId(tree, "state.3.0", out var id));
    Assert.Equal(3, id);
    Assert.True(SceneAddress.TryResolveId(tree, " 42 ", out id));
    Assert.Equal(42, id);
    Assert.False(SceneAddress.TryResolveId(tree, "state.9", out _));
    Assert.False(SceneAddress.TryResolveId(tree, null, out _));
  }
}
