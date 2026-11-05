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

using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One collapsible tracker: a name and a table of its values.
/// </summary>
public sealed partial class TrackerSectionViewModel : ObservableObject
{
  [ObservableProperty]
  private bool _isExpanded = true;

  /// <summary>
  /// Creates a tracker section.
  /// </summary>
  /// <param name="name">The tracker name.</param>
  public TrackerSectionViewModel(string name)
  {
    Name = name;
  }

  /// <summary>Gets the tracker name.</summary>
  public string Name { get; }

  /// <summary>Gets the rows in the order the game sent them.</summary>
  public RangeObservableCollection<ValueRowViewModel> Rows { get; } = [];
}

/// <summary>
/// The ViewModel of the trackers panel: one collapsible section per tracker.
/// </summary>
/// <remarks>
/// A tracker that disappears is only hidden. Its section object stays in memory, so when the
/// tracker comes back it is as open or closed as the person left it.
/// </remarks>
public sealed class TrackersPanelViewModel : LivePanelViewModel
{
  /// <summary>The hint shown while the game has no trackers.</summary>
  public const string EmptyHint =
      "No trackers yet. Track an object in the game and it shows up here.";

  private readonly Dictionary<string, TrackerSectionViewModel> _known = new(StringComparer.Ordinal);
  private TrackersPayload? _last;
  private bool _built;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  public TrackersPanelViewModel(IActiveSession active, IUiScheduler scheduler)
      : base(BuiltInPanels.Trackers, "Trackers", active, scheduler)
  {
    Refresh();
  }

  /// <summary>Gets the trackers the game has right now, in payload order.</summary>
  public RangeObservableCollection<TrackerSectionViewModel> Sections { get; } = [];

  /// <summary>Gets whether no tracker is shown.</summary>
  public bool IsEmpty => Sections.Count == 0;

  /// <inheritdoc/>
  public override void Refresh()
  {
    var payload = Session?.Trackers;
    if (_built && ReferenceEquals(payload, _last))
    {
      return;
    }

    _built = true;
    _last = payload;
    var blocks = payload?.Blocks ?? [];
    var visible = new List<TrackerSectionViewModel>(blocks.Count);
    var seen = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var block in blocks)
    {
      // Two trackers may share a name, so the key also counts how often the name was seen in this
      // payload.
      seen.TryGetValue(block.Name, out var occurrence);
      seen[block.Name] = occurrence + 1;
      var key = occurrence == 0 ? block.Name : $"{block.Name}\0{occurrence}";
      if (!_known.TryGetValue(key, out var section))
      {
        section = new TrackerSectionViewModel(block.Name);
        _known[key] = section;
      }

      ValueRowViewModel.Sync(section.Rows, block.Rows);
      visible.Add(section);
    }

    if (!visible.SequenceEqual(Sections))
    {
      Sections.ReplaceAll(visible);
      OnPropertyChanged(nameof(IsEmpty));
    }
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    // Another game has other trackers, so forget the old sections.
    _known.Clear();
    _built = false;
  }
}
