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
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// A titled group of rows in the stats panel.
/// </summary>
/// <param name="Title">The group name.</param>
/// <param name="Rows">The rows of the group.</param>
public sealed record StatsGroupViewModel(string Title, IReadOnlyList<ValueRowViewModel> Rows);

/// <summary>
/// The ViewModel of the stats panel: a two-column list of the newest sample's numbers.
/// </summary>
/// <remarks>
/// <see cref="StatsTable"/> in Core decides the rows and how every number is formatted. This class
/// owns one row ViewModel per entry and updates them in place when a new sample (or a new ping)
/// arrives, so the list never flickers or rebuilds.
/// </remarks>
public sealed class StatsPanelViewModel : LivePanelViewModel
{
  private readonly ValueRowViewModel[] _rows = new ValueRowViewModel[StatsTable.RowCount];
  private StatsPayload? _lastSample;
  private double? _lastPing;
  private bool _built;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  public StatsPanelViewModel(IActiveSession active, IUiScheduler scheduler)
      : base(BuiltInPanels.Stats, "Stats", active, scheduler)
  {
    for (var i = 0; i < _rows.Length; i++)
    {
      _rows[i] = new ValueRowViewModel();
    }

    Groups =
    [
        new StatsGroupViewModel("Client", _rows[..8]),
        new StatsGroupViewModel("World", _rows[8..10]),
        new StatsGroupViewModel("Camera", _rows[10..]),
    ];
    Refresh();
  }

  /// <summary>Gets the groups to show: Client, World, and Camera.</summary>
  public IReadOnlyList<StatsGroupViewModel> Groups { get; }

  /// <inheritdoc/>
  public override void Refresh()
  {
    var sample = Session?.Stats.LatestPayload;
    var ping = Session?.PingMs;
    if (_built && ReferenceEquals(sample, _lastSample) && ping == _lastPing)
    {
      return;
    }

    _built = true;
    _lastSample = sample;
    _lastPing = ping;
    var entries = StatsTable.Build(sample, ping);
    for (var i = 0; i < entries.Length; i++)
    {
      _rows[i].Set(entries[i]);
    }
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession) =>
      _built = false;
}
