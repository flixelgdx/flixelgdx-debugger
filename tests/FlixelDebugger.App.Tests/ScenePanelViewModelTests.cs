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
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class ScenePanelViewModelTests
{
  private static SceneNodeViewModel Find(ScenePanelViewModel vm, int id)
  {
    var stack = new Stack<SceneNodeViewModel>(vm.Roots);
    while (stack.Count > 0)
    {
      var node = stack.Pop();
      if (node.Id == id)
      {
        return node;
      }

      foreach (var child in node.Children)
      {
        stack.Push(child);
      }
    }

    throw new InvalidOperationException($"No node {id}.");
  }

  [Fact]
  public void BuildsTheTreeWithTypeAndLabelAndMarksTrackedRoots()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();

    var vm = f.Scene();

    Assert.False(vm.IsEmpty);
    Assert.Equal([1, 9], vm.Roots.Select(n => n.Id));
    Assert.Equal([2, 3], vm.Roots[0].Children.Select(n => n.Id));
    Assert.Equal([4], Find(vm, 2).Children.Select(n => n.Id));
    Assert.Equal("State state", vm.Roots[0].Text);
    Assert.Equal("Sprite", Find(vm, 4).Type);
    Assert.Equal("0", Find(vm, 4).Label);
    Assert.True(Find(vm, 9).IsTrackedRoot);
    Assert.False(Find(vm, 1).IsTrackedRoot);
  }

  [Fact]
  public void AnEmptySceneShowsAnEmptyTree()
  {
    var vm = new PanelFixture().Scene();

    Assert.True(vm.IsEmpty);
  }

  [Fact]
  public void RootsStartOpenAndEverythingElseStartsClosed()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();

    var vm = f.Scene();

    Assert.True(Find(vm, 1).IsExpanded);
    Assert.False(Find(vm, 2).IsExpanded);
  }

  [Fact]
  public void ExpandedNodesStayExpandedWhenTheTreeIsRefreshed()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();
    var group = Find(vm, 2);
    group.IsExpanded = true;
    Find(vm, 1).IsExpanded = false;

    f.Game.Scene = new SceneTree(
        [.. TestData.Tree().Nodes, new SceneNode(5, 2, "Sprite", "1", false)]);
    f.Tick();

    Assert.Same(group, Find(vm, 2));
    Assert.True(group.IsExpanded);
    Assert.False(Find(vm, 1).IsExpanded);
    Assert.Equal([4, 5], group.Children.Select(n => n.Id));
  }

  [Fact]
  public void ANodeThatLeavesAndComesBackIsExpandedAgain()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();
    Find(vm, 2).IsExpanded = true;

    f.Game.Scene = new SceneTree([new SceneNode(1, -1, "State", "state", false)]);
    f.Tick();
    Assert.Empty(vm.Roots[0].Children);

    f.Game.Scene = TestData.Tree();
    f.Tick();

    Assert.True(Find(vm, 2).IsExpanded);
  }

  [Fact]
  public void UntouchedBranchesKeepTheirObjectsAndChildLists()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();
    var changes = 0;
    var manager = Find(vm, 9);
    manager.Children.CollectionChanged += (_, _) => changes++;

    f.Game.Scene = TestData.Tree();
    f.Tick();

    Assert.Same(manager, Find(vm, 9));
    Assert.Equal(0, changes);
  }

  [Fact]
  public void ClickingANodeSelectsItInTheGame()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();

    vm.SelectedNode = Find(vm, 3);

    Assert.Equal(["select:3"], f.Game.Actions);
    Assert.True(f.Origin.TryConsume(3));
  }

  [Fact]
  public void ASelectionFromTheGameIsRevealedWithoutSendingAnything()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();
    Assert.False(Find(vm, 2).IsExpanded);

    f.Game.Inspector = TestData.Inspector(4);
    f.Tick();

    Assert.Same(Find(vm, 4), vm.SelectedNode);
    Assert.True(Find(vm, 2).IsExpanded);
    Assert.True(Find(vm, 1).IsExpanded);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void ASelectionForANodeThatIsNotThereYetWaitsForTheNextSnapshot()
  {
    var f = new PanelFixture();
    var vm = f.Scene();

    f.Game.Inspector = TestData.Inspector(4);
    f.Tick();
    Assert.Null(vm.SelectedNode);

    f.Game.Scene = TestData.Tree();
    f.Tick();

    Assert.Equal(4, vm.SelectedNode?.Id);
  }

  [Fact]
  public void ClearingTheSelectionInTheGameClearsTheTreeSelection()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    f.Game.Inspector = TestData.Inspector(3);
    var vm = f.Scene();
    Assert.Equal(3, vm.SelectedNode?.Id);

    f.Game.Inspector = TestData.Inspector(-1, string.Empty);
    f.Tick();

    Assert.Null(vm.SelectedNode);
    Assert.Empty(f.Game.Actions);
  }

  [Fact]
  public void ARemovedSelectedNodeIsDeselected()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();
    vm.SelectedNode = Find(vm, 3);

    f.Game.Scene = new SceneTree([new SceneNode(1, -1, "State", "state", false)]);
    f.Tick();

    Assert.Null(vm.SelectedNode);
  }

  [Fact]
  public void RefreshAsksTheGameForAFreshTree()
  {
    var f = new PanelFixture();
    var vm = f.Scene();

    vm.RefreshTreeCommand.Execute(null);

    Assert.Equal(["requestTree"], f.Game.Actions);
  }

  [Fact]
  public void SwitchingTheGameShowsTheOtherTree()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    var vm = f.Scene();

    f.Active.Current = new FakeGameSession("Other");

    Assert.True(vm.IsEmpty);
    Assert.Empty(vm.Roots);
  }
}
