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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FlixelDebugger.Core.Graphs;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Draws a <see cref="WidgetType.Graph"/> widget: one or more lines over time.
/// </summary>
/// <remarks>
/// <para>
/// The game appends one sample per update; the control only draws what it has. The newest sample
/// sits at the right edge and older ones scroll to the left. The title shows the latest value of
/// every series, and resting the pointer on the graph shows the values at that moment.
/// </para>
/// <para>
/// When the game gives no range, the vertical axis is fitted to the data and rounded up to a
/// friendly number, so the lines do not hug the top edge.
/// </para>
/// </remarks>
public sealed class GraphWidgetControl : Control
{
  /// <summary>The height of a graph when the game asks for none.</summary>
  public const double DefaultHeight = 72;

  private static readonly Typeface _typeface = new(FontFamily.Default);
  private static readonly uint[] _palette =
      [0xFF4C9AFF, 0xFFE3B341, 0xFF3FB950, 0xFFFF7B72, 0xFFA57CFF, 0xFF56D4DD];

  private readonly UiWidget _widget;
  private readonly Pen _borderPen = new(Brushes.Gray, 1);
  private IBrush _background = Brushes.Transparent;
  private IBrush _text = Brushes.Gray;
  private double _hoverX = double.NaN;

  /// <summary>
  /// Creates the control and binds it to the widget.
  /// </summary>
  /// <param name="widget">The graph widget.</param>
  public GraphWidgetControl(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    _widget = widget;
    MinHeight = DefaultHeight;
    MinWidth = 60;
    ClipToBounds = true;
    widget.Changed += (_, _) => InvalidateVisual();
    ActualThemeVariantChanged += (_, _) =>
    {
      ResolveBrushes();
      InvalidateVisual();
    };
  }

  /// <inheritdoc/>
  public override void Render(DrawingContext context)
  {
    ArgumentNullException.ThrowIfNull(context);
    base.Render(context);
    var width = Bounds.Width;
    var height = Bounds.Height;
    context.DrawRectangle(
        _background,
        _borderPen,
        new Rect(0.5, 0.5, Math.Max(0, width - 1), Math.Max(0, height - 1)),
        3,
        3);
    var series = _widget.Get(WidgetProp.Series).Items;
    var samples = _widget.Get(WidgetProp.Samples).Items;
    var limit = Math.Max(2, UiPatch.LimitOf(WidgetType.Graph, Props()));
    if (series.Count == 0 || width < 4)
    {
      DrawTitle(context, series, samples, -1);
      return;
    }

    var (min, max) = Range(samples, series.Count);
    const double TitleSpace = 18;
    var plot = Math.Max(1, height - TitleSpace - 2);
    for (var s = 0; s < series.Count; s++)
    {
      var pen = new Pen(new SolidColorBrush(ColorOf(series[s], s)), 1.5);
      var previous = default(Point);
      var started = false;
      for (var i = 0; i < samples.Count; i++)
      {
        var value = samples[i][s];
        if (!value.IsNumber || double.IsNaN(value.AsDouble()))
        {
          started = false;
          continue;
        }

        var ratio = max > min ? Math.Clamp((value.AsDouble() - min) / (max - min), 0, 1) : 0;
        var point = new Point(
            GraphScale.MapX(i, samples.Count, width, limit), TitleSpace + ((1 - ratio) * plot));
        if (started)
        {
          context.DrawLine(pen, previous, point);
        }

        previous = point;
        started = true;
      }
    }

    var scale = Label(Format(max), _text);
    context.DrawText(scale, new Point(width - scale.Width - 4, 2));
    var hover = -1;
    if (!double.IsNaN(_hoverX) && samples.Count > 0)
    {
      hover = GraphScale.IndexAt(_hoverX, samples.Count, width, limit);
      var x = GraphScale.MapX(hover, samples.Count, width, limit);
      context.DrawLine(_borderPen, new Point(x, TitleSpace), new Point(x, height));
    }

    DrawTitle(context, series, samples, hover);
  }

