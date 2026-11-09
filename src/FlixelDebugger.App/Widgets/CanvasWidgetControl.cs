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
using Avalonia.Media.Imaging;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Draws a <see cref="WidgetType.Canvas"/> widget: boxes in a 2D view, such as a map of what a game
/// camera sees, that the person can click, drag, pan, and zoom.
/// </summary>
/// <remarks>
/// <para>
/// The view is scaled to fit the control and keeps its aspect ratio. Every gesture is turned into
/// view units before it is sent, so the game never has to know how big the app's window is.
/// </para>
/// <para>
/// While the person drags a shape, the control moves it right away and keeps it there until the
/// game sends new shapes; the game decides where the shape really ends up.
/// </para>
/// </remarks>
public sealed class CanvasWidgetControl : Control
{
  private const double ViewMargin = 8;
  private const double ClickSlop = 3;

  private static readonly Typeface _typeface = new(FontFamily.Default);

  private readonly UiWidget _widget;
  private readonly Pen _borderPen = new(Brushes.Gray, 1);
  private Shape[] _shapes = [];
  private Bitmap? _image;
  private ViewportFit _fit;
  private IBrush _background = Brushes.Transparent;
  private IBrush _view = Brushes.Transparent;
  private IBrush _normal = Brushes.Gray;
  private IBrush _selectedFill = Brushes.Transparent;
  private IBrush _labelBackground = Brushes.Black;
  private IBrush _labelText = Brushes.White;
  private Point _pressAt;
  private Point _last;
  private MouseButton _button = MouseButton.None;
  private long _dragId = -1;
  private double _dragX;
  private double _dragY;
  private bool _moved;
  private Point _hover = new(double.NaN, double.NaN);

  /// <summary>
  /// Creates the control and binds it to the widget.
  /// </summary>
  /// <param name="widget">The canvas widget.</param>
  public CanvasWidgetControl(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    _widget = widget;
    MinHeight = 160;
    ClipToBounds = true;
    Focusable = true;
    ReadShapes();
    ReadImage();
    widget.Changed += (_, key) =>
    {
      if (key == WidgetProp.Shapes)
      {
        ReadShapes();
      }
      else if (key == WidgetProp.Image)
      {
        ReadImage();
      }

      InvalidateVisual();
    };
    ActualThemeVariantChanged += (_, _) =>
    {
      ResolveBrushes();
      InvalidateVisual();
    };
  }

  private int Interact => _widget.GetInt(WidgetProp.Interact);

  /// <inheritdoc/>
  public override void Render(DrawingContext context)
  {
    ArgumentNullException.ThrowIfNull(context);
    base.Render(context);
    context.FillRectangle(_background, new Rect(Bounds.Size));
    var view = _widget.Get(WidgetProp.View);
    _fit = ViewportFit.Compute(
        Bounds.Width, Bounds.Height, view[0].AsDouble(), view[1].AsDouble(), ViewMargin);
    if (_fit.IsEmpty)
    {
      return;
    }

    var area = new Rect(_fit.Left, _fit.Top, _fit.Width, _fit.Height);
    context.DrawRectangle(_view, _borderPen, area);
    using (context.PushClip(area))
    {
      if (_image is not null)
      {
        context.DrawImage(_image, area);
      }

      foreach (var shape in _shapes)
      {
        DrawShape(context, shape);
      }
    }

    DrawLabel(context);
  }

  /// <inheritdoc/>
  protected override void OnPointerPressed(PointerPressedEventArgs e)
  {
    ArgumentNullException.ThrowIfNull(e);
    base.OnPointerPressed(e);
    var point = e.GetCurrentPoint(this);
    var button = point.Properties.PointerUpdateKind switch
    {
      PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
      PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
      PointerUpdateKind.RightButtonPressed => MouseButton.Right,
      _ => MouseButton.None,
    };
    if (button == MouseButton.None || _button != MouseButton.None || _fit.IsEmpty)
    {
      return;
    }

    _button = button;
    _pressAt = point.Position;
    _last = point.Position;
    _moved = false;
    _dragId = button == MouseButton.Left && (Interact & UiStyle.InteractDrag) != 0
        ? HitTest(point.Position)?.Id ?? -1
        : -1;
    _dragX = 0;
    _dragY = 0;
    e.Pointer.Capture(this);
    Focus();
    e.Handled = true;
  }

