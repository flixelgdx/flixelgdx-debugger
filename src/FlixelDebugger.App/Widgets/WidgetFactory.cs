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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FlixelDebugger.App.Converters;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Builds the Avalonia control for each widget of a panel and keeps it in step with the widget.
/// </summary>
/// <remarks>
/// <para>
/// Every widget type has one small builder here (or in a partner class for the bigger ones, such
/// as <see cref="TableWidgetView"/>). A builder creates the control once, copies the widget's
/// properties into it, and subscribes to <see cref="UiWidget.Changed"/> so later patches update the
/// same control. When the person uses the control, the builder sends the matching event with
/// <see cref="UiWidget.Send"/> and also records the new value in the widget, so a value the game
/// refuses (and sends back unchanged) still shows up as a change.
/// </para>
/// <para>
/// One factory serves one panel. It remembers the control of every widget, so a container that
/// gets new children reuses the controls of the children it already had.
/// </para>
/// </remarks>
public sealed class WidgetFactory
{
  private const double DefaultSpacing = 6;

  private readonly Dictionary<UiWidget, Control> _controls = [];

  /// <summary>
  /// Returns the control of a widget, creating it the first time.
  /// </summary>
  /// <param name="widget">The widget.</param>
  /// <returns>The control.</returns>
  public Control Get(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    if (_controls.TryGetValue(widget, out var existing))
    {
      Detach(existing);
      return existing;
    }

    var control = Create(widget);
    _controls[widget] = control;
    return control;
  }

  /// <summary>Forgets every control, for example after the panel got a new root widget.</summary>
  public void Clear() => _controls.Clear();

  // Takes a reused control out of its old parent, because a control can only have one.
  internal static void Detach(Control control)
  {
    switch (control.Parent)
    {
      case Panel panel:
        panel.Children.Remove(control);
        break;
      case ContentControl content when ReferenceEquals(content.Content, control):
        content.Content = null;
        break;
      case Decorator decorator when ReferenceEquals(decorator.Child, control):
        decorator.Child = null;
        break;
    }
  }

  private Control Create(UiWidget widget)
  {
    var styled = true;
    Control control;
    switch (widget.Type)
    {
      case WidgetType.Stack:
        control = Stack(widget);
        styled = false;
        break;
      case WidgetType.Wrap:
        control = Wrap(widget);
        styled = false;
        break;
      case WidgetType.Grid:
        control = GridOf(widget);
        styled = false;
        break;
      case WidgetType.Split:
        control = Split(widget);
        styled = false;
        break;
      case WidgetType.Tabs:
        control = Tabs(widget);
        styled = false;
        break;
      case WidgetType.Scroll:
        control = Scroll(widget);
        styled = false;
        break;
      case WidgetType.Expander:
        control = ExpanderOf(widget);
        break;
      case WidgetType.Label:
        control = Label(widget);
        break;
      case WidgetType.Button:
        control = ButtonOf(widget);
        break;
      case WidgetType.Checkbox:
        control = Toggle(widget, new CheckBox());
        break;
      case WidgetType.Toggle:
        control = Toggle(widget, new ToggleButton());
        break;
      case WidgetType.Dropdown:
        control = Dropdown(widget);
        break;
      case WidgetType.TextBox:
        control = new TextBoxWidgetView(widget).Root;
        break;
      case WidgetType.Number:
        control = Number(widget);
        break;
      case WidgetType.Slider:
        control = SliderOf(widget);
        break;
      case WidgetType.Progress:
        control = Progress(widget);
        break;
      case WidgetType.Image:
        control = ImageOf(widget);
        break;
      case WidgetType.Separator:
        control = new Separator { Margin = new Thickness(0, 2) };
        break;
      case WidgetType.Table:
        control = new TableWidgetView(widget).Root;
        break;
      case WidgetType.Tree:
        control = new TreeWidgetView(widget).Root;
        break;
      case WidgetType.Properties:
        control = new PropertiesWidgetView(widget).Root;
        break;
      case WidgetType.Graph:
        control = new GraphWidgetControl(widget);
        break;
      case WidgetType.Canvas:
        control = new CanvasWidgetControl(widget);
        break;
      case WidgetType.Color:
        control = ColorOf(widget);
        break;
      default:
        // A widget type from a newer protocol: draw nothing, together with its children.
        return new Panel { IsVisible = false };
    }

    WidgetStyle.Bind(control, widget, styled);
    return control;
  }

