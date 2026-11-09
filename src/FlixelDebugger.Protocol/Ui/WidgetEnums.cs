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

namespace FlixelDebugger.Protocol.Ui;

/// <summary>
/// The kind of a widget in a panel's tree. See "Widget types" in <c>DEBUG_PROTOCOL.md</c>.
/// </summary>
public enum WidgetType : byte
{
  /// <summary>Children in a line, top to bottom or left to right.</summary>
  Stack = 1,

  /// <summary>Children left to right, flowing onto new lines.</summary>
  Wrap = 2,

  /// <summary>Children in cells, row by row.</summary>
  Grid = 3,

  /// <summary>Two children with a draggable bar between them.</summary>
  Split = 4,

  /// <summary>One page per child.</summary>
  Tabs = 5,

  /// <summary>Scrolls its one child.</summary>
  Scroll = 6,

  /// <summary>A header that shows or hides its one child.</summary>
  Expander = 7,

  /// <summary>Text.</summary>
  Label = 8,

  /// <summary>A button.</summary>
  Button = 9,

  /// <summary>A checkbox.</summary>
  Checkbox = 10,

  /// <summary>A button that stays pressed.</summary>
  Toggle = 11,

  /// <summary>A closed list to pick one item from.</summary>
  Dropdown = 12,

  /// <summary>A text field.</summary>
  TextBox = 13,

  /// <summary>A number field.</summary>
  Number = 14,

  /// <summary>A slider.</summary>
  Slider = 15,

  /// <summary>A bar that fills up.</summary>
  Progress = 16,

  /// <summary>A PNG picture.</summary>
  Image = 17,

  /// <summary>A thin line.</summary>
  Separator = 18,

  /// <summary>Rows of cells under column headers.</summary>
  Table = 19,

  /// <summary>Nodes nested under other nodes.</summary>
  Tree = 20,

  /// <summary>Named values in sections with editors.</summary>
  Properties = 21,

  /// <summary>Lines over time.</summary>
  Graph = 22,

  /// <summary>Boxes in a 2D view.</summary>
  Canvas = 23,

  /// <summary>A color picker.</summary>
  Color = 24,
}

/// <summary>
/// The key of a widget property. See "Common properties" and "Widget-specific properties" in
/// <c>DEBUG_PROTOCOL.md</c>.
/// </summary>
public enum WidgetProp : byte
{
  /// <summary><c>BOOL</c>, default true.</summary>
  Visible = 1,

  /// <summary><c>BOOL</c>, default true.</summary>
  Enabled = 2,

  /// <summary><c>STRING</c>.</summary>
  Tooltip = 3,

  /// <summary><c>FLOAT</c>, 0 sizes to the content.</summary>
  Width = 4,

  /// <summary><c>FLOAT</c>, 0 sizes to the content.</summary>
  Height = 5,

  /// <summary><c>FLOAT</c>, the share of leftover space in a stack.</summary>
  Grow = 6,

  /// <summary><c>INT</c>, see <see cref="UiStyle"/>.</summary>
  Style = 7,

  /// <summary><c>STRING</c>.</summary>
  Text = 16,

  /// <summary>Depends on the widget.</summary>
  Value = 17,

  /// <summary><c>LIST</c>, depends on the widget.</summary>
  Items = 18,

  /// <summary><c>DOUBLE</c>.</summary>
  Min = 19,

  /// <summary><c>DOUBLE</c>.</summary>
  Max = 20,

  /// <summary><c>DOUBLE</c>.</summary>
  Step = 21,

  /// <summary><c>STRING</c>.</summary>
  Placeholder = 22,

  /// <summary><c>BOOL</c>.</summary>
  Horizontal = 23,

  /// <summary><c>FLOAT</c>.</summary>
  Spacing = 24,

  /// <summary><c>LIST</c>, depends on the widget.</summary>
  Columns = 25,

  /// <summary><c>LONG</c>, -1 for none.</summary>
  Selected = 26,

  /// <summary><c>LIST</c> of strings.</summary>
  Menu = 27,

  /// <summary><c>INT</c>.</summary>
  Limit = 28,

  /// <summary><c>BOOL</c>.</summary>
  Follow = 29,

  /// <summary><c>INT</c>, the id of a text box.</summary>
  Filter = 30,

  /// <summary><c>INT</c>.</summary>
  Sort = 31,

  /// <summary><c>BYTES</c> (a PNG) or <c>NULL</c>.</summary>
  Image = 32,