  /// <inheritdoc/>
  protected override void OnPointerMoved(PointerEventArgs e)
  {
    ArgumentNullException.ThrowIfNull(e);
    base.OnPointerMoved(e);
    var position = e.GetPosition(this);
    _hover = position;
    if (_button != MouseButton.None && !_fit.IsEmpty)
    {
      if (!_moved && Distance(position, _pressAt) > ClickSlop)
      {
        _moved = true;
      }

      if (_moved)
      {
        var dx = (position.X - _last.X) / _fit.Scale;
        var dy = (position.Y - _last.Y) / _fit.Scale;
        _last = position;
        if (_button == MouseButton.Left && _dragId >= 0)
        {
          _dragX += dx;
          _dragY += dy;
          _ = _widget.Send(
              WidgetEvent.Drag,
              UiValue.FromLong(_dragId),
              UiValue.FromFloat((float)dx),
              UiValue.FromFloat((float)dy));
        }
        else if (_button != MouseButton.Left && (Interact & UiStyle.InteractPan) != 0)
        {
          _ = _widget.Send(
              WidgetEvent.Pan, UiValue.FromFloat((float)dx), UiValue.FromFloat((float)dy));
        }
      }
    }

    InvalidateVisual();
  }

  /// <inheritdoc/>
  protected override void OnPointerReleased(PointerReleasedEventArgs e)
  {
    ArgumentNullException.ThrowIfNull(e);
    base.OnPointerReleased(e);
    if (_button == MouseButton.None || e.InitialPressMouseButton != _button)
    {
      return;
    }

    if (_button == MouseButton.Left && !_moved && (Interact & UiStyle.InteractPick) != 0 &&
        !_fit.IsEmpty)
    {
      _ = _widget.Send(
          WidgetEvent.Pick,
          UiValue.FromFloat((float)_fit.ToViewX(_pressAt.X)),
          UiValue.FromFloat((float)_fit.ToViewY(_pressAt.Y)));
    }

    _button = MouseButton.None;
    e.Pointer.Capture(null);
    e.Handled = true;
  }

  /// <inheritdoc/>
  protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
  {
    base.OnPointerCaptureLost(e);
    _button = MouseButton.None;
  }

  /// <inheritdoc/>
  protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
  {
    ArgumentNullException.ThrowIfNull(e);
    base.OnPointerWheelChanged(e);
    if ((Interact & UiStyle.InteractZoom) == 0 || _fit.IsEmpty || e.Delta.Y == 0)
    {
      return;
    }

    var position = e.GetPosition(this);
    var factor = e.Delta.Y > 0 ? 1.25f : 0.8f;
    _ = _widget.Send(
        WidgetEvent.Zoom,
        UiValue.FromFloat(factor),
        UiValue.FromFloat((float)_fit.ToViewX(position.X)),
        UiValue.FromFloat((float)_fit.ToViewY(position.Y)));
    e.Handled = true;
  }

  /// <inheritdoc/>
  protected override void OnPointerExited(PointerEventArgs e)
  {
    base.OnPointerExited(e);
    _hover = new Point(double.NaN, double.NaN);
    InvalidateVisual();
  }

