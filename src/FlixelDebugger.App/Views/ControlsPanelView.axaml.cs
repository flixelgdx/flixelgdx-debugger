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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Views;

/// <summary>
/// The view of the controls panel: checkboxes, the update rate slider, log level buttons, and the
/// keybinds.
/// </summary>
/// <remarks>
/// The view only tells the ViewModel when the slider is grabbed and let go, so the rate is sent
/// once per drag.
/// </remarks>
public sealed partial class ControlsPanelView : UserControl
{
  /// <summary>
  /// Creates the view.
  /// </summary>
  public ControlsPanelView()
  {
    InitializeComponent();

    // The slider's thumb marks these events handled, so listen even for handled events.
    RateSlider.AddHandler(PointerPressedEvent, OnSliderPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    RateSlider.AddHandler(PointerReleasedEvent, OnSliderReleased, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
    RateSlider.PointerCaptureLost += OnSliderCaptureLost;
  }

  private ControlsPanelViewModel? Panel => DataContext as ControlsPanelViewModel;

  private void OnSliderPressed(object? sender, PointerPressedEventArgs e) => Panel?.BeginRateDrag();

  private void OnSliderReleased(object? sender, PointerReleasedEventArgs e) => Panel?.EndRateDrag();

  private void OnSliderCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Panel?.EndRateDrag();

  private void OnLogLevelClick(object? sender, RoutedEventArgs e)
  {
    if (sender is Button { DataContext: LogLevelOptionViewModel option } && Panel is { } panel)
    {
      panel.SetLogLevelCommand.Execute(option.Level);
    }
  }
}
