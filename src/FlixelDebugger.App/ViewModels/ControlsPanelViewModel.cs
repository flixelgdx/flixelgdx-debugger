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
using FlixelDebugger.App.Panels;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// A button of the log level row.
/// </summary>
public sealed partial class LogLevelOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Creates an option.
    /// </summary>
    /// <param name="level">The level the button selects.</param>
    /// <param name="label">The button text.</param>
    public LogLevelOptionViewModel(LogLevel level, string label)
    {
        Level = level;
        Label = label;
    }

    /// <summary>Gets the level the button selects.</summary>
    public LogLevel Level { get; }

    /// <summary>Gets the button text.</summary>
    public string Label { get; }
}

/// <summary>
/// A row of the keybinds list: a key and what it does.
/// </summary>
/// <param name="Key">The key or key combination.</param>
/// <param name="Description">What the key does.</param>
public sealed record KeybindRowViewModel(string Key, string Description);

/// <summary>
/// The ViewModel of the controls panel: checkboxes, step, update rate, log level, and the keybinds list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Syncing.</b> The game is the source of truth. Whenever it sends a new controls payload, every control copies
/// it. A change the person makes is sent to the game, and the game's payload then confirms it.
/// </para>
/// <para>
/// <b>The slider.</b> Sending a rate on every pixel of a drag would flood the game, so the rate is sent when the
/// drag ends (<see cref="EndRateDrag"/>), or, for a keyboard change, a fraction of a second after the last change. While the person
/// drags, or until the game echoes the new rate, an incoming payload does not move the slider, so it never
/// fights the hand that holds it.
/// </para>
/// </remarks>
public sealed partial class ControlsPanelViewModel : LivePanelViewModel
{
    /// <summary>The rate the Reset button returns to, in Hz.</summary>
    public const byte DefaultRate = 20;

    /// <summary>The smallest update rate, in Hz.</summary>
    public const double MinRate = 1;

    /// <summary>The largest update rate, in Hz.</summary>
    public const double MaxRate = 30;

    // A keyboard change is sent after this many quiet refreshes, and the wait for an echo ends after this many.
    private const int DebounceTicks = 2;
    private const int EchoTimeoutTicks = 30;

    private readonly KeybindRowViewModel[] _appShortcuts =
    [
        new("F2", "Show hitboxes (this window)"),
        new("F3", "Pause or resume (this window)"),
        new("F4", "Free camera (this window)"),
    ];

    private readonly IPanelLayout _layout;
    private ControlsPayload? _lastControls;
    private IReadOnlyList<Keybind>? _lastKeybinds;
    private byte? _pendingRate;
    private int _quietTicks;
    private int _echoTicks;
    private bool _resync = true;
    private bool _syncing;
    private bool _rateDirty;
    private bool _dragging;

    [ObservableProperty]
    private bool _showHitboxes;

    [ObservableProperty]
    private bool _paused;

    [ObservableProperty]
    private bool _freeCam;

    [ObservableProperty]
    private double _rateHz = DefaultRate;

    [ObservableProperty]
    private bool _hasSession;