  /// <inheritdoc/>
  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    ResolveBrushes();
  }

  private static double Distance(Point a, Point b) =>
      Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

  private void ReadShapes()
  {
    var items = _widget.Get(WidgetProp.Shapes).Items;
    var shapes = new Shape[items.Count];
    for (var i = 0; i < shapes.Length; i++)
    {
      var item = items[i];
      shapes[i] = new Shape(
          item[0].AsLong(-1),
          item[1].AsDouble(),
          item[2].AsDouble(),
          item[3].AsDouble(),
          item[4].AsDouble(),
          item[5].AsInt(),
          item[6].AsString());
    }

    _shapes = shapes;

    // The game's new shapes include the result of the drag so far.
    _dragX = 0;
    _dragY = 0;
  }

  private void ReadImage()
  {
    _image = _widget.Get(WidgetProp.Image).AsBytes() is { } png ? WidgetFactory.Decode(png) : null;
    RenderOptions.SetBitmapInterpolationMode(
        this,
        _widget.GetBool(WidgetProp.Smooth)
            ? BitmapInterpolationMode.HighQuality
            : BitmapInterpolationMode.None);
  }

  private Rect RectOf(Shape shape)
  {
    var x = shape.X;
    var y = shape.Y;
    if (shape.Id == _dragId && _button == MouseButton.Left)
    {
      x += _dragX;
      y += _dragY;
    }

    return new Rect(
        _fit.ToCanvasX(x),
        _fit.ToCanvasY(y),
        Math.Max(0, shape.Width * _fit.Scale),
        Math.Max(0, shape.Height * _fit.Scale));
  }

  private Shape? HitTest(Point point)
  {
    // The last shape is drawn on top, so it wins.
    for (var i = _shapes.Length - 1; i >= 0; i--)
    {
      if (RectOf(_shapes[i]).Inflate(2).Contains(point))
      {
        return _shapes[i];
      }
    }

    return null;
  }

  private void DrawShape(DrawingContext context, Shape shape)
  {
    var tone = UiStyle.ToneOf(shape.Style);
    var brush = WidgetStyle.BrushOf(this, tone, _normal);
    var selected = UiStyle.Has(shape.Style, UiStyle.ShapeSelected);
    var pen = new Pen(brush, selected ? 2.5 : 1)
    {
      DashStyle = UiStyle.Has(shape.Style, UiStyle.ShapeDashed) ? DashStyle.Dash : null,
    };
    IBrush? fill = null;
    if (UiStyle.Has(shape.Style, UiStyle.ShapeFilled))
    {
      fill = brush;
    }
    else if (selected)
    {
      fill = _selectedFill;
    }

    using (context.PushOpacity(UiStyle.Has(shape.Style, UiStyle.ShapeDim) ? 0.45 : 1))
    {
      context.DrawRectangle(fill, pen, RectOf(shape));
    }
  }

  private void DrawLabel(DrawingContext context)
  {
    if (double.IsNaN(_hover.X) || HitTest(_hover) is not { Label.Length: > 0 } shape)
    {
      return;
    }

    var text = new FormattedText(
        shape.Label,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        _typeface,
        11,
        _labelText);

    // Next to the pointer, but always inside the control.
    var width = text.Width + 8;
    var height = text.Height + 4;
    var x = Math.Max(0, Math.Min(_hover.X + 14, Bounds.Width - width));
    var y = Math.Max(0, Math.Min(_hover.Y + 14, Bounds.Height - height));
    context.DrawRectangle(_labelBackground, null, new Rect(x, y, width, height), 3, 3);
    context.DrawText(text, new Point(x + 4, y + 2));
  }

  // Looks the colors up in the current theme, so the map follows light and dark mode.
  private void ResolveBrushes()
  {
    _background = Find("ViewportBackgroundBrush", Brushes.Transparent);
    _view = Find("ViewportViewBrush", Brushes.Transparent);
    _borderPen.Brush = Find("ViewportBorderBrush", Brushes.Gray);
    _normal = Find("ViewportBoxBrush", Brushes.Gray);
    _selectedFill = Find("ViewportSelectedFillBrush", Brushes.Transparent);
    _labelBackground = Find("ViewportLabelBackgroundBrush", Brushes.Black);
    _labelText = Find("ViewportLabelTextBrush", Brushes.White);
  }

  private IBrush Find(string key, IBrush fallback) =>
      this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
          ? brush
          : fallback;

  private readonly record struct Shape(
      long Id, double X, double Y, double Width, double Height, int Style, string Label);
}
