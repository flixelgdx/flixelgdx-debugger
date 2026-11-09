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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Draws a <see cref="WidgetType.Table"/> widget: rows of cells under a header.
/// </summary>
/// <remarks>
/// <para>
/// The game sends the rows; the app only decides how to show them. Sorting by a header, hiding
/// rows that do not match a filter box, keeping the newest row in view, and copying a row are all
/// done here without asking the game, so they feel instant even over a slow connection. Every
/// event the table sends carries a row id, never a position, so sorting and filtering cannot
/// confuse the game.
/// </para>
/// <para>
/// When the game only adds rows to the end (a log, for example), the table adds just those rows
/// instead of rebuilding the whole list, which keeps long tables cheap.
/// </para>
/// </remarks>
public sealed class TableWidgetView
{
  private readonly UiWidget _widget;
  private readonly Grid _header = new() { Margin = new Thickness(10, 0, 22, 2) };
  private readonly ListBox _list = new();
  private readonly RangeObservableCollection<TableRow> _shown = [];
  private readonly ContextMenu _menu = new();
  private TableColumn[] _columns = [];
  private TableRow[] _rows = [];
  private UiWidget? _filterBox;
  private int _sort;
  private bool _updating;

  /// <summary>
  /// Builds the control and binds it to the widget.
  /// </summary>
  /// <param name="widget">The table widget.</param>
  public TableWidgetView(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    _widget = widget;
    _list.ItemsSource = _shown;
    _list.SelectionMode = SelectionMode.Single;
    _list.ContextMenu = _menu;
    _list.SelectionChanged += (_, _) => OnSelected();
    _list.DoubleTapped += (_, _) => Activate();
    _list.KeyDown += (_, e) =>
    {
      if (e.Key == Key.Enter)
      {
        Activate();
        e.Handled = true;
      }
    };
    _menu.Opening += (_, _) => BuildMenu();
    var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
    Grid.SetRow(_list, 1);
    grid.Children.Add(_header);
    grid.Children.Add(_list);
    Root = grid;

    _sort = widget.GetInt(WidgetProp.Sort);
    ApplyColumns();
    ApplyRows();
    ApplyFilterSource();
    ApplySelected();
    widget.Changed += (_, key) => Apply(key);
  }

  /// <summary>Gets the control to put in the panel.</summary>
  public Control Root { get; }

  private void Apply(WidgetProp key)
  {
    switch (key)
    {
      case WidgetProp.Columns:
        ApplyColumns();
        Show(false);
        break;
      case WidgetProp.Items:
        ApplyRows();
        break;
      case WidgetProp.Sort:
        _sort = _widget.GetInt(WidgetProp.Sort);
        ApplyColumns();
        Show(false);
        break;
      case WidgetProp.Filter:
        ApplyFilterSource();
        Show(false);
        break;
      case WidgetProp.Selected:
        ApplySelected();
        break;
    }
  }

