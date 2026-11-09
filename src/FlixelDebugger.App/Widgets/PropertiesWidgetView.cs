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
using Avalonia.Layout;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Draws a <see cref="WidgetType.Properties"/> widget: named values in folding sections, like an
/// object inspector.
/// </summary>
/// <remarks>
/// Each section reuses the app's property rows (<see cref="PropertyRowViewModel"/>), so an editable
/// value gets the editor that fits its kind. Committing an edit sends
/// <see cref="WidgetEvent.Edit"/> with the section index, the row index, and the new value, and the
/// game decides whether to take it.
/// </remarks>
public sealed class PropertiesWidgetView
{
  private readonly UiWidget _widget;
  private readonly StackPanel _panel = new() { Spacing = 4 };
  private readonly List<Section> _sections = [];

  /// <summary>
  /// Builds the control and binds it to the widget.
  /// </summary>
  /// <param name="widget">The property grid widget.</param>
  public PropertiesWidgetView(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    _widget = widget;
    Root = _panel;
    Apply();
    widget.Changed += (_, key) =>
    {
      if (key == WidgetProp.Sections)
      {
        Apply();
      }
    };
  }

  /// <summary>Gets the control to put in the panel.</summary>
  public Control Root { get; }

  private void Apply()
  {
    var sections = _widget.Get(WidgetProp.Sections).Items;
    for (var i = 0; i < sections.Count; i++)
    {
      var value = sections[i];
      if (i >= _sections.Count)
      {
        var index = i;
        var section = new Section(new PropertyGridViewModel(
            (row, edited) => _widget.Send(
                WidgetEvent.Edit,
                UiValue.FromInt(index),
                UiValue.FromInt(row),
                UiValue.FromProperty(edited))));
        section.Expander.IsExpanded = value[1].AsBool(true);
        _sections.Add(section);
        _panel.Children.Add(section.Expander);
      }

      var current = _sections[i];
      current.Expander.Header = value[0].AsString();
      var rows = value[2].Items;
      var list = new PropertyRow[rows.Count];
      for (var r = 0; r < list.Length; r++)
      {
        var row = rows[r];
        var cell = row[1];
        list[r] = new PropertyRow(
            row[0].AsString(), cell.ToProperty(), row[2].AsBool() && cell.Kind != ValueKind.Error);
      }

      current.Grid.Update(list);
    }

    while (_sections.Count > sections.Count)
    {
      _panel.Children.Remove(_sections[^1].Expander);
      _sections.RemoveAt(_sections.Count - 1);
    }
  }

  private sealed class Section
  {
    public Section(PropertyGridViewModel grid)
    {
      Grid = grid;
      Expander = new Expander
      {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = new ItemsControl { ItemsSource = grid.Rows },
      };
    }

    public PropertyGridViewModel Grid { get; }

    public Expander Expander { get; }
  }
}