  /// <inheritdoc/>
  protected override void OnPointerMoved(PointerEventArgs e)
  {
    ArgumentNullException.ThrowIfNull(e);
    base.OnPointerMoved(e);
    _hoverX = e.GetPosition(this).X;
    InvalidateVisual();
  }

  /// <inheritdoc/>
  protected override void OnPointerExited(PointerEventArgs e)
  {
    base.OnPointerExited(e);
    _hoverX = double.NaN;
    InvalidateVisual();
  }

  /// <inheritdoc/>
  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    ResolveBrushes();
  }

  private Dictionary<WidgetProp, UiValue> Props()
  {
    var props = new Dictionary<WidgetProp, UiValue>();
    if (_widget.Has(WidgetProp.Limit))
    {
      props[WidgetProp.Limit] = _widget.Get(WidgetProp.Limit);
    }

    return props;
  }

  // Fits the axis to the data unless the game fixed MIN or MAX.
  private (double Min, double Max) Range(IReadOnlyList<UiValue> samples, int count)
  {
    var low = double.PositiveInfinity;
    var high = double.NegativeInfinity;
    foreach (var sample in samples)
    {
      for (var s = 0; s < count; s++)
      {
        var value = sample[s];
        if (value.IsNumber && !double.IsNaN(value.AsDouble()))
        {
          low = Math.Min(low, value.AsDouble());
          high = Math.Max(high, value.AsDouble());
        }
      }
    }

    if (double.IsInfinity(low))
    {
      low = 0;
      high = 1;
    }

    var min = _widget.GetDouble(WidgetProp.Min, double.NaN);
    var max = _widget.GetDouble(WidgetProp.Max, double.NaN);
    if (double.IsNaN(min))
    {
      min = low >= 0 ? 0 : low;
    }

    if (double.IsNaN(max))
    {
      max = GraphScale.NiceCeil((high - min) * GraphScale.Headroom) + min;
      if (!(max > min))
      {
        max = min + 1;
      }
    }

    return (min, max);
  }

  private void DrawTitle(
      DrawingContext context, IReadOnlyList<UiValue> series, IReadOnlyList<UiValue> samples, int at)
  {
    var x = 4.0;
    var title = _widget.GetString(WidgetProp.Text);
    if (title.Length > 0)
    {
      var text = Label(title, _text);
      context.DrawText(text, new Point(x, 2));
      x += text.Width + 8;
    }

    var index = at >= 0 ? at : samples.Count - 1;
    for (var s = 0; s < series.Count; s++)
    {
      var name = series[s][0].AsString();
      var unit = series[s][2].AsString();
      var value = index >= 0 ? samples[index][s] : UiValue.Null;
      var shown = value.IsNumber ? Format(value.AsDouble()) : "-";
      var text = Label(
          unit.Length > 0 ? $"{name} {shown} {unit}" : $"{name} {shown}",
          new SolidColorBrush(ColorOf(series[s], s)));
      context.DrawText(text, new Point(x, 2));
      x += text.Width + 8;
    }
  }

  private void ResolveBrushes()
  {
    _background = Find("GraphBackgroundBrush", Brushes.Transparent);
    _text = Find("MutedTextBrush", Brushes.Gray);
    _borderPen.Brush = Find("GraphBorderBrush", Brushes.Gray);
  }

  private IBrush Find(string key, IBrush fallback) =>
      this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
          ? brush
          : fallback;

  private static Color ColorOf(UiValue series, int index)
  {
    var argb = (uint)series[1].AsInt();
    return Color.FromUInt32(argb != 0 ? argb : _palette[index % _palette.Length]);
  }

  private static string Format(double value) => Math.Abs(value) >= 100
      ? value.ToString("0", CultureInfo.InvariantCulture)
      : value.ToString("0.##", CultureInfo.InvariantCulture);

  private static FormattedText Label(string text, IBrush brush) =>
      new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 11, brush);
}
