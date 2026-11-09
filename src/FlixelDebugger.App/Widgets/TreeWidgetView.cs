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
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// Draws a <see cref="WidgetType.Tree"/> widget: nodes the person can expand, select, and filter.
/// </summary>
/// <remarks>
/// <para>
/// Node objects are kept by id between updates, and only the parts that changed are touched, so
/// the tree does not jump or fold up when the game sends a new list. The app remembers which nodes
/// the person expanded; the game's "expanded" style bit only matters the first time a node shows
/// up.
/// </para>
/// <para>
/// While a filter box has text, only matching nodes and their parents are shown, and those parents
/// are opened so the matches can be seen.
/// </para>
/// </remarks>
public sealed partial class TreeWidgetView
{
  private readonly UiWidget _widget;
  private readonly TreeView _tree = new();
  private readonly RangeObservableCollection<TreeNodeItem> _roots = [];
  private readonly Dictionary<long, TreeNodeItem> _items = [];
  private readonly HashSet<long> _expanded = [];
  private readonly HashSet<long> _seen = [];
  private readonly ContextMenu _menu = new();
  private UiWidget? _filterBox;
  private bool _updating;

  /// <summary>
  /// Builds the control and binds it to the widget.
  /// </summary>
  /// <param name="widget">The tree widget.</param>
  public TreeWidgetView(UiWidget widget)
  {
    ArgumentNullException.ThrowIfNull(widget);
    _widget = widget;
    _tree.ItemsSource = _roots;
    _tree.ContextMenu = _menu;
    _tree.ItemTemplate = new FuncTreeDataTemplate<TreeNodeItem>(
        (node, _) => BuildNode(node), node => node.Children);
    _tree.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
    {
      Setters =
      {
        new Setter(
            TreeViewItem.IsExpandedProperty,
            new Binding(nameof(TreeNodeItem.IsExpanded)) { Mode = BindingMode.TwoWay }),
      },
    });
    _tree.SelectionChanged += (_, _) => OnSelected();
    _tree.DoubleTapped += (_, _) => Activate();
    _tree.KeyDown += (_, e) =>
    {
      if (e.Key == Key.Enter)
      {
        Activate();
        e.Handled = true;
      }
    };
    _menu.Opening += (_, _) => BuildMenu();
    Root = _tree;

