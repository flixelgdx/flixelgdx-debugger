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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One row of the save table.
/// </summary>
/// <param name="Row">The row as Core built it.</param>
public sealed record SaveRowViewModel(SaveListRow Row)
{
  /// <summary>Gets the path as shown: unsaved values start with <c>* </c>.</summary>
  public string Label => Row.Label;

  /// <summary>Gets the value as text.</summary>
  public string Display => Row.Display;

  /// <summary>Gets the raw path.</summary>
  public string Path => Row.Path;

  /// <summary>Gets whether the path was removed and not yet saved.</summary>
  public bool IsRemoved => Row.Removed;
}

/// <summary>
/// One entry of the save picker: the label of a save the game keeps.
/// </summary>
/// <remarks>
/// The label is observable so a refresh can rename an entry in place. Replacing the entry instead
/// would make the ComboBox drop its selection for a moment.
/// </remarks>
public sealed partial class SaveChoiceViewModel : ObservableObject
{
  [ObservableProperty]
  private string _label;

  /// <summary>
  /// Creates an entry.
  /// </summary>
  /// <param name="label">The label the game sent, for example <c>Game/settings</c>.</param>
  public SaveChoiceViewModel(string label)
  {
    _label = label;
  }

  /// <inheritdoc/>
  public override string ToString() => Label;
}

/// <summary>
/// The ViewModel of the save panel: the game's save data as a table, with editing and reset
/// buttons.
/// </summary>
/// <remarks>
/// <para>
/// <b>Table.</b> <c>SaveListing</c> in Core builds the rows and applies the filter. Clicking a row
/// copies its path and value into the two boxes under the table.
/// </para>
/// <para>
/// <b>Reset all.</b> Erasing the whole save by accident would hurt, so the button asks twice: the
/// first click arms it (the label changes to <see cref="ArmedLabel"/>), and a second click within
/// three seconds sends the action. If the time runs out, the button goes back to normal by itself.
/// <c>ConfirmArm</c> in Core keeps the time.
/// </para>
/// <para>
/// <b>Save picker.</b> A game can keep several saves (a settings file and a few profiles, for
/// example) and the panel edits one of them at a time. <see cref="Saves"/> lists them and
/// <see cref="SelectedSaveIndex"/> is the one shown; the picker is only visible when there is more
/// than one (<see cref="HasManySaves"/>). The game is the source of truth: picking another save
/// sends <c>SELECT</c>, and the game answers with the rows of that save. Two things keep the
/// ComboBox from fighting the game. Entries are updated in place, never replaced, and a ComboBox
/// that reports "no selection" while its items change is ignored. And a selection the game sends
/// is copied without sending it back, while a pick the person just made is kept on screen until
/// the game confirms it (or a couple of seconds pass).
/// </para>
/// </remarks>
public sealed partial class SavePanelViewModel : LivePanelViewModel
{
  /// <summary>The longest path or value text the game accepts, in characters.</summary>
  public const int MaxTextLength = 200;

  /// <summary>The label of the reset-all button.</summary>
  public const string ResetAllLabel = "Reset all";

  /// <summary>The label of the reset-all button while it waits for the second click.</summary>
  public const string ArmedLabel = "Click again to reset";

  // How many refreshes a pick waits for the game to confirm it before the game's choice wins.
  private const int PickTimeoutTicks = 20;

  private readonly ConfirmArm _arm;
  private readonly IUiScheduler _ui;
  private SavePayload? _last;
  private string _lastFilter = string.Empty;
  private int _selectedSaveIndex;
  private int _pendingPick = -1;
  private int _pickTicks;
  private bool _built;
  private bool _syncingSaves;

  [ObservableProperty]
  private string _filter = string.Empty;

  [ObservableProperty]
  private string _pathText = string.Empty;

  [ObservableProperty]
  private string _valueText = string.Empty;

  [ObservableProperty]
  private string _status = string.Empty;

  [ObservableProperty]
  private bool _isError;

  [ObservableProperty]
  private string _resetAllText = ResetAllLabel;

  [ObservableProperty]
  private bool _isResetAllArmed;

  [ObservableProperty]
  private SaveRowViewModel? _selectedRow;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  /// <param name="clock">Measures the three seconds of the reset-all confirmation.</param>
  public SavePanelViewModel(IActiveSession active, IUiScheduler scheduler, TimeProvider clock)
      : base(BuiltInPanels.Save, "Save", active, scheduler)
  {
    ArgumentNullException.ThrowIfNull(clock);
    _ui = scheduler;
    _arm = new ConfirmArm(clock);
    Refresh();
  }

  /// <summary>Gets the rows that match the filter.</summary>
  public RangeObservableCollection<SaveRowViewModel> Rows { get; } = [];

  /// <summary>
  /// Gets the saves the person can pick from. Entry 0 is always the game's main save.
  /// </summary>
  public ObservableCollection<SaveChoiceViewModel> Saves { get; } = [];

  /// <summary>
  /// Gets whether the game keeps more than one save, so the picker is worth showing. Games that do
  /// not send a save list count as one save.
  /// </summary>
  public bool HasManySaves => Saves.Count > 1;

