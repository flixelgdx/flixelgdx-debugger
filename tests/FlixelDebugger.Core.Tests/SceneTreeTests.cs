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
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Core.Tests;

public class SceneTreeTests
{
  private static SceneTree Sample() => new(
  [
      new SceneNode(1, -1, "State", "state", false),
      new SceneNode(2, 1, "Group", "0", false),
      new SceneNode(3, 1, "Sprite", "1", false),
      new SceneNode(4, 2, "Sprite", "0", false),
      new SceneNode(9, -1, "Manager", "managers", true),
  ]);

  [Fact]
  public void RootsAreNodesWithoutAParent()
  {
    Assert.Equal([1, 9], Sample().Roots.Select(n => n.Id));
  }

  [Fact]
  public void ChildrenKeepTheOrderTheGameSentThem()
  {
    var tree = Sample();
    Assert.Equal([2, 3], tree.GetChildren(1).Select(n => n.Id));
    Assert.Equal([4], tree.GetChildren(2).Select(n => n.Id));
    Assert.Empty(tree.GetChildren(4));
    Assert.Empty(tree.GetChildren(12345));
  }

  [Fact]
  public void FindsParentsAndNodesById()
  {
    var tree = Sample();
    Assert.True(tree.TryGetParent(4, out var parent));
    Assert.Equal(2, parent.Id);
    Assert.False(tree.TryGetParent(1, out _));
    Assert.False(tree.TryGetParent(777, out _));
    Assert.True(tree.TryGetNode(9, out var node));
    Assert.Equal("managers", node.Label);
    Assert.False(tree.TryGetNode(777, out _));
  }

  [Fact]
  public void ANodeWhoseParentIsMissingIsTreatedAsARoot()
  {
    var tree = new SceneTree([new SceneNode(5, 99, "Orphan", "x", false)]);
    Assert.Single(tree.Roots);
    Assert.False(tree.TryGetParent(5, out _));
  }

  [Fact]
  public void AnEmptyTreeHasNothing()
  {
    Assert.Empty(SceneTree.Empty.Nodes);
    Assert.Empty(SceneTree.Empty.Roots);
  }
}
