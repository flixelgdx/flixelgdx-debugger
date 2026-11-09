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

using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Tests;

public class PanelRegistryTests
{
  private static PanelDefinition Define(string id, string title = "T", bool shown = true) =>
      new(id, title, PanelDock.Center, shown);

  [Fact]
  public void TheRegistryStartsEmptyBecauseTheGameBuildsEveryPanel()
  {
    var h = new Harness(standardPanels: false);
    Assert.Empty(h.Registry.Definitions);
  }

  [Fact]
  public void AConnectedGameRegistersItsPanelsInListOrder()
  {
    var h = new Harness(standardPanels: false);
    _ = h.Main;
    h.Connect();

    Assert.Equal(
        TestData.StandardPanels.Select(p => p.Id), h.Registry.Definitions.Select(d => d.Id));
    Assert.All(
        h.Registry.Definitions,
        d => Assert.IsType<GamePanelViewModel>(h.Registry.GetViewModel(d.Id)));
  }

  [Fact]
  public void ViewModelsAreCreatedOnceAndShared()
  {
    var h = new Harness();
    Assert.Same(h.Registry.GetViewModel("stats"), h.Registry.GetViewModel("stats"));
    Assert.Throws<KeyNotFoundException>(() => h.Registry.GetViewModel("nope"));
  }

  [Fact]
  public void RegisteringAnExistingIdUpdatesItAndKeepsItsViewModel()
  {
    var h = new Harness(standardPanels: false);
    var changes = 0;
    h.Registry.Changed += (_, _) => changes++;

    Assert.True(h.Registry.Register(Define("a", "Old")));
    var viewModel = h.Registry.GetViewModel("a");
    Assert.False(h.Registry.Register(Define("a", "Old")));
    Assert.False(h.Registry.Register(Define("a", "New")));

    Assert.Equal(2, changes);
    Assert.Same(viewModel, h.Registry.GetViewModel("a"));
    Assert.Equal("New", viewModel.Title);
    Assert.True(h.Registry.TryGet("a", out var definition));
    Assert.Equal("New", definition.Title);
  }

  [Fact]
  public void RegisterAllRaisesChangedOnceForAWholeList()
  {
    var h = new Harness(standardPanels: false);
    var changes = 0;
    h.Registry.Changed += (_, _) => changes++;

    h.Registry.RegisterAll([Define("a"), Define("b"), Define("c")]);
    h.Registry.RegisterAll([Define("a"), Define("b")]);

    Assert.Equal(1, changes);
    Assert.Equal(["a", "b", "c"], h.Registry.Definitions.Select(d => d.Id));
  }
}