  private Grid Stack(UiWidget widget)
  {
    var grid = new Grid();
    void Layout()
    {
      var horizontal = widget.GetBool(WidgetProp.Horizontal);
      var spacing = widget.GetDouble(WidgetProp.Spacing, DefaultSpacing);
      grid.Children.Clear();
      grid.RowDefinitions.Clear();
      grid.ColumnDefinitions.Clear();
      grid.RowSpacing = horizontal ? 0 : spacing;
      grid.ColumnSpacing = horizontal ? spacing : 0;
      var index = 0;
      foreach (var child in widget.Children)
      {
        // A hidden child gets no row at all, so it leaves no gap behind.
        if (!child.IsVisible)
        {
          continue;
        }

        var size = child.Grow > 0 ? new GridLength(child.Grow, GridUnitType.Star) : GridLength.Auto;
        var control = Get(child);
        if (horizontal)
        {
          grid.ColumnDefinitions.Add(new ColumnDefinition(size));
          Grid.SetColumn(control, index);
          Grid.SetRow(control, 0);
        }
        else
        {
          grid.RowDefinitions.Add(new RowDefinition(size));
          Grid.SetRow(control, index);
          Grid.SetColumn(control, 0);
        }

        grid.Children.Add(control);
        index++;
      }
    }

    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Horizontal or WidgetProp.Spacing)
      {
        Layout();
      }
    };
    WatchChildren(widget, Layout, WidgetProp.Visible, WidgetProp.Grow);
    return grid;
  }

  private WrapPanel Wrap(UiWidget widget)
  {
    var panel = new WrapPanel();
    void Layout()
    {
      var spacing = widget.GetDouble(WidgetProp.Spacing, DefaultSpacing);
      panel.ItemSpacing = spacing;
      panel.LineSpacing = spacing;
      panel.Children.Clear();
      foreach (var child in widget.Children)
      {
        var control = Get(child);
        control.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(control);
      }
    }

    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    widget.Changed += (_, key) =>
    {
      if (key == WidgetProp.Spacing)
      {
        Layout();
      }
    };
    return panel;
  }

  private Grid GridOf(UiWidget widget)
  {
    var grid = new Grid();
    void Layout()
    {
      var spacing = widget.GetDouble(WidgetProp.Spacing, DefaultSpacing);
      grid.RowSpacing = spacing;
      grid.ColumnSpacing = spacing;
      grid.Children.Clear();
      grid.ColumnDefinitions.Clear();
      grid.RowDefinitions.Clear();
      var columns = widget.Get(WidgetProp.Columns).Items;
      foreach (var column in columns)
      {
        grid.ColumnDefinitions.Add(new ColumnDefinition(ParseLength(column.AsString("*"))));
      }

      var count = Math.Max(1, grid.ColumnDefinitions.Count);
      if (grid.ColumnDefinitions.Count == 0)
      {
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
      }

      for (var i = 0; i < widget.Children.Count; i++)
      {
        if (i % count == 0)
        {
          grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        var control = Get(widget.Children[i]);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(control, i / count);
        Grid.SetColumn(control, i % count);
        grid.Children.Add(control);
      }
    }

    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Columns or WidgetProp.Spacing)
      {
        Layout();
      }
    };
    return grid;
  }

  private Grid Split(UiWidget widget)
  {
    var grid = new Grid();
    var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Auto };
    var updating = false;
    void Layout()
    {
      updating = true;
      var horizontal = widget.GetBool(WidgetProp.Horizontal);
      var share = Math.Clamp(widget.GetDouble(WidgetProp.Value, 0.5), 0.05, 0.95);
      grid.Children.Clear();
      grid.RowDefinitions.Clear();
      grid.ColumnDefinitions.Clear();
      var first = new GridLength(share, GridUnitType.Star);
      var second = new GridLength(1 - share, GridUnitType.Star);
      var parts = widget.Children;
      if (horizontal)
      {
        grid.ColumnDefinitions.Add(new ColumnDefinition(first));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(5)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(second));
      }
      else
      {
        grid.RowDefinitions.Add(new RowDefinition(first));
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(5)));
        grid.RowDefinitions.Add(new RowDefinition(second));
      }

      for (var i = 0; i < Math.Min(2, parts.Count); i++)
      {
        var control = Get(parts[i]);
        Place(control, horizontal, i * 2);
        grid.Children.Add(control);
      }

      Place(splitter, horizontal, 1);
      grid.Children.Add(splitter);
      updating = false;
    }

    splitter.DragCompleted += (_, _) =>
    {
      if (updating)
      {
        return;
      }

      var horizontal = widget.GetBool(WidgetProp.Horizontal);
      double a, b;
      if (horizontal)
      {
        a = grid.ColumnDefinitions[0].ActualWidth;
        b = grid.ColumnDefinitions[2].ActualWidth;
      }
      else
      {
        a = grid.RowDefinitions[0].ActualHeight;
        b = grid.RowDefinitions[2].ActualHeight;
      }

      if (a + b > 0)
      {
        var share = UiValue.FromDouble(a / (a + b));
        widget.Set(WidgetProp.Value, share);
        _ = widget.Send(WidgetEvent.Change, share);
      }
    };
    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Horizontal or WidgetProp.Value)
      {
        Layout();
      }
    };
    return grid;
  }

  private TabControl Tabs(UiWidget widget)
  {
    var tabs = new TabControl();
    var updating = false;
    void Layout()
    {
      updating = true;
      tabs.Items.Clear();
      var titles = widget.Get(WidgetProp.Items).Items;
      for (var i = 0; i < widget.Children.Count; i++)
      {
        var page = Get(widget.Children[i]);
        var title = i < titles.Count ? titles[i].AsString() : string.Empty;
        tabs.Items.Add(new TabItem { Header = title, Content = page });
      }

      tabs.SelectedIndex = Math.Clamp(widget.GetInt(WidgetProp.Value), 0, tabs.ItemCount - 1);
      updating = false;
    }

    tabs.SelectionChanged += (_, _) =>
    {
      if (updating || tabs.SelectedIndex < 0)
      {
        return;
      }

      var value = UiValue.FromInt(tabs.SelectedIndex);
      widget.Set(WidgetProp.Value, value);
      _ = widget.Send(WidgetEvent.Change, value);
    };
    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    widget.Changed += (_, key) =>
    {
      if (key == WidgetProp.Items)
      {
        Layout();
      }
      else if (key == WidgetProp.Value)
      {
        updating = true;
        tabs.SelectedIndex = Math.Clamp(widget.GetInt(WidgetProp.Value), 0, tabs.ItemCount - 1);
        updating = false;
      }
    };
    return tabs;
  }

  private ScrollViewer Scroll(UiWidget widget)
  {
    var viewer = new ScrollViewer
    {
      HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
      VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };
    void Layout() => viewer.Content = widget.Children.Count > 0 ? Get(widget.Children[0]) : null;
    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    return viewer;
  }

  private Expander ExpanderOf(UiWidget widget)
  {
    var expander = new Expander { HorizontalAlignment = HorizontalAlignment.Stretch };
    var updating = false;
    void Update()
    {
      updating = true;
      expander.Header = widget.GetString(WidgetProp.Text);
      expander.IsExpanded = widget.GetBool(WidgetProp.Expanded, true);
      updating = false;
    }

    void Layout() => expander.Content = widget.Children.Count > 0 ? Get(widget.Children[0]) : null;
    expander.PropertyChanged += (_, e) =>
    {
      if (updating || e.Property != Expander.IsExpandedProperty)
      {
        return;
      }

      var value = UiValue.FromBool(expander.IsExpanded);
      widget.Set(WidgetProp.Expanded, value);
      _ = widget.Send(WidgetEvent.Change, value);
    };
    Update();
    Layout();
    widget.ChildrenChanged += (_, _) => Layout();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Text or WidgetProp.Expanded)
      {
        Update();
      }
    };
    return expander;
  }

  private static SelectableTextBlock Label(UiWidget widget)
  {
    var block = new SelectableTextBlock { VerticalAlignment = VerticalAlignment.Center };
    void Update()
    {
      block.Text = widget.GetString(WidgetProp.Text);
      var wrap = widget.GetBool(WidgetProp.Wrap);
      block.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
      block.TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }

    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Text or WidgetProp.Wrap)
      {
        Update();
      }
    };
    return block;
  }

  private static Button ButtonOf(UiWidget widget)
  {
    var button = new Button();
    button.Classes.Add("compact");
    void Update() => button.Content = widget.GetString(WidgetProp.Text);
    button.Click += async (_, _) =>
    {
      var question = widget.GetString(WidgetProp.Confirm);
      if (question.Length > 0 && !await widget.Panel.Dialogs.ConfirmAsync(question))
      {
        return;
      }

      await widget.Send(WidgetEvent.Click);
    };
    Update();
    widget.Changed += (_, key) =>
    {
      if (key == WidgetProp.Text)
      {
        Update();
      }
    };
    return button;
  }

  private static ToggleButton Toggle(UiWidget widget, ToggleButton toggle)
  {
    var updating = false;
    void Update()
    {
      updating = true;
      toggle.Content = widget.GetString(WidgetProp.Text);
      toggle.IsChecked = widget.GetBool(WidgetProp.Value);
      updating = false;
    }

    toggle.IsCheckedChanged += (_, _) =>
    {
      if (updating)
      {
        return;
      }

      var value = UiValue.FromBool(toggle.IsChecked == true);
      widget.Set(WidgetProp.Value, value);
      _ = widget.Send(WidgetEvent.Change, value);
    };
    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Text or WidgetProp.Value)
      {
        Update();
      }
    };
    return toggle;
  }

  private static ComboBox Dropdown(UiWidget widget)
  {
    var box = new ComboBox { MinWidth = 120 };
    var updating = false;
    void Update()
    {
      updating = true;
      var items = widget.Get(WidgetProp.Items).Items;
      var names = new string[items.Count];
      for (var i = 0; i < names.Length; i++)
      {
        names[i] = items[i].ToDisplay();
      }

      box.ItemsSource = names;
      var selected = widget.GetInt(WidgetProp.Value, -1);
      box.SelectedIndex = selected >= 0 && selected < names.Length ? selected : -1;
      updating = false;
    }

    box.SelectionChanged += (_, _) =>
    {
      if (updating || box.SelectedIndex < 0)
      {
        return;
      }

      var value = UiValue.FromInt(box.SelectedIndex);
      widget.Set(WidgetProp.Value, value);
      _ = widget.Send(WidgetEvent.Change, value);
    };
    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Items or WidgetProp.Value)
      {
        Update();
      }
    };
    return box;
  }

  private static NumericUpDown Number(UiWidget widget)
  {
    var box = new NumericUpDown { MinWidth = 110 };
    var updating = false;
    void Update()
    {
      updating = true;
      var decimals = Math.Clamp(widget.GetInt(WidgetProp.Decimals, 2), 0, 10);
      box.FormatString = "F" + decimals.ToString(CultureInfo.InvariantCulture);
      box.Minimum = ToDecimal(widget.GetDouble(WidgetProp.Min, double.MinValue));
      box.Maximum = ToDecimal(widget.GetDouble(WidgetProp.Max, double.MaxValue));
      var step = widget.GetDouble(WidgetProp.Step, 1);
      box.Increment = ToDecimal(step > 0 ? step : 1);
      box.Value = ToDecimal(widget.GetDouble(WidgetProp.Value));
      updating = false;
    }

    box.ValueChanged += (_, e) =>
    {
      if (updating || e.NewValue is not { } number)
      {
        return;
      }

      var value = UiValue.FromDouble((double)number);
      widget.Set(WidgetProp.Value, value);
      _ = widget.Send(WidgetEvent.Change, value);
    };
    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Value or WidgetProp.Min or WidgetProp.Max or WidgetProp.Step
          or WidgetProp.Decimals)
      {
        Update();
      }
    };
    return box;
  }

  private static Slider SliderOf(UiWidget widget)
  {
    var slider = new Slider { MinWidth = 100 };
    var updating = false;
    var dragging = false;
    void Update()
    {
      updating = true;
      slider.Minimum = widget.GetDouble(WidgetProp.Min);
      slider.Maximum = widget.GetDouble(WidgetProp.Max, 1);
      var step = widget.GetDouble(WidgetProp.Step);
      slider.IsSnapToTickEnabled = step > 0;
      slider.TickFrequency = step > 0 ? step : 0;
      slider.SmallChange = step > 0 ? step : (slider.Maximum - slider.Minimum) / 100;
      if (!dragging)
      {
        slider.Value = widget.GetDouble(WidgetProp.Value);
      }

      updating = false;
    }

    void Commit()
    {
      var value = UiValue.FromDouble(slider.Value);
      widget.Set(WidgetProp.Value, value);
      _ = widget.Send(WidgetEvent.Change, value);
    }

    slider.AddHandler(Thumb.DragStartedEvent, (_, _) => dragging = true);
    slider.AddHandler(Thumb.DragCompletedEvent, (_, _) =>
    {
      dragging = false;
      Commit();
    });
    slider.ValueChanged += (_, _) =>
    {
      // While the thumb is dragged only the final value is sent, as the protocol asks.
      if (!updating && !dragging)
      {
        Commit();
      }
    };
    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Value or WidgetProp.Min or WidgetProp.Max or WidgetProp.Step)
      {
        Update();
      }
    };
    return slider;
  }

  private static Grid Progress(UiWidget widget)
  {
    var bar = new ProgressBar { Minimum = 0, Maximum = 1, MinHeight = 18 };
    var text = new TextBlock
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      FontSize = 11,
    };
    void Update()
    {
      bar.Value = Math.Clamp(widget.GetDouble(WidgetProp.Value), 0, 1);
      text.Text = widget.GetString(WidgetProp.Text);
    }

    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Value or WidgetProp.Text)
      {
        Update();
      }
    };
    var grid = new Grid();
    grid.Children.Add(bar);
    grid.Children.Add(text);
    return grid;
  }

  private static Image ImageOf(UiWidget widget)
  {
    var image = new Image { HorizontalAlignment = HorizontalAlignment.Left };
    void Update()
    {
      var bitmap = widget.Get(WidgetProp.Image).AsBytes() is { } png ? Decode(png) : null;
      image.Source = bitmap;
      var scale = widget.GetDouble(WidgetProp.Scale, 1);
      if (bitmap is null || scale <= 0)
      {
        image.Stretch = Stretch.Uniform;
        image.Width = double.NaN;
        image.Height = double.NaN;
      }
      else
      {
        image.Stretch = Stretch.Fill;
        image.Width = bitmap.PixelSize.Width * scale;
        image.Height = bitmap.PixelSize.Height * scale;
      }

      RenderOptions.SetBitmapInterpolationMode(
          image,
          widget.GetBool(WidgetProp.Smooth)
              ? BitmapInterpolationMode.HighQuality
              : BitmapInterpolationMode.None);
    }

    Update();
    widget.Changed += (_, key) =>
    {
      if (key is WidgetProp.Image or WidgetProp.Scale or WidgetProp.Smooth)
      {
        Update();
      }
    };
    return image;
  }

  private static StackPanel ColorOf(UiWidget widget)
  {
    var swatch = new Border
    {
      Width = 22,
      Height = 22,
      CornerRadius = new CornerRadius(3),
      BorderThickness = new Thickness(1),
      BorderBrush = Brushes.Gray,
    };
    var box = new TextBox { MinWidth = 100 };
    box.Classes.Add("mono");
    void Update()
    {
      var argb = (uint)widget.GetInt(WidgetProp.Value);
      box.Text = "#" + argb.ToString("X8", CultureInfo.InvariantCulture);
      box.Classes.Set("invalid", false);
      swatch.Background = new SolidColorBrush(Color.FromUInt32(argb));
    }

    void Commit()
    {
      var text = (box.Text ?? string.Empty).Trim().TrimStart('#');
      if (text.Length == 6)
      {
        text = "FF" + text;
      }

      if (text.Length != 8 ||
          !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
      {
        box.Classes.Set("invalid", true);
        return;
      }

      var value = UiValue.FromInt(unchecked((int)argb));
      widget.Set(WidgetProp.Value, value);
      _ = widget.Send(WidgetEvent.Change, value);
    }

    box.KeyDown += (_, e) =>
    {
      if (e.Key == Key.Enter)
      {
        Commit();
        e.Handled = true;
      }
    };
    box.LostFocus += (_, _) => Commit();
    Update();
    widget.Changed += (_, key) =>
    {
      if (key == WidgetProp.Value)
      {
        Update();
      }
    };
    var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
    panel.Children.Add(swatch);
    panel.Children.Add(box);
    return panel;
  }

  // Re-runs a layout when one of the listed properties of any child changes.
  private static void WatchChildren(UiWidget widget, Action layout, params WidgetProp[] keys)
  {
    var watched = new HashSet<UiWidget>();
    void Watch()
    {
      foreach (var child in widget.Children)
      {
        if (watched.Add(child))
        {
          child.Changed += (_, key) =>
          {
            if (Array.IndexOf(keys, key) >= 0 && widget.Children.Contains(child))
            {
              layout();
            }
          };
        }
      }
    }

    Watch();
    widget.ChildrenChanged += (_, _) => Watch();
  }

  private static void Place(Control control, bool horizontal, int index)
  {
    Grid.SetRow(control, horizontal ? 0 : index);
    Grid.SetColumn(control, horizontal ? index : 0);
  }

  private static GridLength ParseLength(string text)
  {
    text = text.Trim();
    if (text.Equals("auto", StringComparison.OrdinalIgnoreCase))
    {
      return GridLength.Auto;
    }

    if (text.EndsWith('*'))
    {
      var share = text.Length == 1
          ? 1
          : double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
              ? s
              : 1;
      return new GridLength(share, GridUnitType.Star);
    }

    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels)
        ? new GridLength(pixels)
        : GridLength.Star;
  }

  private static decimal ToDecimal(double value)
  {
    if (double.IsNaN(value))
    {
      return 0;
    }

    return value >= (double)decimal.MaxValue ? decimal.MaxValue
        : value <= (double)decimal.MinValue ? decimal.MinValue
        : (decimal)value;
  }

  internal static Bitmap? Decode(byte[] png) =>
      PngImageConverter.Instance.Convert(png, typeof(Bitmap), null, CultureInfo.InvariantCulture)
          as Bitmap;
}
