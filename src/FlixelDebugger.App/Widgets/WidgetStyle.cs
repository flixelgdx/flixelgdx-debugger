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
using Avalonia.Media;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Applies the properties every widget shares (visibility, enabled, tooltip, size, and style) to
/// the control that draws it.
/// </summary>
/// <remarks>
/// <para>
/// The game never names a color or a font. It names a <i>tone</i> (muted, accent, success, warning,
/// error) and a few flags (bold, mono, heading, italic). This class turns them into style classes
/// such as <c>tone-error</c> and <c>mono</c>, and <c>App.axaml</c> decides what those classes look
/// like in the light and dark theme. That keeps every panel, built-in or custom, matching the app.
/// </para>
/// </remarks>
public static class WidgetStyle
{
  private static readonly string[] _toneClasses =
      ["", "tone-muted", "tone-accent", "tone-success", "tone-warning", "tone-error"];

  private static readonly WidgetProp[] _commonKeys =
  [
      WidgetProp.Visible,
      WidgetProp.Enabled,
      WidgetProp.Tooltip,
      WidgetProp.Width,
      WidgetProp.Height,
      WidgetProp.Style,
  ];

  /// <summary>
  /// Applies the common properties once and keeps them up to date.
  /// </summary>
  /// <param name="control">The control that draws the widget.</param>
  /// <param name="widget">The widget.</param>
  /// <param name="styled">
  /// Whether the widget's style bits apply to this control. Containers pass
  /// <see langword="false"/>, so a tone does not leak into every child.
  /// </param>
  public static void Bind(Control control, UiWidget widget, bool styled = true)
  {
    ArgumentNullException.ThrowIfNull(control);
    ArgumentNullException.ThrowIfNull(widget);
    foreach (var key in _commonKeys)
    {
      Apply(control, widget, key, styled);
    }

    widget.Changed += (_, key) => Apply(control, widget, key, styled);
  }

  /// <summary>
  /// Sets the style classes of a control from style bits.
  /// </summary>
  /// <param name="control">The control.</param>
  /// <param name="style">The style bits.</param>
  public static void SetClasses(Control control, int style)
  {
    ArgumentNullException.ThrowIfNull(control);
    var tone = (int)UiStyle.ToneOf(style);
    for (var i = 1; i < _toneClasses.Length; i++)
    {
      control.Classes.Set(_toneClasses[i], i == tone);
    }

    control.Classes.Set("bold", UiStyle.Has(style, UiStyle.Bold));
    control.Classes.Set("mono", UiStyle.Has(style, UiStyle.Mono));
    control.Classes.Set("heading", UiStyle.Has(style, UiStyle.Heading));
    control.Classes.Set("italic", UiStyle.Has(style, UiStyle.Italic));
  }

  /// <summary>
  /// Finds a brush of the current theme for a tone.
  /// </summary>
  /// <param name="control">A control in the window, used to look the brush up.</param>
  /// <param name="tone">The tone.</param>
  /// <param name="fallback">The brush for the normal tone or when nothing is found.</param>
  /// <returns>The brush.</returns>
  public static IBrush BrushOf(Control control, Tone tone, IBrush fallback)
  {
    ArgumentNullException.ThrowIfNull(control);
    var key = tone switch
    {
      Tone.Muted => "MutedTextBrush",
      Tone.Accent => "SystemControlHighlightAccentBrush",
      Tone.Success => "GoodBrush",
      Tone.Warning => "LogWarnBrush",
      Tone.Error => "LogErrorBrush",
      _ => null,
    };
    return key is not null &&
        control.TryFindResource(key, control.ActualThemeVariant, out var value) &&
        value is IBrush brush
        ? brush
        : fallback;
  }

  private static void Apply(Control control, UiWidget widget, WidgetProp key, bool styled)
  {
    switch (key)
    {
      case WidgetProp.Visible:
        control.IsVisible = widget.IsVisible;
        break;
      case WidgetProp.Enabled:
        control.IsEnabled = widget.IsEnabled;
        break;
      case WidgetProp.Tooltip:
        ToolTip.SetTip(control, widget.Tooltip.Length == 0 ? null : widget.Tooltip);
        break;
      case WidgetProp.Width:
        control.Width = widget.Width > 0 ? widget.Width : double.NaN;
        break;
      case WidgetProp.Height:
        control.Height = widget.Height > 0 ? widget.Height : double.NaN;
        break;
      case WidgetProp.Style when styled:
        SetClasses(control, widget.Style);
        break;
    }
  }
}
