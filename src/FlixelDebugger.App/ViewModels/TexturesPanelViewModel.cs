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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One row of the textures table.
/// </summary>
public sealed class TextureRowViewModel
{
  private const double BytesPerMegabyte = 1024.0 * 1024.0;

  /// <summary>
  /// Creates a row.
  /// </summary>
  /// <param name="entry">The texture from the game.</param>
  public TextureRowViewModel(TextureEntry entry)
  {
    Entry = entry;
    SizeText = string.Create(CultureInfo.InvariantCulture, $"{entry.Width}x{entry.Height}");
    GpuMbText = (entry.GpuBytes / BytesPerMegabyte).ToString("0.00", CultureInfo.InvariantCulture);
  }

  /// <summary>Gets the texture from the game.</summary>
  public TextureEntry Entry { get; }

  /// <summary>Gets the texture id.</summary>
  public int Id => Entry.Id;

  /// <summary>Gets the asset path or name.</summary>
  public string Name => Entry.Name;

  /// <summary>Gets the size as <c>WxH</c>.</summary>
  public string SizeText { get; }

  /// <summary>Gets the format name.</summary>
  public string Format => Entry.Format;

  /// <summary>Gets the GPU memory in megabytes, with two decimals.</summary>
  public string GpuMbText { get; }

  /// <summary>Gets how many sprites use the texture.</summary>
  public int Users => Entry.Users;

  /// <summary>Gets whether the same file was loaded more than once.</summary>
  public bool IsDuplicate => Entry.Duplicate;

  /// <summary>
  /// Gets the text of the warning badge, which is empty for a texture that is not a duplicate.
  /// </summary>
  public string DuplicateText => Entry.Duplicate ? "Duplicate" : string.Empty;
}

/// <summary>
/// The header button of one column of the textures table.
/// </summary>
public sealed partial class SortHeaderViewModel : ObservableObject
{
  [ObservableProperty]
  private string _text;

  /// <summary>
  /// Creates a header.
  /// </summary>
  /// <param name="key">The column the header sorts by.</param>
  /// <param name="title">The column title.</param>
  public SortHeaderViewModel(TextureSortKey key, string title)
  {
    Key = key;
    Title = title;
    _text = title;
  }

  /// <summary>Gets the column the header sorts by.</summary>
  public TextureSortKey Key { get; }

  /// <summary>Gets the column title without a sort arrow.</summary>
  public string Title { get; }
}

/// <summary>
/// The ViewModel of the textures panel: totals, a sortable table, and a preview with a pager.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sorting.</b> <see cref="SortBy"/> calls <c>TextureSorter</c> in Core. The pager walks through
/// the table in the order it is sorted right now.
/// </para>
/// <para>
/// <b>Selection.</b> Selecting a row asks the game for that texture's thumbnail. The game rebuilds
/// the list about once a second, so the selection is kept by texture id when the table is
/// refreshed. The refresh itself never sends a request.
/// </para>
/// </remarks>
public sealed partial class TexturesPanelViewModel : LivePanelViewModel
{
  private const int PreviewTimeoutTicks = 30;
  private readonly List<TextureRowViewModel> _all = [];
  private TexturesListPayload? _last;
  private int _shownFor = -1;
  private bool _syncing;
  private bool _built;

  [ObservableProperty]
  private TextureRowViewModel? _selectedRow;

  [ObservableProperty]
  private string _countText = "Textures: -";

  [ObservableProperty]
  private string _gpuText = "GPU: -";

  [ObservableProperty]
  private string _duplicatesText = "Duplicates: -";

  [ObservableProperty]
  private string _pageText = "0 of 0";

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  public TexturesPanelViewModel(IActiveSession active, IUiScheduler scheduler)
      : base(BuiltInPanels.Textures, "Textures", active, scheduler)
  {
    Preview = new ThumbnailPreviewViewModel(PreviewTimeoutTicks);
    Headers =
    [
        new(TextureSortKey.Id, "Id"),
        new(TextureSortKey.Name, "Name or path"),
        new(TextureSortKey.Size, "Size"),
        new(TextureSortKey.Format, "Format"),
        new(TextureSortKey.GpuBytes, "GPU MB"),
        new(TextureSortKey.Users, "Users"),
        new(TextureSortKey.Duplicate, "Duplicate"),
    ];
    UpdateHeaders();
    Refresh();
  }

  /// <summary>Raised after the sort column or direction changed.</summary>
  public event EventHandler? SortChanged;

  /// <summary>Gets the column headers, in the order of <see cref="TextureSortKey"/>.</summary>
  public IReadOnlyList<SortHeaderViewModel> Headers { get; }

  /// <summary>Gets the rows in the current sort order.</summary>
  public RangeObservableCollection<TextureRowViewModel> Rows { get; } = [];

