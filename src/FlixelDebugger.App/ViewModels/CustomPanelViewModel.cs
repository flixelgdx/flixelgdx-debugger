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

using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// A button of a custom panel.
/// </summary>
public sealed class CustomButtonViewModel
{
    /// <summary>
    /// Creates a button.
    /// </summary>
    /// <param name="actionIndex">The action index the game gave the button.</param>
    /// <param name="label">The button text.</param>
    /// <param name="press">Sends the press.</param>
    public CustomButtonViewModel(ushort actionIndex, string label, Func<ushort, Task> press)
    {
        ArgumentNullException.ThrowIfNull(press);
        ActionIndex = actionIndex;
        Label = label;
        PressCommand = new AsyncRelayCommand(() => press(actionIndex));
    }

    /// <summary>Gets the action index the game gave the button.</summary>
    public ushort ActionIndex { get; }

    /// <summary>Gets the button text.</summary>
    public string Label { get; }

    /// <summary>Gets the command that presses the button in the game.</summary>
    public IAsyncRelayCommand PressCommand { get; }
}

/// <summary>
/// The ViewModel of a panel that game code registered itself: property rows plus action buttons.
/// </summary>
/// <remarks>
/// The rows behave exactly like the inspector's (see <see cref="PropertyRowViewModel"/>), but an edit is sent with
/// <c>CustomSetPropertyAsync</c> and a button with <c>CustomPressAsync</c>.
/// </remarks>
public sealed class CustomPanelViewModel : LivePanelViewModel
{
    private CustomPayload? _last;
    private bool _built;

    /// <summary>
    /// Creates the panel.
    /// </summary>
    /// <param name="id">The panel id the game gave it, usually starting with <c>custom.</c>.</param>
    /// <param name="title">The title the game gave it.</param>
    /// <param name="active">Tells which game is active.</param>
    /// <param name="scheduler">Provides the UI-thread timer.</param>
    public CustomPanelViewModel(string id, string title, IActiveSession active, IUiScheduler scheduler)
        : base(id, title, active, scheduler)
    {
        Grid = new PropertyGridViewModel(SetPropertyAsync);
        Refresh();
    }

    /// <summary>Gets the rows.</summary>
    public PropertyGridViewModel Grid { get; }

    /// <summary>Gets the buttons in the order the game sent them.</summary>
    public RangeObservableCollection<CustomButtonViewModel> Buttons { get; } = [];

    /// <inheritdoc/>
    public override void Refresh()
    {
        var payload = Session?.GetCustom(Id);
        if (_built && ReferenceEquals(payload, _last))
        {
            return;
        }

        _built = true;
        _last = payload;
        Grid.Update(payload?.Rows ?? []);
        var buttons = new List<CustomButtonViewModel>();
        foreach (var button in payload?.Buttons ?? [])
        {
            buttons.Add(new CustomButtonViewModel(button.ActionIndex, button.Label, PressAsync));
        }

        if (!SameButtons(buttons))
        {
            Buttons.ReplaceAll(buttons);
        }
    }

    /// <inheritdoc/>
    protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession) => _built = false;

    private bool SameButtons(List<CustomButtonViewModel> buttons)
    {
        if (buttons.Count != Buttons.Count)
        {
            return false;
        }

        for (var i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].ActionIndex != Buttons[i].ActionIndex || buttons[i].Label != Buttons[i].Label)
            {
                return false;
            }
        }

        return true;
    }

    private Task SetPropertyAsync(int index, PropertyValue value) =>
        Session is { } session ? session.CustomSetPropertyAsync(Id, (ushort)index, value) : Task.CompletedTask;

    private Task PressAsync(ushort actionIndex) =>
        Session is { } session ? session.CustomPressAsync(Id, actionIndex) : Task.CompletedTask;
}