  private void ApplyColumns()
  {
    var items = _widget.Get(WidgetProp.Columns).Items;
    _columns = new TableColumn[items.Count];
    for (var i = 0; i < _columns.Length; i++)
    {
      var item = items[i];
      _columns[i] = new TableColumn(item[0].AsString(), item[1].AsDouble(), item[2].AsInt());
    }

    _header.ColumnDefinitions = Definitions();
    _header.Children.Clear();
    for (var i = 0; i < _columns.Length; i++)
    {
      var column = _columns[i];
      var title = column.Title;
      if (Math.Abs(_sort) == i + 1)
      {
        title += _sort > 0 ? " ▲" : " ▼";
      }

      Control cell;
      if ((column.Flags & UiStyle.ColumnSortable) != 0)
      {
        var index = i + 1;
        var button = new Button
        {
          Content = title,
          Padding = new Thickness(0),
          Background = Brushes.Transparent,
          BorderThickness = new Thickness(0),
          FontWeight = FontWeight.SemiBold,
          HorizontalAlignment =
              Right(column) ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
        button.Click += (_, _) =>
        {
          _sort = _sort == index ? -index : index;
          ApplyColumns();
          Show(false);
        };
        cell = button;
      }
      else
      {
        cell = new TextBlock
        {
          Text = title,
          FontWeight = FontWeight.SemiBold,
          TextAlignment = Right(column) ? TextAlignment.Right : TextAlignment.Left,
        };
      }

      Grid.SetColumn(cell, i);
      _header.Children.Add(cell);
    }

    _list.ItemTemplate = new FuncDataTemplate<TableRow>((row, _) => BuildRow(row), true);
  }

  private Grid BuildRow(TableRow? row)
  {
    var grid = new Grid { ColumnDefinitions = Definitions(), ColumnSpacing = 8 };
    if (row is null)
    {
      return grid;
    }

    WidgetStyle.SetClasses(grid, row.Style);
    for (var i = 0; i < _columns.Length && i < row.Cells.Length; i++)
    {
      var column = _columns[i];
      var text = new TextBlock
      {
        Text = row.Cells[i],
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = Right(column) ? TextAlignment.Right : TextAlignment.Left,
      };
      if ((column.Flags & UiStyle.ColumnMono) != 0)
      {
        text.Classes.Add("mono");
      }

      Grid.SetColumn(text, i);
      grid.Children.Add(text);
    }

    if (row.Detail.Length > 0)
    {
      ToolTip.SetTip(grid, row.Detail);
    }

    return grid;
  }

  private ColumnDefinitions Definitions()
  {
    var definitions = new ColumnDefinitions();
    foreach (var column in _columns)
    {
      definitions.Add(new ColumnDefinition(
          column.Width > 0 ? new GridLength(column.Width) : GridLength.Star));
    }

    if (definitions.Count == 0)
    {
      definitions.Add(new ColumnDefinition(GridLength.Star));
    }

    return definitions;
  }

  private void ApplyRows()
  {
    var items = _widget.Get(WidgetProp.Items).Items;
    var rows = new TableRow[items.Count];
    for (var i = 0; i < rows.Length; i++)
    {
      rows[i] = TableRow.Read(items[i]);
    }

    var old = _rows;
    _rows = rows;

    // The cheap path: no sorting or filtering, and the game only dropped old rows from the front
    // and added new ones at the end.
    if (_sort == 0 && FilterText().Length == 0 && TryAppend(old, rows))
    {
      return;
    }

    Show(true);
  }

  private bool TryAppend(TableRow[] old, TableRow[] rows)
  {
    if (old.Length == 0 || rows.Length == 0 || _shown.Count != old.Length)
    {
      return false;
    }

    var dropped = Array.FindIndex(old, r => r.Id == rows[0].Id);
    if (dropped < 0)
    {
      return false;
    }

    var kept = old.Length - dropped;
    if (kept > rows.Length)
    {
      return false;
    }

    for (var i = 0; i < kept; i++)
    {
      if (old[dropped + i].Id != rows[i].Id)
      {
        return false;
      }
    }

    var follow = ShouldFollow();
    _updating = true;
    for (var i = 0; i < dropped; i++)
    {
      _shown.RemoveAt(0);
    }

    for (var i = 0; i < kept; i++)
    {
      // A kept row can still have new text, for example a counter of repeated log lines.
      if (!rows[i].Equals(_shown[i]))
      {
        _shown[i] = rows[i];
      }
    }

    for (var i = kept; i < rows.Length; i++)
    {
      _shown.Add(rows[i]);
    }

    _updating = false;
    if (follow && _shown.Count > 0)
    {
      _list.ScrollIntoView(_shown[^1]);
    }

    return true;
  }

  private void Show(bool followable)
  {
    var follow = followable && ShouldFollow();
    var filter = FilterText();
    IEnumerable<TableRow> rows = _rows;
    if (filter.Length > 0)
    {
      rows = rows.Where(r => r.Matches(filter));
    }

    if (_sort != 0)
    {
      var column = Math.Abs(_sort) - 1;
      var ordered = rows.Order(new TableRow.Comparer(column));
      rows = _sort > 0 ? ordered : ordered.Reverse();
    }

    _updating = true;
    var selected = _widget.GetLong(WidgetProp.Selected, -1);
    _shown.ReplaceAll(rows);
    _list.SelectedItem = FindShown(selected);
    _updating = false;
    if (follow && _shown.Count > 0)
    {
      _list.ScrollIntoView(_shown[^1]);
    }
  }

  // Follows the newest row only while the person has not scrolled away from the bottom.
  private bool ShouldFollow()
  {
    if (!_widget.GetBool(WidgetProp.Follow))
    {
      return false;
    }

    if (_list.Scroll is not { } scroll)
    {
      return true;
    }

    return scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 4;
  }

  private void ApplyFilterSource()
  {
    _filterBox = null;
    var id = _widget.GetInt(WidgetProp.Filter, -1);
    if (id >= 0 && _widget.Panel.TryGetWidget(id, out var box))
    {
      _filterBox = box;
      box.Changed += (_, key) =>
      {
        if (key == WidgetProp.Text && ReferenceEquals(box, _filterBox))
        {
          Show(false);
        }
      };
    }
  }

  private string FilterText() => _filterBox?.GetString(WidgetProp.Text).Trim() ?? string.Empty;

  private void ApplySelected()
  {
    _updating = true;
    _list.SelectedItem = FindShown(_widget.GetLong(WidgetProp.Selected, -1));
    if (_list.SelectedItem is { } item)
    {
      _list.ScrollIntoView(item);
    }

    _updating = false;
  }

  private TableRow? FindShown(long id)
  {
    if (id < 0)
    {
      return null;
    }

    foreach (var row in _shown)
    {
      if (row.Id == id)
      {
        return row;
      }
    }

    return null;
  }

  private void OnSelected()
  {
    if (_updating)
    {
      return;
    }

    var value = _list.SelectedItem is TableRow row ? UiValue.FromLong(row.Id) : UiValue.Null;
    _widget.Set(WidgetProp.Selected, value.IsNull ? UiValue.FromLong(-1) : value);
    _ = _widget.Send(WidgetEvent.Select, value);
  }

  private void Activate()
  {
    if (_list.SelectedItem is TableRow row)
    {
      _ = _widget.Send(WidgetEvent.Activate, UiValue.FromLong(row.Id));
    }
  }

  private void BuildMenu()
  {
    _menu.Items.Clear();
    if (_list.SelectedItem is not TableRow row)
    {
      return;
    }

    var items = _widget.Get(WidgetProp.Menu).Items;
    for (var i = 0; i < items.Count; i++)
    {
      var index = i;
      var item = new MenuItem { Header = items[i].AsString() };
      item.Click += (_, _) =>
          _ = _widget.Send(WidgetEvent.Menu, UiValue.FromLong(row.Id), UiValue.FromInt(index));
      _menu.Items.Add(item);
    }

    if (_menu.Items.Count > 0)
    {
      _menu.Items.Add(new Separator());
    }

    var copyRow = new MenuItem { Header = "Copy row" };
    copyRow.Click += (_, _) => _ = CopyAsync(string.Join('\t', row.Cells));
    _menu.Items.Add(copyRow);
    if (row.Detail.Length > 0)
    {
      var copyDetail = new MenuItem { Header = "Copy detail" };
      copyDetail.Click += (_, _) => _ = CopyAsync(row.Detail);
      _menu.Items.Add(copyDetail);
    }
  }

  private async Task CopyAsync(string text)
  {
    if (TopLevel.GetTopLevel(_list)?.Clipboard is { } clipboard)
    {
      await clipboard.SetTextAsync(text);
    }
  }

  private static bool Right(TableColumn column) => (column.Flags & UiStyle.ColumnRight) != 0;

  private readonly record struct TableColumn(string Title, double Width, int Flags);
}
