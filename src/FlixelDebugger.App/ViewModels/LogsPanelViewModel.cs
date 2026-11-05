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
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The ViewModel of the log panel: a filtered, throttled view of the active game's
/// <see cref="LogStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>All logic is Core's.</b> The store keeps the lines, applies the level and text filters, and
/// tracks the selection. This class only copies the store's view into a list the UI can show and
/// forwards the toolbar to the store.
/// </para>
/// <para>
/// <b>Throttling.</b> Logs can arrive by the thousand per second, and rebuilding a list that often
/// would freeze the window. A timer on the UI thread (every <see cref="RefreshInterval"/>, so about
/// 10 times a second) calls <see cref="Refresh"/>, which does nothing unless the store's
/// <see cref="LogStore.Version"/> changed. The version is a single number, so checking it costs
/// nothing.
/// </para>
/// <para>
/// <b>Auto-scroll.</b> The view reports how far it is from the bottom through
/// <see cref="ReportScroll"/>. New lines are followed (<see cref="ScrollToBottomRequested"/>) only
/// while <see cref="AutoScroll"/> is on and the list was already at the bottom, so scrolling up to
/// read an old line is never interrupted.
/// </para>
/// </remarks>
public sealed partial class LogsPanelViewModel : PanelViewModel, IDisposable
{
  /// <summary>
  /// How often the list is brought up to date. Ten times a second is smooth enough to read.
  /// </summary>
  public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(100);

  /// <summary>How close to the bottom, in pixels, the list counts as "at the bottom".</summary>
  public const double BottomTolerance = 2.0;

  private readonly List<LogEntry> _view = new(LogStore.Capacity);
  private readonly Dictionary<long, LogRowViewModel> _rowCache = new(LogStore.Capacity);
  private readonly IDisposable _timer;
  private LogStore? _store;
  private long _lastVersion = -1;
  private bool _atBottom = true;
  private bool _syncing;

  [ObservableProperty]
  private bool _showDebug = true;

  [ObservableProperty]
  private bool _showInfo = true;

  [ObservableProperty]
  private bool _showWarn = true;

  [ObservableProperty]
  private bool _showError = true;

  [ObservableProperty]
  private bool _showUncaught = true;

  [ObservableProperty]
  private bool _autoScroll = true;

  [ObservableProperty]
  private string _filterText = string.Empty;

  [ObservableProperty]
  private string _selectedDetails = string.Empty;

  /// <summary>
  /// Creates the panel and starts its refresh timer.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  public LogsPanelViewModel(IActiveSession active, IUiScheduler scheduler)
      : base(BuiltInPanels.Logs, "Logs", active)
  {
    ArgumentNullException.ThrowIfNull(scheduler);
    _timer = scheduler.Every(RefreshInterval, Refresh);
    Attach(active.Current?.Logs);
  }

  /// <summary>Raised when the view should scroll to the last row.</summary>
  public event EventHandler? ScrollToBottomRequested;

  /// <summary>Gets the rows to show, oldest first.</summary>
  public RangeObservableCollection<LogRowViewModel> Rows { get; } = [];

  /// <summary>
  /// Gets whether the list was at the bottom the last time the view reported its scroll position.
  /// </summary>
  public bool IsAtBottom => _atBottom;

  /// <summary>Gets whether a line is selected.</summary>
  public bool HasSelection => _store?.SelectedSeq is not null;

  /// <summary>
  /// Tells the ViewModel how far the list is scrolled from the bottom.
  /// </summary>
  /// <remarks>
  /// The view calls this when the person scrolls (not when new rows only make the list longer).
  /// Within <see cref="BottomTolerance"/> pixels counts as the bottom.
  /// </remarks>
  /// <param name="distanceFromBottom">
  /// The distance in pixels. Zero means the last row is fully visible.
  /// </param>
  public void ReportScroll(double distanceFromBottom) =>
      _atBottom = distanceFromBottom <= BottomTolerance;

  /// <summary>
  /// Brings the list up to date with the store if anything changed since the last call.
  /// </summary>
  /// <remarks>
  /// The timer calls this about 10 times a second, and a click on a row calls it right away.
  /// </remarks>
  public void Refresh()
  {
    var store = _store;
    if (store is null)
    {
      return;
    }

    var version = store.Version;
    if (version == _lastVersion)
    {
      return;
    }

    _lastVersion = version;
    var follow = AutoScroll && _atBottom;
    store.CopyView(_view);
    var changed = ApplyView();
    SyncSelection(store);
    if (_rowCache.Count > LogStore.Capacity * 2)
    {
      // Rows of evicted lines are no longer needed.
      _rowCache.Clear();
      foreach (var row in Rows)
      {
        _rowCache[row.Seq] = row;
      }
    }

    if (follow && changed)
    {
      ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
    }
  }

  /// <summary>
  /// Selects a row, or clears the selection when that row is already selected.
  /// </summary>
  /// <param name="row">The row that was clicked.</param>
  public void ToggleSelect(LogRowViewModel row)
  {
    ArgumentNullException.ThrowIfNull(row);
    var store = _store;
    if (store is null)
    {
      return;
    }

    if (store.SelectedSeq == row.Seq)
    {
      store.ClearSelection();
    }
    else
    {
      store.Select(row.Seq);
    }

    Refresh();
  }

