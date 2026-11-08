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
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Controls;

/// <summary>
/// Draws the viewport map: the camera's view as a letterboxed rectangle with one outline per
/// object, and passes the mouse to its <see cref="ViewportPanelViewModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="GraphControl"/>, this is a plain <see cref="Control"/> with its own
/// <see cref="Render"/> override. Every decision (where the view sits, which box is dragged, what
/// a click means) belongs to the ViewModel, so this class only turns boxes into pixels and pointer
/// events into calls.
/// </para>
/// <para>
/// <b>Look.</b> A plain box is a thin outline. A group is dashed, an inactive object is muted, the
/// box under the pointer is drawn thicker, and the selected object is outlined in the accent color
/// and lightly filled, on top of everything else. The colors come from the theme, so the map
/// follows light and dark mode.
/// </para>
/// <para>
/// <b>No allocation while drawing.</b> The pens are made once and only get new brushes when the
/// theme changes, and the hover label is cached until its text changes.
/// </para>
/// </remarks>
public sealed class ViewportControl : Control
{
  /// <summary>Defines the <see cref="Viewport"/> property.</summary>
  public static readonly StyledProperty<ViewportPanelViewModel?> ViewportProperty =
      AvaloniaProperty.Register<ViewportControl, ViewportPanelViewModel?>(nameof(Viewport));

  private static readonly Typeface _typeface = new(FontFamily.Default);

  private readonly Pen _borderPen = new(Brushes.Gray, 1);
  private readonly Pen _boxPen = new(Brushes.Gray, 1);
  private readonly Pen _groupPen = new(Brushes.Gray, 1, DashStyle.Dash);
  private readonly Pen _inactivePen = new(Brushes.Gray, 1);
  private readonly Pen _hoverPen = new(Brushes.Gray, 2);
  private readonly Pen _selectedPen = new(Brushes.Gold, 2);
  private readonly Pen _agentPen = new(Brushes.MediumPurple, 3);
  private IBrush _background = Brushes.Transparent;
  private IBrush _view = Brushes.Transparent;
  private IBrush _selectedFill = Brushes.Transparent;
  private IBrush _labelBackground = Brushes.Black;
  private IBrush _labelText = Brushes.White;
  private IBrush _agentFill = Brushes.Transparent;
  private FormattedText? _hoverLabel;
  private string _hoverLabelText = string.Empty;
  private ViewportButton? _pressed;

  static ViewportControl()
  {
    AffectsRender<ViewportControl>(ViewportProperty);
    ViewportProperty.Changed.AddClassHandler<ViewportControl>(
        (control, e) => control.OnViewportChanged(
            e.OldValue as ViewportPanelViewModel, e.NewValue as ViewportPanelViewModel));
  }

  /// <summary>
  /// Creates the control.
  /// </summary>
  public ViewportControl()
  {
    ClipToBounds = true;
    MinWidth = 80;
    MinHeight = 60;
    ActualThemeVariantChanged += OnThemeChanged;
  }

  /// <summary>Gets or sets the panel whose map is drawn.</summary>
  public ViewportPanelViewModel? Viewport
  {
    get => GetValue(ViewportProperty);
    set => SetValue(ViewportProperty, value);
  }

  /// <inheritdoc/>
  public override void Render(DrawingContext context)
  {
    base.Render(context);
    context.FillRectangle(_background, new Rect(Bounds.Size));
    if (Viewport is not { Payload: { } payload } viewport || viewport.Fit.IsEmpty)
    {
      return;
    }

    var fit = viewport.Fit;
    var view = new Rect(fit.Left, fit.Top, fit.Width, fit.Height);
    context.DrawRectangle(_view, _borderPen, view);
    using (context.PushClip(view))
    {
      DrawBoxes(context, viewport, payload, fit);
    }

    DrawHover(context, viewport);
  }

  /// <inheritdoc/>
  protected override void OnSizeChanged(SizeChangedEventArgs e)
  {
    base.OnSizeChanged(e);
    Viewport?.Resize(e.NewSize.Width, e.NewSize.Height);
  }

