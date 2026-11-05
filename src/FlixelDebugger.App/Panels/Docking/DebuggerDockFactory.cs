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

using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using FlixelDebugger.Core.Sessions;

namespace FlixelDebugger.App.Panels.Docking;

/// <summary>
/// Builds the default arrangement of the debugger's panels.
/// </summary>
/// <remarks>
/// <para>
/// The default layout is:
/// </para>
/// <list type="bullet">
/// <item><description>
/// left column: Stats, Performance, and Logs (which takes the rest of the height);
/// </description></item>
/// <item><description>
/// middle: Scene, Viewport, Inspector, Textures, and Save as tabs of one group, plus panels games
/// add;
/// </description></item>
/// <item><description>right column: Watches, Controls, and Trackers;</description></item>
/// <item><description>bottom, across the full width: Console.</description></item>
/// </list>
/// <para>
/// Closing a tab hides the panel instead of destroying it, so the Debug menu can bring it back.
/// </para>
/// </remarks>
public sealed class DebuggerDockFactory : Factory
{
  /// <summary>
  /// The id of the tab group in the middle, where panels that games add are placed.
  /// </summary>
  public const string MiddleDockId = "middle";

  private readonly IPanelRegistry _registry;

  /// <summary>
  /// Creates a factory.
  /// </summary>
  /// <param name="registry">Tells which panels exist and provides their ViewModels.</param>
  public DebuggerDockFactory(IPanelRegistry registry)
  {
    ArgumentNullException.ThrowIfNull(registry);
    _registry = registry;
    HideToolsOnClose = true;
  }

  /// <summary>
  /// Creates the dock item for a registered panel.
  /// </summary>
  /// <param name="id">The panel id.</param>
  /// <returns>A new tool whose context is the panel's ViewModel.</returns>
  /// <exception cref="KeyNotFoundException">No panel has that id.</exception>
  public PanelTool CreateTool(string id)
  {
    if (!_registry.TryGet(id, out var definition))
    {
      throw new KeyNotFoundException($"No panel is registered with the id '{id}'.");
    }

    return new PanelTool
    {
      Id = definition.Id,
      Title = definition.Title,
      Context = _registry.GetViewModel(definition.Id),
      CanClose = true,
      CanPin = false,
      CanFloat = false,
    };
  }

  /// <inheritdoc/>
  public override IRootDock CreateLayout()
  {
    var left = Column(
        0.3,
        Group(0.13, BuiltInPanels.Stats),
        Group(0.17, DefaultPanels.Performance),
        Group(double.NaN, BuiltInPanels.Logs));

    var middle = Group(
        double.NaN,
        BuiltInPanels.Scene,
        BuiltInPanels.Viewport,
        BuiltInPanels.Inspector,
        BuiltInPanels.Textures,
        BuiltInPanels.Save);
    middle.Id = MiddleDockId;

    var right = Column(
        0.22,
        Group(0.34, BuiltInPanels.Watches),
        Group(0.33, BuiltInPanels.Controls),
        Group(double.NaN, BuiltInPanels.Trackers));

    var top = new ProportionalDock
    {
      Orientation = Orientation.Horizontal,
      VisibleDockables = CreateList<IDockable>(
          left,
          new ProportionalDockSplitter(),
          middle,
          new ProportionalDockSplitter(),
          right),
    };

    var main = new ProportionalDock
    {
      Id = "main",
      Orientation = Orientation.Vertical,
      VisibleDockables = CreateList<IDockable>(
          top,
          new ProportionalDockSplitter(),
          Group(0.2, BuiltInPanels.Console)),
    };

    var root = CreateRootDock();
    root.Id = "root";
    root.IsCollapsable = false;
    root.VisibleDockables = CreateList<IDockable>(main);
    root.ActiveDockable = main;
    root.DefaultDockable = main;
    root.HiddenDockables = CreateList<IDockable>();
    return root;
  }

  /// <inheritdoc/>
  public override void InitLayout(IDockable layout)
  {
    HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
    {
      [nameof(IDockWindow)] = () => new HostWindow(),
    };

    base.InitLayout(layout);
  }

  private ProportionalDock Column(double proportion, params IToolDock[] groups)
  {
    var items = new List<IDockable>();
    foreach (var group in groups)
    {
      if (items.Count > 0)
      {
        items.Add(new ProportionalDockSplitter());
      }

      items.Add(group);
    }

    return new ProportionalDock
    {
      Proportion = proportion,
      Orientation = Orientation.Vertical,
      VisibleDockables = CreateList([.. items]),
    };
  }

  private ToolDock Group(double proportion, params string[] ids)
  {
    var tools = new List<IDockable>();
    foreach (var id in ids)
    {
      tools.Add(CreateTool(id));
    }

    return new ToolDock
    {
      Proportion = proportion,
      ActiveDockable = tools[0],
      VisibleDockables = CreateList([.. tools]),
    };
  }
}
