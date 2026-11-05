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
/// The ViewModel of the watches panel: a Name and Value table of the values the game registered.
/// </summary>
/// <remarks>
/// Numbers are shown by the shared rules of <c>ValueFormat</c> (whole numbers as integers, others with two
/// decimals). A value the game failed to read shows as red <c>error: message</c>.
/// </remarks>
public sealed partial class WatchesPanelViewModel : LivePanelViewModel
{
    /// <summary>The hint shown while the game has no watches.</summary>
    public const string EmptyHint = "No watches yet. Register a value in the game and it shows up here.";

    private WatchesPayload? _last;
    private bool _built;

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>
    /// Creates the panel.
    /// </summary>
    /// <param name="active">Tells which game is active.</param>
    /// <param name="scheduler">Provides the UI-thread timer.</param>
    public WatchesPanelViewModel(IActiveSession active, IUiScheduler scheduler)
        : base(BuiltInPanels.Watches, "Watches", active, scheduler)
    {
        Refresh();
    }

    /// <summary>Gets the rows in the order the game sent them.</summary>
    public RangeObservableCollection<ValueRowViewModel> Rows { get; } = [];

    /// <inheritdoc/>
    public override void Refresh()
    {
        var payload = Session?.Watches;
        if (_built && ReferenceEquals(payload, _last))
        {
            return;
        }

        _built = true;
        _last = payload;
        ValueRowViewModel.Sync(Rows, payload?.Rows ?? []);
        IsEmpty = Rows.Count == 0;
    }

    /// <inheritdoc/>
    protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession) => _built = false;
}
