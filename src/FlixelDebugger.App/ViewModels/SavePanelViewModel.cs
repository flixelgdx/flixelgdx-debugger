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
/// The ViewModel of the save panel: the game's save data as a table, with editing and reset buttons.
/// </summary>
/// <remarks>
/// <para>
/// <b>Table.</b> <c>SaveListing</c> in Core builds the rows and applies the filter. Clicking a row copies its
/// path and value into the two boxes under the table.
/// </para>
/// <para>
/// <b>Reset all.</b> Erasing the whole save by accident would hurt, so the button asks twice: the first click arms it
/// (the label changes to <see cref="ArmedLabel"/>), and a second click within three seconds sends the action. If the
/// time runs out, the button goes back to normal by itself. <c>ConfirmArm</c> in Core keeps the time.
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

  private readonly ConfirmArm _arm;
  private SavePayload? _last;
  private string _lastFilter = string.Empty;
  private bool _built;

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
    _arm = new ConfirmArm(clock);
    Refresh();
  }

  /// <summary>Gets the rows that match the filter.</summary>
  public RangeObservableCollection<SaveRowViewModel> Rows { get; } = [];

  /// <summary>Gets whether the Set button can be used: both boxes are filled and within the length the game accepts.</summary>
  public bool CanSet => PathText.Length is > 0 and <= MaxTextLength && ValueText.Length <= MaxTextLength;

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
      Rows.ReplaceAll(SaveListing.Build(payload, Filter).ConvertAll(static r => new SaveRowViewModel(r)));
      Status = payload?.Status ?? string.Empty;
      IsError = payload?.IsError ?? false;
    }

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

  private void SyncArm()
  {
    IsResetAllArmed = _arm.IsArmed;
    ResetAllText = IsResetAllArmed ? ArmedLabel : ResetAllLabel;
  }
}