  /// <summary><c>FLOAT</c>.</summary>
  Scale = 33,

  /// <summary><c>BOOL</c>.</summary>
  Smooth = 34,

  /// <summary><c>BOOL</c>.</summary>
  Multiline = 35,

  /// <summary><c>BOOL</c>.</summary>
  ReadOnly = 36,

  /// <summary><c>BOOL</c>.</summary>
  ClearOnSubmit = 37,

  /// <summary><c>INT</c>.</summary>
  History = 38,

  /// <summary><c>LIST</c> of <c>[name, usage, description]</c>.</summary>
  Completions = 39,

  /// <summary><c>STRING</c>.</summary>
  Confirm = 40,

  /// <summary><c>BOOL</c>.</summary>
  Expanded = 41,

  /// <summary><c>BOOL</c>.</summary>
  Live = 42,

  /// <summary><c>INT</c>.</summary>
  Decimals = 43,

  /// <summary><c>LIST</c> of <c>[name, color, unit]</c>.</summary>
  Series = 44,

  /// <summary><c>LIST</c> of samples.</summary>
  Samples = 45,

  /// <summary><c>LIST [width, height]</c>.</summary>
  View = 46,

  /// <summary><c>LIST</c> of shapes.</summary>
  Shapes = 47,

  /// <summary><c>INT</c>, see <see cref="UiStyle"/>.</summary>
  Interact = 48,

  /// <summary><c>LIST</c> of sections.</summary>
  Sections = 49,

  /// <summary><c>BOOL</c>.</summary>
  Wrap = 50,
}

/// <summary>
/// What the person did with a widget. See <c>PANEL_EVENT</c> in <c>DEBUG_PROTOCOL.md</c>.
/// </summary>
public enum WidgetEvent : byte
{
  /// <summary>A button was clicked. No arguments.</summary>
  Click = 1,

  /// <summary>A value changed. One argument: the new value.</summary>
  Change = 2,

  /// <summary>Enter was pressed in a text box. One argument: the text.</summary>
  Submit = 3,

  /// <summary>A row or node was selected. One argument: its id, or null.</summary>
  Select = 4,

  /// <summary>A row or node was double clicked. One argument: its id.</summary>
  Activate = 5,

  /// <summary>A menu item was picked. Arguments: the row or node id, the item position.</summary>
  Menu = 6,

  /// <summary>A property was edited. Arguments: section, row, value.</summary>
  Edit = 7,

  /// <summary>A canvas point was clicked. Arguments: x, y.</summary>
  Pick = 8,

  /// <summary>A canvas shape was dragged. Arguments: id, dx, dy.</summary>
  Drag = 9,

  /// <summary>A canvas was panned. Arguments: dx, dy.</summary>
  Pan = 10,

  /// <summary>A canvas was zoomed. Arguments: factor, x, y.</summary>
  Zoom = 11,
}

/// <summary>The operation of one entry of a <c>PANEL_PATCH</c>.</summary>
public enum PatchCode : byte
{
  /// <summary>The value replaces the property.</summary>
  Set = 1,

  /// <summary>The value's items are added to the end of the list property.</summary>
  Append = 2,
}

/// <summary>Where a panel goes the first time the app sees its id.</summary>
public enum PanelDock : byte
{
  /// <summary>The middle tab group.</summary>
  Center = 0,

  /// <summary>The left column.</summary>
  Left = 1,

  /// <summary>The right column.</summary>
  Right = 2,

  /// <summary>The bottom tab group.</summary>
  Bottom = 3,
}

/// <summary>What an <c>APP_REQUEST</c> asks the app to do.</summary>
public enum AppRequestKind : byte
{
  /// <summary>Show a panel and bring it to the front.</summary>
  ShowPanel = 1,

  /// <summary>Close a panel's tab.</summary>
  HidePanel = 2,

  /// <summary>Put every panel back where its dock says.</summary>
  ResetLayout = 3,

  /// <summary>Show a short message.</summary>
  Notify = 4,
}

/// <summary>The color family of a widget, row, or shape. The app picks the real colors.</summary>
public enum Tone : byte
{
  /// <summary>The normal text color.</summary>
  Normal = 0,

  /// <summary>A quieter color for hints.</summary>
  Muted = 1,

  /// <summary>The theme's accent color.</summary>
  Accent = 2,

  /// <summary>A color for good news.</summary>
  Success = 3,

  /// <summary>A color for warnings.</summary>
  Warning = 4,

  /// <summary>A color for errors.</summary>
  Error = 5,
}
