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

using FlixelDebugger.App.Services;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class AgentActivityTests
{
  [Fact]
  public void AToolCallMakesTheAgentActiveUntilItIsIdleForThreeSeconds()
  {
    var f = new PanelFixture();
    var agent = f.Agent;
    var changes = 0;
    agent.Changed += (_, _) => changes++;
    var game = Guid.NewGuid();

    f.Mcp.Raise("inspect", game, objectId: 7, client: "Claude Code");

    Assert.True(agent.IsActive);
    Assert.Equal("Claude Code", agent.Client);
    Assert.Equal("inspect", agent.Tool);
    Assert.Equal("Agent: Claude Code - inspect", agent.BadgeText);
    Assert.Equal(game, agent.SessionId);
    Assert.Equal(7, agent.ObjectId);
    Assert.Equal(1, changes);

    f.Clock.Advance(TimeSpan.FromMilliseconds(2900));
    f.Scheduler.Tick();
    Assert.True(agent.IsActive);

    f.Clock.Advance(TimeSpan.FromMilliseconds(200));
    f.Scheduler.Tick();
    Assert.False(agent.IsActive);
    Assert.Equal(string.Empty, agent.BadgeText);
    Assert.Equal(-1, agent.ObjectId);
    Assert.Null(agent.SessionId);
    Assert.Equal(2, changes);
  }

  [Fact]
  public void EachCallRestartsTheIdleTimerAndKeepsTheLastTouchedObject()
  {
    var f = new PanelFixture();
    var agent = f.Agent;
    f.Mcp.Raise("inspect", objectId: 7);
    f.Clock.Advance(TimeSpan.FromSeconds(2));
    f.Mcp.Raise("get_stats");
    f.Clock.Advance(TimeSpan.FromSeconds(2));
    f.Scheduler.Tick();

    Assert.True(agent.IsActive);
    Assert.Equal("get_stats", agent.Tool);
    Assert.Equal(7, agent.ObjectId);
  }

  [Fact]
  public void AnInjectedPointerIsMarkedBrieflyAndTheGamePositionIsKept()
  {
    var f = new PanelFixture();
    var agent = f.Agent;

    f.Mcp.Raise("click", viewX: 12.5f, viewY: 30f);
    Assert.True(agent.HasPointer);
    Assert.Equal(12.5f, agent.PointerX);
    Assert.Equal(30f, agent.PointerY);

    f.Clock.Advance(TimeSpan.FromMilliseconds(1600));
    f.Scheduler.Tick();

    Assert.False(agent.HasPointer);
    Assert.True(agent.IsActive);
  }

  [Fact]
  public void ResetEndsTheActivityAtOnce()
  {
    var f = new PanelFixture();
    var agent = f.Agent;
    f.Mcp.Raise("inspect", objectId: 3);

    agent.Reset();

    Assert.False(agent.IsActive);
    Assert.Equal(-1, agent.ObjectId);
  }

  [Fact]
  public void EventsFromABackgroundThreadWaitForTheUiThread()
  {
    var f = new PanelFixture();
    f.Scheduler.Deferred = true;
    var agent = f.Agent;

    f.Mcp.Raise("get_logs");
    Assert.False(agent.IsActive);

    f.Scheduler.Flush();
    Assert.True(agent.IsActive);
  }

  [Fact]
  public void RecentCallsAreListedNewestFirstAndCapped()
  {
    var f = new PanelFixture();
    var agent = f.Agent;
    for (var i = 0; i < 12; i++)
    {
      f.Mcp.Raise($"tool{i}");
    }

    var lines = agent.RecentText.Split('\n');

    Assert.Equal(AgentActivityMonitor.RecentCapacity, lines.Length);
    Assert.EndsWith("Test Agent: tool11", lines[0], StringComparison.Ordinal);
    Assert.EndsWith("Test Agent: tool2", lines[^1], StringComparison.Ordinal);
  }

  [Fact]
  public void IsDrivingOnlyMatchesTheGameTheAgentNamed()
  {
    var f = new PanelFixture();
    var agent = f.Agent;
    var game = Guid.NewGuid();
    Assert.False(agent.IsDriving(game));

    f.Mcp.Raise("inspect", game);

    Assert.True(agent.IsDriving(game));
    Assert.False(agent.IsDriving(Guid.NewGuid()));
  }

  [Fact]
  public void TheViewportOutlinesTheObjectTheAgentTouchedAndMarksItsPointer()
  {
    var f = new PanelFixture();
    f.Game.Viewport = TestData.Viewport(
        TestData.Box(3, 20, 20, 16, 16, ViewportBoxState.Selected));
    var vm = f.Viewport();
    var redraws = 0;
    vm.Invalidated += (_, _) => redraws++;
    Assert.Equal(-1, vm.AgentObjectId);

    f.Mcp.Raise("click_object", f.Game.Id, objectId: 3, viewX: 5f, viewY: 6f);
    vm.Refresh();

    Assert.Equal(3, vm.AgentObjectId);
    Assert.True(vm.HasAgentPointer);
    Assert.Equal(5f, vm.AgentPointerX);
    Assert.Equal(6f, vm.AgentPointerY);
    Assert.True(redraws > 0);

    f.Clock.Advance(TimeSpan.FromSeconds(4));
    f.Scheduler.Tick();
    vm.Refresh();

    Assert.Equal(-1, vm.AgentObjectId);
    Assert.False(vm.HasAgentPointer);
  }

  [Fact]
  public void TheViewportIgnoresAnAgentThatDrivesAnotherGame()
  {
    var f = new PanelFixture();
    f.Game.Viewport = TestData.Viewport(
        TestData.Box(3, 20, 20, 16, 16, ViewportBoxState.None));
    var vm = f.Viewport();

    f.Mcp.Raise("move_object", Guid.NewGuid(), objectId: 3);
    vm.Refresh();

    Assert.Equal(-1, vm.AgentObjectId);
  }

  [Fact]
  public void TheSceneAndInspectorMarkTheObjectTheAgentTouched()
  {
    var f = new PanelFixture();
    f.Game.Scene = TestData.Tree();
    f.Game.Inspector = TestData.Inspector(
        3,
        "state.1",
        false,
        TestData.Section("Sprite", TestData.Row("x", PropertyValue.FromInt(1), true)));
    var scene = f.Scene();
    var inspector = f.Inspector();
    Assert.False(inspector.IsAgentSelection);

    f.Mcp.Raise("inspect", f.Game.Id, objectId: 3);
    scene.Refresh();
    inspector.Refresh();

    Assert.True(Find(scene, 3).IsAgentTouched);
    Assert.False(Find(scene, 4).IsAgentTouched);
    Assert.True(inspector.IsAgentSelection);

    f.Mcp.Raise("inspect", f.Game.Id, objectId: 4);
    scene.Refresh();
    inspector.Refresh();

    Assert.False(Find(scene, 3).IsAgentTouched);
    Assert.True(Find(scene, 4).IsAgentTouched);
    Assert.False(inspector.IsAgentSelection);

    f.Clock.Advance(TimeSpan.FromSeconds(4));
    f.Scheduler.Tick();
    scene.Refresh();

    Assert.False(Find(scene, 4).IsAgentTouched);
  }

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
}