  /// <inheritdoc/>
  protected override void OnPointerPressed(PointerPressedEventArgs e)
  {
    base.OnPointerPressed(e);
    var point = e.GetCurrentPoint(this);
    ViewportButton? button = point.Properties.PointerUpdateKind switch
    {
      PointerUpdateKind.LeftButtonPressed => ViewportButton.Left,
      PointerUpdateKind.MiddleButtonPressed => ViewportButton.Middle,
      PointerUpdateKind.RightButtonPressed => ViewportButton.Right,
      _ => null,
    };
    if (button is not { } pressed || _pressed is not null || Viewport is not { } viewport)
    {
      return;
    }

    _pressed = pressed;
    e.Pointer.Capture(this);
    viewport.Press(pressed, point.Position.X, point.Position.Y, e.ClickCount);
    e.Handled = true;
  }

  /// <inheritdoc/>
  protected override void OnPointerMoved(PointerEventArgs e)
  {
    base.OnPointerMoved(e);
    var position = e.GetPosition(this);
    Viewport?.Move(position.X, position.Y);
  }

  /// <inheritdoc/>
  protected override void OnPointerReleased(PointerReleasedEventArgs e)
  {
    base.OnPointerReleased(e);
    ViewportButton? button = e.InitialPressMouseButton switch
    {
      MouseButton.Left => ViewportButton.Left,
      MouseButton.Middle => ViewportButton.Middle,
      MouseButton.Right => ViewportButton.Right,
      _ => null,
    };
    if (_pressed is null || button != _pressed)
    {
      return;
    }

    _pressed = null;
    Viewport?.Release();
    e.Pointer.Capture(null);
    e.Handled = true;
  }

  /// <inheritdoc/>
  protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
  {
    base.OnPointerCaptureLost(e);
    _pressed = null;
    Viewport?.Cancel();
  }

  /// <inheritdoc/>
  protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
  {
    base.OnPointerWheelChanged(e);
    if (Viewport is not { } viewport)
    {
      return;
    }

    var position = e.GetPosition(this);
    viewport.Wheel(e.Delta.Y, position.X, position.Y);
    e.Handled = true;
  }

  /// <inheritdoc/>
  protected override void OnPointerExited(PointerEventArgs e)
  {
    base.OnPointerExited(e);
    if (_pressed is null)
    {
      Viewport?.Leave();
    }
  }