  /// <summary>Gets the preview of the selected texture.</summary>
  public ThumbnailPreviewViewModel Preview { get; }

  /// <summary>Gets the column the table is sorted by.</summary>
  public TextureSortKey SortKey { get; private set; } = TextureSortKey.Id;

  /// <summary>Gets whether the sort order is largest first.</summary>
  public bool SortDescending { get; private set; }

  /// <summary>
  /// Sorts by a column. Choosing the column that is already used flips the direction.
  /// </summary>
  /// <param name="key">The column to sort by.</param>
  public void SortBy(TextureSortKey key)
  {
    if (key == SortKey)
    {
      SortDescending = !SortDescending;
    }
    else
    {
      SortKey = key;
      SortDescending = false;
    }

    UpdateHeaders();
    Rebuild();
    SortChanged?.Invoke(this, EventArgs.Empty);
  }

  /// <inheritdoc/>
  public override void Refresh()
  {
    var payload = Session?.Textures;
    if (!_built || !ReferenceEquals(payload, _last))
    {
      _built = true;
      _last = payload;
      _all.Clear();
      foreach (var entry in payload?.Entries ?? [])
      {
        _all.Add(new TextureRowViewModel(entry));
      }

      CountText = payload is null ? "Textures: -" : $"Textures: {payload.TextureCount}";
      GpuText = payload is null ? "GPU: -" : string.Create(CultureInfo.InvariantCulture, $"GPU: {payload.TotalGpuBytes / (1024.0 * 1024.0):0.00} MB");
      DuplicatesText = payload is null ? "Duplicates: -" : $"Duplicates: {payload.DuplicateCount}";
      Rebuild();
    }

    if (SelectedRow is { } selected && Session is { } session && session.TextureThumbnails.TryGet(selected.Id, out var thumbnail))
    {
      Preview.Show(thumbnail);
    }

    Preview.Tick();
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    _built = false;
    _shownFor = -1;
    _syncing = true;
    SelectedRow = null;
    _syncing = false;
    Preview.Clear();
  }

  partial void OnSelectedRowChanged(TextureRowViewModel? value)
  {
    UpdatePage();
    PreviousCommand.NotifyCanExecuteChanged();
    NextCommand.NotifyCanExecuteChanged();
    if (_syncing)
    {
      return;
    }

    if (value is null)
    {
      _shownFor = -1;
      Preview.Clear();
    }
    else if (value.Id != _shownFor)
    {
      _shownFor = value.Id;
      Preview.BeginWaiting();
      if (Session is { } session)
      {
        _ = session.RequestThumbnailAsync(value.Id);
      }
    }
  }

  [RelayCommand]
  private void SortByColumn(TextureSortKey key) => SortBy(key);

  [RelayCommand(CanExecute = nameof(CanGoPrevious))]
  private void Previous() => Select(Rows.IndexOf(SelectedRow!) - 1);

  [RelayCommand(CanExecute = nameof(CanGoNext))]
  private void Next() => Select(Rows.IndexOf(SelectedRow!) + 1);

  private bool CanGoPrevious() => SelectedRow is not null && Rows.IndexOf(SelectedRow) > 0;

  private bool CanGoNext() => SelectedRow is not null && Rows.IndexOf(SelectedRow) < Rows.Count - 1;

  private void Select(int index)
  {
    if (index >= 0 && index < Rows.Count)
    {
      SelectedRow = Rows[index];
    }
  }

  // Sorts the stored rows into the table and puts the selection back on the same texture.
  private void Rebuild()
  {
    var selectedId = SelectedRow?.Id ?? -1;
    var byId = new Dictionary<int, TextureRowViewModel>(_all.Count);
    foreach (var row in _all)
    {
      byId[row.Id] = row;
    }

    var sorted = TextureSorter.Sort(_all.ConvertAll(static r => r.Entry), SortKey, SortDescending);
    _syncing = true;
    Rows.ReplaceAll(sorted.ConvertAll(e => byId[e.Id]));
    SelectedRow = selectedId >= 0 && byId.TryGetValue(selectedId, out var again) ? again : null;
    _syncing = false;
    if (SelectedRow is null)
    {
      _shownFor = -1;
      Preview.Clear();
    }

    UpdatePage();
    PreviousCommand.NotifyCanExecuteChanged();
    NextCommand.NotifyCanExecuteChanged();
  }

  private void UpdateHeaders()
  {
    foreach (var header in Headers)
    {
      header.Text = header.Key != SortKey ? header.Title : $"{header.Title} {(SortDescending ? "\u25BC" : "\u25B2")}";
    }
  }

  private void UpdatePage()
  {
    var index = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);
    PageText = string.Create(CultureInfo.InvariantCulture, $"{index + 1} of {Rows.Count}");
  }
}