    ApplyFilterSource();
    ApplyItems();
    ApplySelected();
    widget.Changed += (_, key) =>
    {
      switch (key)
      {
        case WidgetProp.Items:
          ApplyItems();
          break;
        case WidgetProp.Filter:
          ApplyFilterSource();
          ApplyItems();
          break;
        case WidgetProp.Selected:
          ApplySelected();
          break;
      }
    };
  }

  /// <summary>Gets the control to put in the panel.</summary>
  public Control Root { get; }

  private static StackPanel BuildNode(TreeNodeItem? node)
  {
    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
    if (node is null)
    {
      return row;
    }

    var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
    text.Bind(TextBlock.TextProperty, new Binding(nameof(TreeNodeItem.Text)));
    var detail = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
    detail.Classes.Add("tone-muted");
    detail.Bind(TextBlock.TextProperty, new Binding(nameof(TreeNodeItem.Detail)));
    var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
    row.Children.Add(text);
    row.Children.Add(detail);
    row.Children.Add(badges);

    void Update()
    {
      WidgetStyle.SetClasses(text, node.Style);
      badges.Children.Clear();
      foreach (var (label, tone) in node.Badges)
      {
        var badge = new Border
        {
          CornerRadius = new CornerRadius(3),
          Padding = new Thickness(4, 0),
          BorderThickness = new Thickness(1),
          VerticalAlignment = VerticalAlignment.Center,
          Child = new TextBlock { Text = label, FontSize = 11 },
        };
        badge.BorderBrush = WidgetStyle.BrushOf(row, (Tone)tone, Brushes.Gray);
        WidgetStyle.SetClasses(badge, tone);
        badges.Children.Add(badge);
      }
    }

    Update();
    node.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName is nameof(TreeNodeItem.Style) or nameof(TreeNodeItem.Badges))
      {
        Update();
      }
    };
    return row;
  }

  private void ApplyItems()
  {
    _updating = true;
    var list = _widget.Get(WidgetProp.Items).Items;
    var present = new HashSet<long>();
    var children = new Dictionary<long, List<TreeNodeItem>>();
    var roots = new List<TreeNodeItem>();
    var parents = new Dictionary<long, long>();
    foreach (var value in list)
    {
      var id = value[0].AsLong(-1);
      if (!present.Add(id))
      {
        continue;
      }

      if (!_items.TryGetValue(id, out var item))
      {
        item = new TreeNodeItem(id);
        _items[id] = item;
        var node = item;
        node.PropertyChanged += (_, e) =>
        {
          // Remember what the person opened, but not what a filter opened for them.
          if (!_updating && e.PropertyName == nameof(TreeNodeItem.IsExpanded) &&
              FilterText().Length == 0)
          {
            if (node.IsExpanded)
            {
              _expanded.Add(node.Id);
            }
            else
            {
              _expanded.Remove(node.Id);
            }
          }
        };
      }

      var style = value[4].AsInt();
      if (_seen.Add(id) && UiStyle.Has(style, UiStyle.Expanded))
      {
        _expanded.Add(id);
      }

      item.Text = value[2].AsString();
      item.Detail = value[3].AsString();
      item.Style = style;
      item.SetBadges(value[5].Items);
      var parent = value[1].AsLong(-1);
      parents[id] = parent;
      if (parent >= 0 && present.Contains(parent))
      {
        if (!children.TryGetValue(parent, out var siblings))
        {
          siblings = [];
          children[parent] = siblings;
        }

        siblings.Add(item);
      }
      else
      {
        roots.Add(item);
      }
    }

    // A filter keeps the matching nodes and every parent on their way up.
    var filter = FilterText();
    HashSet<long>? keep = null;
    if (filter.Length > 0)
    {
      keep = [];
      foreach (var (id, item) in _items)
      {
        if (!present.Contains(id) ||
            !item.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        for (var at = id; at >= 0 && keep.Add(at); at = parents.GetValueOrDefault(at, -1))
        {
        }
      }
    }

    foreach (var id in _items.Keys.ToArray())
    {
      if (!present.Contains(id))
      {
        _items.Remove(id);
      }
    }

    foreach (var (id, item) in _items)
    {
      var kids = children.TryGetValue(id, out var found) ? found : [];
      if (keep is not null)
      {
        kids = kids.FindAll(k => keep.Contains(k.Id));
      }

      item.SetChildren(kids);
      item.IsExpanded = keep is not null ? kids.Count > 0 : _expanded.Contains(id);
    }

    if (keep is not null)
    {
      roots = roots.FindAll(r => keep.Contains(r.Id));
    }

    if (!SameItems(_roots, roots))
    {
      _roots.ReplaceAll(roots);
    }

    _updating = false;
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
          ApplyItems();
        }
      };
    }
  }

  private string FilterText() => _filterBox?.GetString(WidgetProp.Text).Trim() ?? string.Empty;

  private void ApplySelected()
  {
    _updating = true;
    var id = _widget.GetLong(WidgetProp.Selected, -1);
    _tree.SelectedItem = id >= 0 && _items.TryGetValue(id, out var item) ? item : null;
    _updating = false;
  }

  private void OnSelected()
  {
    if (_updating)
    {
      return;
    }

    var value = _tree.SelectedItem is TreeNodeItem node ? UiValue.FromLong(node.Id) : UiValue.Null;
    _widget.Set(WidgetProp.Selected, value.IsNull ? UiValue.FromLong(-1) : value);
    _ = _widget.Send(WidgetEvent.Select, value);
  }

  private void Activate()
  {
    if (_tree.SelectedItem is TreeNodeItem node)
    {
      _ = _widget.Send(WidgetEvent.Activate, UiValue.FromLong(node.Id));
    }
  }

  private void BuildMenu()
  {
    _menu.Items.Clear();
    if (_tree.SelectedItem is not TreeNodeItem node)
    {
      return;
    }

    var items = _widget.Get(WidgetProp.Menu).Items;
    for (var i = 0; i < items.Count; i++)
    {
      var index = i;
      var item = new MenuItem { Header = items[i].AsString() };
      item.Click += (_, _) =>
          _ = _widget.Send(WidgetEvent.Menu, UiValue.FromLong(node.Id), UiValue.FromInt(index));
      _menu.Items.Add(item);
    }

    if (_menu.Items.Count > 0)
    {
      _menu.Items.Add(new Separator());
    }

    var copy = new MenuItem { Header = "Copy" };
    copy.Click += async (_, _) =>
    {
      if (TopLevel.GetTopLevel(_tree)?.Clipboard is { } clipboard)
      {
        await clipboard.SetTextAsync(
            node.Detail.Length > 0 ? $"{node.Text} {node.Detail}" : node.Text);
      }
    };
    _menu.Items.Add(copy);
  }

  private static bool SameItems(
      RangeObservableCollection<TreeNodeItem> current, List<TreeNodeItem> wanted)
  {
    if (current.Count != wanted.Count)
    {
      return false;
    }

    for (var i = 0; i < wanted.Count; i++)
    {
      if (!ReferenceEquals(current[i], wanted[i]))
      {
        return false;
      }
    }

    return true;
  }

  /// <summary>One node of a tree widget, kept by id between updates.</summary>
  /// <param name="id">The node id the game uses in events.</param>
  public sealed partial class TreeNodeItem(long id) : ObservableObject
  {
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private int _style;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private IReadOnlyList<(string Text, int Tone)> _badges = [];

    /// <summary>Gets the node id.</summary>
    public long Id { get; } = id;

    /// <summary>Gets the child nodes that are shown.</summary>
    public RangeObservableCollection<TreeNodeItem> Children { get; } = [];

    internal void SetBadges(IReadOnlyList<UiValue> values)
    {
      var badges = new (string, int)[values.Count];
      for (var i = 0; i < badges.Length; i++)
      {
        badges[i] = (values[i][0].AsString(), values[i][1].AsInt());
      }

      if (!badges.SequenceEqual(Badges))
      {
        Badges = badges;
      }
    }

    internal void SetChildren(List<TreeNodeItem> children)
    {
      if (!SameItems(Children, children))
      {
        Children.ReplaceAll(children);
      }
    }
  }
}