  /// <inheritdoc/>
  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    ResolveBrushes();
  }

  private static Rect ToRect(ViewportFit fit, ViewportBox box) =>
      new(
          fit.ToCanvasX(box.X),
          fit.ToCanvasY(box.Y),
          Math.Max(0, box.Width * fit.Scale),
          Math.Max(0, box.Height * fit.Scale));

  private void DrawBoxes(
      DrawingContext context,
      ViewportPanelViewModel viewport,
      ViewportPayload payload,
      ViewportFit fit)
  {
    var boxes = payload.Boxes;
    var selected = default(Rect);
    var hasSelected = false;
    var hovered = default(Rect);
    var hasHovered = false;
    var touched = default(Rect);
    var hasTouched = false;
    for (var i = 0; i < boxes.Count; i++)
    {
      var box = viewport.Shown(boxes[i]);
      var rect = ToRect(fit, box);
      if (box.Id == viewport.AgentObjectId)
      {
        touched = rect;
        hasTouched = true;
      }

      if (box.Id == payload.SelectedId || box.IsSelected)
      {
        // Drawn last, so no other outline covers it.
        selected = rect;
        hasSelected = true;
        continue;
      }

      if (box.Id == viewport.HoveredId)
      {
        hovered = rect;
        hasHovered = true;
      }

      var pen = box.IsInactive ? _inactivePen : box.IsGroup ? _groupPen : _boxPen;
      context.DrawRectangle(null, pen, rect);
    }

    if (hasHovered)
    {
      context.DrawRectangle(null, _hoverPen, hovered);
    }

    if (hasSelected)
    {
      context.DrawRectangle(_selectedFill, _selectedPen, selected);
    }

    // The agent's outline is drawn over the selection and has its own color, so the person can
    // tell what the agent touched from what they selected.
    if (hasTouched)
    {
      context.DrawRectangle(null, _agentPen, touched.Inflate(2));
    }

    if (viewport.HasAgentPointer)
    {
      DrawAgentPointer(context, new Point(
          fit.ToCanvasX(viewport.AgentPointerX), fit.ToCanvasY(viewport.AgentPointerY)));
    }
  }

  private void DrawAgentPointer(DrawingContext context, Point at)
  {
    context.DrawEllipse(_agentFill, _agentPen, at, 8, 8);
    context.DrawLine(_agentPen, new Point(at.X - 14, at.Y), new Point(at.X + 14, at.Y));
    context.DrawLine(_agentPen, new Point(at.X, at.Y - 14), new Point(at.X, at.Y + 14));
  }

  private void DrawHover(DrawingContext context, ViewportPanelViewModel viewport)
  {
    var text = viewport.HoverText;
    if (text.Length == 0 || double.IsNaN(viewport.HoverX))
    {
      return;
    }

    if (_hoverLabel is null || text != _hoverLabelText)
    {
      _hoverLabelText = text;
      _hoverLabel = new FormattedText(
          text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 11, _labelText);
    }

    // Next to the pointer, but always inside the control.
    var width = _hoverLabel.Width + 8;
    var height = _hoverLabel.Height + 4;
    var x = Math.Max(0, Math.Min(viewport.HoverX + 14, Bounds.Width - width));
    var y = Math.Max(0, Math.Min(viewport.HoverY + 14, Bounds.Height - height));
    context.DrawRectangle(_labelBackground, null, new Rect(x, y, width, height), 3, 3);
    context.DrawText(_hoverLabel, new Point(x + 4, y + 2));
  }

  private void OnThemeChanged(object? sender, EventArgs e)
  {
    ResolveBrushes();
    _hoverLabel = null;
    InvalidateVisual();
  }

  private void OnViewportChanged(
      ViewportPanelViewModel? oldViewport, ViewportPanelViewModel? newViewport)
  {
    if (oldViewport is not null)
    {
      oldViewport.Invalidated -= OnInvalidated;
    }

    _pressed = null;
    if (newViewport is not null)
    {
      newViewport.Invalidated += OnInvalidated;
      newViewport.Resize(Bounds.Width, Bounds.Height);
    }

    InvalidateVisual();
  }

  private void OnInvalidated(object? sender, EventArgs e) => InvalidateVisual();

  // Looks the colors up in the current theme, so the map follows light and dark mode.
  private void ResolveBrushes()
  {
    _background = Find("ViewportBackgroundBrush", Brushes.Transparent);
    _view = Find("ViewportViewBrush", Brushes.Transparent);
    _borderPen.Brush = Find("ViewportBorderBrush", Brushes.Gray);
    _boxPen.Brush = Find("ViewportBoxBrush", Brushes.Gray);
    _groupPen.Brush = Find("ViewportGroupBrush", Brushes.Gray);
    _inactivePen.Brush = Find("ViewportInactiveBrush", Brushes.Gray);
    _hoverPen.Brush = Find("SystemControlHighlightAccentBrush", Brushes.DodgerBlue);
    _selectedPen.Brush = Find("ViewportSelectedBrush", Brushes.Gold);
    _selectedFill = Find("ViewportSelectedFillBrush", Brushes.Transparent);
    _agentPen.Brush = Find("AgentBrush", Brushes.MediumPurple);
    _agentFill = Find("AgentFillBrush", Brushes.Transparent);
    _labelBackground = Find("ViewportLabelBackgroundBrush", Brushes.Black);
    _labelText = Find("ViewportLabelTextBrush", Brushes.White);
  }

  private IBrush Find(string key, IBrush fallback) =>
      this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
          ? brush
          : fallback;
}