  /// <summary>
  /// Gets or sets the position in <see cref="Saves"/> of the save the table shows.
  /// </summary>
  /// <remarks>
  /// Setting another index asks the game to switch (<c>SELECT</c>). A negative or out-of-range
  /// value is what a ComboBox reports while its items change; it is ignored and the property
  /// change is raised again, so the control shows the real selection.
  /// </remarks>
  public int SelectedSaveIndex
  {
    get => _selectedSaveIndex;
    set
    {
      if (_syncingSaves)
      {
        // The ComboBox reacting to its items changing. The sync sets the real index afterwards.
        return;
      }

      if (value < 0 || value >= Saves.Count)
      {
        _ui.Post(() => OnPropertyChanged(nameof(SelectedSaveIndex)));
        return;
      }

      if (!SetProperty(ref _selectedSaveIndex, value))
      {
        return;
      }

      // The person picked another save. Keep it on screen until the game confirms it.
      _pendingPick = value;
      _pickTicks = 0;
      ClearEditor();
      if (Session is { } session)
      {
        _ = session.SaveSelectAsync((ushort)value);
      }
    }
  }

  /// <summary>
  /// Gets whether the Set button can be used: both boxes are filled and within the length the game
  /// accepts.
  /// </summary>
  public bool CanSet =>
      PathText.Length is > 0 and <= MaxTextLength && ValueText.Length <= MaxTextLength;

  /// <summary>Gets whether a path is typed, which the Reset key button needs.</summary>
  public bool HasPath => PathText.Length is > 0 and <= MaxTextLength;

  /// <inheritdoc/>
  public override void Refresh()
  {
    var payload = Session?.Save;
    if (!_built || !ReferenceEquals(payload, _last) || Filter != _lastFilter)
    {
      _built = true;
      _last = payload;
      _lastFilter = Filter;
      Rows.ReplaceAll(
          SaveListing.Build(payload, Filter).ConvertAll(static r => new SaveRowViewModel(r)));
      Status = payload?.Status ?? string.Empty;
      IsError = payload?.IsError ?? false;
    }

    SyncSaves(payload);

    // The armed state ends by itself, so the label follows the clock.
    if (IsResetAllArmed != _arm.IsArmed)
    {
      SyncArm();
    }
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    _built = false;
    _pendingPick = -1;
    _arm.Disarm();
    SyncArm();
  }

  partial void OnFilterChanged(string value) => Refresh();

  partial void OnPathTextChanged(string value)
  {
    OnPropertyChanged(nameof(CanSet));
    OnPropertyChanged(nameof(HasPath));
    SetCommand.NotifyCanExecuteChanged();
    ResetKeyCommand.NotifyCanExecuteChanged();
  }

  partial void OnValueTextChanged(string value)
  {
    OnPropertyChanged(nameof(CanSet));
    SetCommand.NotifyCanExecuteChanged();
  }

  partial void OnSelectedRowChanged(SaveRowViewModel? value)
  {
    if (value is not null)
    {
      PathText = value.Path;
      ValueText = value.IsRemoved ? string.Empty : value.Display;
    }
  }

  [RelayCommand(CanExecute = nameof(CanSet))]
  private async Task SetAsync()
  {
    if (Session is { } session)
    {
      await session.SaveSetAsync(PathText, ValueText);
    }
  }

  [RelayCommand(CanExecute = nameof(HasPath))]
  private async Task ResetKeyAsync()
  {
    if (Session is { } session)
    {
      await session.SaveResetKeyAsync(PathText);
    }
  }

  [RelayCommand]
  private async Task SaveNowAsync()
  {
    if (Session is { } session)
    {
      await session.SaveNowAsync();
    }
  }

  [RelayCommand]
  private async Task ResetAllAsync()
  {
    var confirmed = _arm.Press();
    SyncArm();
    if (confirmed && Session is { } session)
    {
      await session.SaveResetAllAsync();
    }
  }

  // Copies the game's save list and selection, without sending anything back.
  private void SyncSaves(SavePayload? payload)
  {
    var labels = payload?.Saves ?? [];
    var selected = labels.Count == 0 ? 0 : Math.Min((int)payload!.Selected, labels.Count - 1);
    if (_pendingPick >= 0)
    {
      // Wait for the game to switch, unless it takes too long or the pick no longer exists.
      if (selected == _pendingPick ||
          ++_pickTicks > PickTimeoutTicks ||
          _pendingPick >= labels.Count)
      {
        _pendingPick = -1;
      }
      else
      {
        selected = _pendingPick;
      }
    }

    var hadMany = HasManySaves;
    var resized = Saves.Count != labels.Count;
    _syncingSaves = true;
    try
    {
      for (var i = 0; i < labels.Count; i++)
      {
        if (i < Saves.Count)
        {
          Saves[i].Label = labels[i];
        }
        else
        {
          Saves.Add(new SaveChoiceViewModel(labels[i]));
        }
      }

      while (Saves.Count > labels.Count)
      {
        Saves.RemoveAt(Saves.Count - 1);
      }

      var moved = selected != _selectedSaveIndex;
      if (moved)
      {
        ClearEditor();
      }

      // Also raised when only the list changed size: adding or removing entries may have cleared
      // the ComboBox's own selection.
      _selectedSaveIndex = selected;
      if (moved || resized)
      {
        OnPropertyChanged(nameof(SelectedSaveIndex));
      }
    }
    finally
    {
      _syncingSaves = false;
    }

    if (hadMany != HasManySaves)
    {
      OnPropertyChanged(nameof(HasManySaves));
    }
  }

  // Another save is shown, so a path typed for the old one means nothing any more.
  private void ClearEditor()
  {
    SelectedRow = null;
    PathText = string.Empty;
    ValueText = string.Empty;
  }

  private void SyncArm()
  {
    IsResetAllArmed = _arm.IsArmed;
    ResetAllText = IsResetAllArmed ? ArmedLabel : ResetAllLabel;
  }
}
