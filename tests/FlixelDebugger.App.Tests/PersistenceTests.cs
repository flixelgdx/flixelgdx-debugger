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
using FlixelDebugger.Core.Settings;

namespace FlixelDebugger.App.Tests;

public class PersistenceTests
{
  [Fact]
  public void SavingWritesTheLayoutAndTheVisibilityOfEveryPanel()
  {
    var h = new Harness();
    var main = h.Main;
    h.Layout.SetVisible("scene", false);

    main.SaveLayout();

    var settings = h.Settings.Current;
    Assert.False(string.IsNullOrWhiteSpace(settings.WindowLayout));
    Assert.Equal(h.Registry.Definitions.Count, settings.PanelVisibility.Count);
    Assert.False(settings.PanelVisibility["scene"]);
    Assert.True(settings.PanelVisibility["logs"]);
    Assert.Equal(1, h.Settings.Saves);
  }

  [Fact]
  public void RestoringBringsBackTheSavedLayoutAndVisibilityOnTheNextLaunch()
  {
    var first = new Harness();
    first.Layout.SetVisible("inspector", false);
    first.Layout.SetVisible("stats", false);
    first.Main.SaveLayout();

    var second = new Harness();
    second.Settings.Current.WindowLayout = first.Settings.Current.WindowLayout;
    foreach (var (id, visible) in first.Settings.Current.PanelVisibility)
    {
      second.Settings.Current.PanelVisibility[id] = visible;
    }

    second.Main.RestoreLayout();

    Assert.False(second.Layout.IsVisible("inspector"));
    Assert.False(second.Layout.IsVisible("stats"));
    Assert.True(second.Layout.IsVisible("scene"));
    Assert.False(second.Main.PanelItems.Single(i => i.Id == "inspector").IsVisible);
    Assert.True(second.Main.PanelItems.Single(i => i.Id == "scene").IsVisible);
  }

  [Fact]
  public void SavedVisibilityStillAppliesWhenTheLayoutTextIsMissingOrBroken()
  {
    var h = new Harness();
    h.Settings.Current.WindowLayout = "{ broken";
    h.Settings.Current.PanelVisibility["trackers"] = false;

    h.Main.RestoreLayout();

    Assert.False(h.Layout.IsVisible("trackers"));
    Assert.True(h.Layout.IsVisible("watches"));
  }

  [Fact]
  public void ResetLayoutRestoresTheDefaultArrangement()
  {
    var h = new Harness();
    var main = h.Main;
    h.Layout.SetVisible("controls", false);
    Assert.False(main.PanelItems.Single(i => i.Id == "controls").IsVisible);

    main.ResetLayoutCommand.Execute(null);

    Assert.True(h.Layout.IsVisible("controls"));
    Assert.True(main.PanelItems.Single(i => i.Id == "controls").IsVisible);
    Assert.Same(h.Layout.Root, main.Layout);
  }

  [Fact]
  public void TheDebugMenuChecksShowAndHidePanels()
  {
    var h = new Harness();
    var item = h.Main.PanelItems.Single(i => i.Id == "logs");
    Assert.True(item.IsVisible);

    item.IsVisible = false;
    Assert.False(h.Layout.IsVisible("logs"));

    item.IsVisible = true;
    Assert.True(h.Layout.IsVisible("logs"));
  }

  [Fact]
  public void ClosingATabUncheckstheMenuItem()
  {
    var h = new Harness();
    var item = h.Main.PanelItems.Single(i => i.Id == "logs");

    h.Layout.SetVisible("logs", false);

    Assert.False(item.IsVisible);
  }

  [Fact]
  public async Task ShuttingDownSavesTheLayoutAndStopsTheServer()
  {
    var h = new Harness();
    await h.Main.InitializeAsync();

    await h.Main.ShutdownAsync();

    Assert.False(h.Host.IsRunning);
    Assert.False(string.IsNullOrWhiteSpace(h.Settings.Current.WindowLayout));
  }

  [Fact]
  public void MenuItemsExistForEveryPanelInOrder()
  {
    var h = new Harness();

    Assert.Equal(h.Registry.Definitions.Select(d => d.Id), h.Main.PanelItems.Select(i => i.Id));
    Assert.All(h.Main.PanelItems, i => Assert.Equal(h.Layout.IsVisible(i.Id), i.IsVisible));
  }

  [Fact]
  public void AppSettingsKeepsUnknownPanelVisibilityEntries()
  {
    // The settings object is plain data, so ids of panels this build does not know are simply ignored when restoring.
    var h = new Harness();
    h.Settings.Current.PanelVisibility["removed.panel"] = false;
    var settings = h.Settings.Current;

    h.Main.RestoreLayout();

    Assert.IsType<AppSettings>(settings);
    Assert.False(h.Layout.IsVisible("removed.panel"));
  }
}