    /// <summary>
    /// Creates the panel.
    /// </summary>
    /// <param name="active">Tells which game is active.</param>
    /// <param name="scheduler">Provides the UI-thread timer.</param>
    /// <param name="layout">Resets the panel layout.</param>
    public ControlsPanelViewModel(IActiveSession active, IUiScheduler scheduler, IPanelLayout layout)
        : base(BuiltInPanels.Controls, "Controls", active, scheduler)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        LogLevels =
        [
            new(LogLevel.Default, "Default"),
            new(LogLevel.Debug, "Debug"),
            new(LogLevel.Info, "Info"),
            new(LogLevel.Warn, "Warn"),
            new(LogLevel.Error, "Error"),
        ];
        Refresh();
    }

    /// <summary>Gets the log level buttons.</summary>
    public IReadOnlyList<LogLevelOptionViewModel> LogLevels { get; }

    /// <summary>Gets the keybinds the game sent, in the order it sent them.</summary>
    public RangeObservableCollection<KeybindRowViewModel> Keybinds { get; } = [];

    /// <summary>Gets whether the game has sent no keybinds yet.</summary>
    public bool HasNoKeybinds => Keybinds.Count == 0;

    /// <summary>Gets the shortcuts of this window: F2, F3, and F4.</summary>
    public IReadOnlyList<KeybindRowViewModel> AppShortcuts => _appShortcuts;

    /// <summary>Gets the slider text, for example <c>20 Hz</c>.</summary>
    public string RateText => string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(RateHz)} Hz");

    /// <summary>
    /// Tells the panel that the person grabbed the slider.
    /// </summary>
    public void BeginRateDrag() => _dragging = true;

    /// <summary>
    /// Tells the panel that the person let go of the slider, which sends the new rate.
    /// </summary>
    public void EndRateDrag()
    {
        _dragging = false;
        CommitRate();
    }

    /// <inheritdoc/>
    public override void Refresh()
    {
        var session = Session;
        HasSession = session is not null;
        if (_rateDirty && !_dragging && ++_quietTicks >= DebounceTicks)
        {
            CommitRate();
        }

        if (_pendingRate is not null && ++_echoTicks > EchoTimeoutTicks)
        {
            _pendingRate = null;
        }

        var controls = session?.Controls;
        if (!ReferenceEquals(controls, _lastControls))
        {
            _lastControls = controls;
            _resync = true;
        }

        if (_resync && controls is not null)
        {
            Sync(controls);
        }
    }

    /// <inheritdoc/>
    protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
    {
        _lastControls = null;
        _lastKeybinds = null;
        _pendingRate = null;
        _rateDirty = false;
        _resync = true;
    }

    partial void OnShowHitboxesChanged(bool value)
    {
        if (!_syncing && Session is { } session)
        {
            _ = session.SetHitboxesAsync(value);
        }
    }

    partial void OnPausedChanged(bool value)
    {
        if (!_syncing && Session is { } session)
        {
            _ = session.SetPausedAsync(value);
        }
    }

    partial void OnFreeCamChanged(bool value)
    {
        if (!_syncing && Session is { } session)
        {
            _ = session.SetFreeCamAsync(value);
        }
    }

    partial void OnRateHzChanged(double value)
    {
        OnPropertyChanged(nameof(RateText));
        if (!_syncing)
        {
            _rateDirty = true;
            _quietTicks = 0;
        }
    }

    [RelayCommand]
    private async Task StepFrameAsync()
    {
        if (Session is { } session)
        {
            await session.StepAsync();
        }
    }

    [RelayCommand]
    private async Task ResetRateAsync()
    {
        _rateDirty = false;
        _syncing = true;
        RateHz = DefaultRate;
        _syncing = false;
        await SendRateAsync(DefaultRate);
    }

    [RelayCommand]
    private async Task SetLogLevelAsync(LogLevel level)
    {
        if (Session is { } session)
        {
            await session.SetLogLevelAsync(level);
        }
    }

    [RelayCommand]
    private void ResetLayout() => _layout.Reset();

    private void CommitRate()
    {
        if (!_rateDirty)
        {
            return;
        }

        _rateDirty = false;
        _ = SendRateAsync((byte)Math.Clamp((int)Math.Round(RateHz), (int)MinRate, (int)MaxRate));
    }

    private async Task SendRateAsync(byte rate)
    {
        _pendingRate = rate;
        _echoTicks = 0;
        if (Session is { } session)
        {
            await session.SetRateAsync(rate);
        }
    }

    // Copies the game's state into the controls. The slider is skipped while the person holds it or waits for an echo.
    private void Sync(ControlsPayload controls)
    {
        _syncing = true;
        ShowHitboxes = controls.Hitboxes;
        Paused = controls.Paused;
        FreeCam = controls.FreeCam;
        foreach (var option in LogLevels)
        {
            option.IsActive = option.Level == controls.LogLevel;
        }

        var rateBusy = _dragging || _rateDirty || (_pendingRate is { } pending && pending != controls.Rate);
        if (!rateBusy)
        {
            RateHz = controls.Rate;
            _pendingRate = null;
        }

        _resync = rateBusy;
        _syncing = false;

        if (ReferenceEquals(controls.Keybinds, _lastKeybinds))
        {
            return;
        }

        _lastKeybinds = controls.Keybinds;
        var rows = new KeybindRowViewModel[controls.Keybinds.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = new KeybindRowViewModel(controls.Keybinds[i].Key, controls.Keybinds[i].Description);
        }

        Keybinds.ReplaceAll(rows);
        OnPropertyChanged(nameof(HasNoKeybinds));
    }
}