  /// <summary>
  /// Gets the text to copy for "Copy line".
  /// </summary>
  /// <param name="row">The row, or <see langword="null"/> to use the selected line.</param>
  /// <returns>
  /// The one-line summary, or <see langword="null"/> when there is nothing to copy.
  /// </returns>
  public string? GetLineText(LogRowViewModel? row) =>
      row?.Summary ?? _store?.SelectedEntry?.Summary;

  /// <summary>
  /// Gets the text to copy for "Copy details".
  /// </summary>
  /// <param name="row">The row, or <see langword="null"/> to use the selected line.</param>
  /// <returns>
  /// The full text including the stack trace, or <see langword="null"/> when there is nothing to
  /// copy.
  /// </returns>
  public string? GetDetailsText(LogRowViewModel? row) =>
      row is not null ? LogStore.FormatDetails(row.Entry.Line) : _store?.SelectedDetails;

  /// <summary>
  /// Stops the refresh timer.
  /// </summary>
  public void Dispose() => _timer.Dispose();

  /// <inheritdoc/>
  protected override void OnSessionChanged(IGameSession? oldSession, IGameSession? newSession) =>
      Attach(newSession?.Logs);

  partial void OnShowDebugChanged(bool value) => SetLevel(LogLevel.Debug, value);

  partial void OnShowInfoChanged(bool value) => SetLevel(LogLevel.Info, value);

  partial void OnShowWarnChanged(bool value) => SetLevel(LogLevel.Warn, value);

  partial void OnShowErrorChanged(bool value) => SetLevel(LogLevel.Error, value);

  partial void OnShowUncaughtChanged(bool value)
  {
    if (_store is { } store && !_syncing)
    {
      store.ShowUncaught = value;
    }
  }

  partial void OnFilterTextChanged(string value)
  {
    if (_store is { } store)
    {
      store.Search = value;
    }
  }

  partial void OnAutoScrollChanged(bool value)
  {
    if (value)
    {
      _atBottom = true;
      ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
    }
  }

  [RelayCommand]
  private async Task ClearAsync()
  {
    if (Session is { } session)
    {
      await session.ClearLogsAsync();
    }
  }

  private void SetLevel(LogLevel level, bool shown)
  {
    if (_store is { } store && !_syncing)
    {
      store.SetLevelShown(level, shown);
    }
  }

  // Points the panel at another game's store and pushes the toolbar state into it.
  private void Attach(LogStore? store)
  {
    _store = store;
    _lastVersion = -1;
    _rowCache.Clear();
    _atBottom = true;

    // Another game's lines can reuse the same sequence numbers, so never compare them with old
    // rows.
    Rows.ReplaceAll([]);
    SelectedDetails = string.Empty;
    if (store is not null)
    {
      _syncing = true;
      store.SetLevelShown(LogLevel.Debug, ShowDebug);
      store.SetLevelShown(LogLevel.Info, ShowInfo);
      store.SetLevelShown(LogLevel.Warn, ShowWarn);
      store.SetLevelShown(LogLevel.Error, ShowError);
      store.ShowUncaught = ShowUncaught;
      store.Search = FilterText;
      _syncing = false;
    }

    OnPropertyChanged(nameof(HasSelection));
    Refresh();
  }

  // Updates Rows from _view with as few changes as possible, so the list keeps its scroll position.
  // Returns whether anything changed.
  private bool ApplyView()
  {
    var changed = false;

    // Lines evicted from the front of the store disappear from the front of the list.
    if (Rows.Count > 0 && _view.Count > 0 && Rows[0].Seq != _view[0].Seq)
    {
      var first = _view[0].Seq;
      var skip = 0;
      while (skip < Rows.Count && Rows[skip].Seq != first)
      {
        skip++;
      }

      if (skip < Rows.Count)
      {
        for (var i = 0; i < skip; i++)
        {
          Rows.RemoveAt(0);
        }

        changed = true;
      }
    }

    var common = Math.Min(Rows.Count, _view.Count);
    for (var i = 0; i < common; i++)
    {
      if (Rows[i].Seq != _view[i].Seq)
      {
        // A filter changed, so the rows no longer line up. Rebuild them in one step.
        ReplaceAllRows();
        return true;
      }
    }

    while (Rows.Count > _view.Count)
    {
      Rows.RemoveAt(Rows.Count - 1);
      changed = true;
    }

    for (var i = Rows.Count; i < _view.Count; i++)
    {
      Rows.Add(RowFor(_view[i]));
      changed = true;
    }

    return changed;
  }

  private void ReplaceAllRows()
  {
    var rows = new LogRowViewModel[_view.Count];
    for (var i = 0; i < rows.Length; i++)
    {
      rows[i] = RowFor(_view[i]);
    }

    Rows.ReplaceAll(rows);

    // Forget rows that left the view so the cache cannot grow.
    _rowCache.Clear();
    foreach (var row in rows)
    {
      _rowCache[row.Seq] = row;
    }
  }

  private LogRowViewModel RowFor(LogEntry entry)
  {
    if (!_rowCache.TryGetValue(entry.Seq, out var row))
    {
      row = new LogRowViewModel(entry);
      _rowCache[entry.Seq] = row;
    }

    return row;
  }

  private void SyncSelection(LogStore store)
  {
    var selected = store.SelectedSeq;
    foreach (var row in Rows)
    {
      row.IsSelected = row.Seq == selected;
    }

    SelectedDetails = store.SelectedDetails ?? string.Empty;
    OnPropertyChanged(nameof(HasSelection));
  }
}
