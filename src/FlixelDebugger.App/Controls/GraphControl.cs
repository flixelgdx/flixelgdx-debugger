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
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Graphs;

namespace FlixelDebugger.App.Controls;

/// <summary>
/// Draws one performance graph: a line of the last 120 samples with the scale in the top-right
/// corner and the hovered value in the top-left.
/// </summary>
/// <remarks>
/// <para>
/// The control is a plain <see cref="Control"/> with its own <see cref="Render"/> override.
/// Everything it has to decide (the series, the scale, the label texts) comes from its
/// <see cref="GraphViewModel"/>, so this class only turns numbers into pixels.
/// </para>
/// <para>
/// <b>No allocation while drawing.</b> The series is copied into one buffer that the control owns
/// for its whole life, the line is drawn as short segments between <see cref="Point"/> structs, and
/// the two text labels are cached and rebuilt only when their text changes.
/// </para>
/// </remarks>
public sealed class GraphControl : Control
{
  /// <summary>The height of a graph in pixels.</summary>
  public const double GraphHeight = 52;

  /// <summary>Defines the <see cref="Graph"/> property.</summary>
  public static readonly StyledProperty<GraphViewModel?> GraphProperty =
      AvaloniaProperty.Register<GraphControl, GraphViewModel?>(nameof(Graph));

  private static readonly Typeface _typeface = new(FontFamily.Default);

  private readonly double[] _buffer = new double[StatsHistory.Capacity];
  private readonly Pen _linePen = new(Brushes.Gray, 1.5);
  private readonly Pen _borderPen = new(Brushes.Gray, 1);
  private IBrush _background = Brushes.Transparent;
  private IBrush _accent = Brushes.DodgerBlue;
  private IBrush _muted = Brushes.Gray;
  private FormattedText? _scaleLabel;
  private string _scaleLabelText = string.Empty;
  private FormattedText? _hoverLabel;
  private string _hoverLabelText = string.Empty;
  private double _hoverX = double.NaN;

  static GraphControl()
  {
    AffectsRender<GraphControl>(GraphProperty);
    GraphProperty.Changed.AddClassHandler<GraphControl>(
        (control, e) =>
            control.OnGraphChanged(e.OldValue as GraphViewModel, e.NewValue as GraphViewModel));
  }

  /// <summary>
  /// Creates the control.
  /// </summary>
  public GraphControl()
  {
    Height = GraphHeight;
    MinWidth = 40;
    ClipToBounds = true;
    ActualThemeVariantChanged += OnThemeChanged;
  }

  /// <summary>Gets or sets the graph to draw.</summary>
  public GraphViewModel? Graph
  {
    get => GetValue(GraphProperty);
    set => SetValue(GraphProperty, value);
  }

  /// <inheritdoc/>
  public override void Render(DrawingContext context)
  {
    base.Render(context);
    var width = Bounds.Width;
    var height = Bounds.Height;
    context.DrawRectangle(
        _background,
        _borderPen,
        new Rect(0.5, 0.5, Math.Max(0, width - 1), Math.Max(0, height - 1)),
        3,
        3);
    if (Graph is not { } graph || width < 4)
    {
      return;
    }

    var count = graph.CopyTo(_buffer);
    var previous = default(Point);
    for (var i = 0; i < count; i++)
    {
      var point = new Point(
          GraphScale.MapX(i, count, width), graph.Scaler.MapY(_buffer[i], height));
      if (i > 0)
      {
        context.DrawLine(_linePen, previous, point);
      }

      previous = point;
    }

    DrawScaleLabel(context, graph, width);
    if (!double.IsNaN(_hoverX) && count > 0)
    {
      DrawHover(context, graph, count, width);
    }
  }

  /// <inheritdoc/>
  protected override void OnPointerMoved(PointerEventArgs e)
  {
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

  private void OnThemeChanged(object? sender, EventArgs e)
  {
    ResolveBrushes();
    _scaleLabel = null;
    _hoverLabel = null;
    InvalidateVisual();
  }

  private void OnGraphChanged(GraphViewModel? oldGraph, GraphViewModel? newGraph)
  {
    if (oldGraph is not null)
    {
      oldGraph.Invalidated -= OnInvalidated;
    }

    if (newGraph is not null)
    {
      newGraph.Invalidated += OnInvalidated;
    }

    InvalidateVisual();
  }

  private void OnInvalidated(object? sender, EventArgs e) => InvalidateVisual();

  // Looks the colors up in the current theme, so the graph follows light and dark mode.
  private void ResolveBrushes()
  {
    _background = Find("GraphBackgroundBrush", Brushes.Transparent);
    _muted = Find("GraphLineBrush", Brushes.Gray);
    _accent = Find("SystemControlHighlightAccentBrush", Brushes.DodgerBlue);
    _linePen.Brush = _muted;
    _borderPen.Brush = Find("GraphBorderBrush", Brushes.Gray);
  }

  private IBrush Find(string key, IBrush fallback) =>
      this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
          ? brush
          : fallback;

  private void DrawScaleLabel(DrawingContext context, GraphViewModel graph, double width)
  {
    var text = graph.ScaleText;
    if (_scaleLabel is null || text != _scaleLabelText)
    {
      _scaleLabelText = text;
      _scaleLabel = Label(text, _muted);
    }

    context.DrawText(_scaleLabel, new Point(width - _scaleLabel.Width - 4, 2));
  }

  private void DrawHover(DrawingContext context, GraphViewModel graph, int count, double width)
  {
    var index = GraphScale.IndexAt(_hoverX, count, width);
    if (index < 0)
    {
      return;
    }

    var x = GraphScale.MapX(index, count, width);
    context.DrawLine(_borderPen, new Point(x, 0), new Point(x, Bounds.Height));
    var text = graph.FormatValue(_buffer[index]);
    if (_hoverLabel is null || text != _hoverLabelText)
    {
      _hoverLabelText = text;
      _hoverLabel = Label(text, _accent);
    }

    context.DrawText(_hoverLabel, new Point(4, 2));
  }

  private static FormattedText Label(string text, IBrush brush) =>
      new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 11, brush);
}
