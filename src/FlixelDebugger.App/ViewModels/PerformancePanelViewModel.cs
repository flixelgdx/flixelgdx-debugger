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

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Graphs;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One collapsible section of the performance panel. Its graphs exist only while it is expanded.
/// </summary>
public sealed partial class PerformanceSectionViewModel : ObservableObject
{
  private readonly GraphSection _spec;
  private IGameSession? _session;

  [ObservableProperty]
  private bool _isExpanded;

  /// <summary>
  /// Creates a section. It starts expanded or collapsed as its description says.
  /// </summary>
  /// <param name="spec">Describes the section and its graphs.</param>
  public PerformanceSectionViewModel(GraphSection spec)
  {
    ArgumentNullException.ThrowIfNull(spec);
    _spec = spec;
    IsExpanded = spec.ExpandedByDefault;
  }

  /// <summary>Gets the section name.</summary>
  public string Title => _spec.Title;

  /// <summary>Gets the graphs, which is empty while the section is collapsed.</summary>
  public ObservableCollection<GraphViewModel> Graphs { get; } = [];

  /// <summary>
  /// Points the section at a game.
  /// </summary>
  /// <param name="session">The game to draw, or <see langword="null"/> for none.</param>
  public void Bind(IGameSession? session)
  {
    _session = session;
    foreach (var graph in Graphs)
    {
      graph.Bind(session);
    }
  }

  /// <summary>
  /// Updates every graph. A collapsed section has none, so this costs nothing for it.
  /// </summary>
  public void Update()
  {
    foreach (var graph in Graphs)
    {
      graph.Update();
    }
  }

  partial void OnIsExpandedChanged(bool value)
  {
    Graphs.Clear();
    if (!value)
    {
      return;
    }

    foreach (var spec in _spec.Graphs)
    {
      var graph = new GraphViewModel(spec);
      graph.Bind(_session);
      Graphs.Add(graph);
    }
  }
}

/// <summary>
/// The ViewModel of the performance panel: the sample rate, the address games connect to, and the graph sections.
/// </summary>
/// <remarks>
/// <see cref="GraphCatalog"/> in Core lists the sections and graphs. The panel refreshes only the graphs of expanded
/// sections, about 10 times a second.
/// </remarks>
public sealed partial class PerformancePanelViewModel : LivePanelViewModel
{
  private readonly IDebugHost _host;

  [ObservableProperty]
  private string _sampleRateText = "-";

  [ObservableProperty]
  private string _listenAddress = string.Empty;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  /// <param name="host">Tells the address games connect to.</param>
  public PerformancePanelViewModel(IActiveSession active, IUiScheduler scheduler, IDebugHost host)
      : base(DefaultPanels.Performance, "Performance", active, scheduler)
  {
    ArgumentNullException.ThrowIfNull(host);
    _host = host;
    foreach (var spec in GraphCatalog.Sections)
    {
      var section = new PerformanceSectionViewModel(spec);
      section.Bind(active.Current);
      Sections.Add(section);
    }

    Refresh();
  }

  /// <summary>Gets the sections: Core, Render, Memory, and Network.</summary>
  public ObservableCollection<PerformanceSectionViewModel> Sections { get; } = [];

  /// <inheritdoc/>
  public override void Refresh()
  {
    var rate = Session?.Controls?.Rate;
    SampleRateText = rate is { } hz ? string.Create(CultureInfo.InvariantCulture, $"{hz} Hz") : "-";
    ListenAddress = _host.IsRunning ? _host.ListenAddress : "Server stopped";
    foreach (var section in Sections)
    {
      section.Update();
    }
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    foreach (var section in Sections)
    {
      section.Bind(newSession);
    }
  }
}
