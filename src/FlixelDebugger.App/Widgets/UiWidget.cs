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

using FlixelDebugger.App.Panels;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Widgets;

/// <summary>
/// The app's copy of one widget the game built: its type, its current properties, and its
/// children.
/// </summary>
/// <remarks>
/// <para>
/// A widget is a mirror, not a source of truth. The game owns every value; this class only holds
/// what the game sent last and tells the control that draws it when a property changed. When the
/// person does something, the control calls <see cref="Send"/> and the game decides what happens,
/// usually by sending a new value back. Think of a remote control's screen: it shows what the TV
/// reports, and pressing a button asks the TV to change.
/// </para>
/// <para>Use this class from the UI thread only.</para>
/// </remarks>
public sealed class UiWidget
{
  private readonly Dictionary<WidgetProp, UiValue> _props = [];

  /// <summary>
  /// Creates a widget from a node of a panel tree.
  /// </summary>
  /// <param name="panel">The panel the widget belongs to.</param>
  /// <param name="node">The node. Its children are not copied; the panel adds them.</param>
  public UiWidget(GamePanelViewModel panel, WidgetNode node)
  {
    ArgumentNullException.ThrowIfNull(panel);
    ArgumentNullException.ThrowIfNull(node);
    Panel = panel;
    Id = node.Id;
    Type = node.Type;
    Name = node.Name;
    foreach (var (key, value) in node.Props)
    {
      _props[key] = value;
    }
  }

  /// <summary>
  /// Raised on the UI thread after a property changed. The argument is the property key.
  /// </summary>
  public event EventHandler<WidgetProp>? Changed;

  /// <summary>Raised after the list of children was replaced.</summary>
  public event EventHandler? ChildrenChanged;

  /// <summary>Gets the panel the widget belongs to.</summary>
  public GamePanelViewModel Panel { get; }

  /// <summary>Gets the widget id, unique inside its panel.</summary>
  public int Id { get; }

  /// <summary>Gets the widget type.</summary>
  public WidgetType Type { get; }

  /// <summary>Gets the optional stable name the game gave the widget, or an empty string.</summary>
  public string Name { get; }

  /// <summary>Gets the child widgets, in order.</summary>
  public IReadOnlyList<UiWidget> Children { get; private set; } = [];

  /// <summary>Gets whether the widget is shown. A hidden widget takes no space.</summary>
  public bool IsVisible => GetBool(WidgetProp.Visible, true);

  /// <summary>Gets whether the person may use the widget.</summary>
  public bool IsEnabled => GetBool(WidgetProp.Enabled, true);

  /// <summary>Gets the tooltip, or an empty string for none.</summary>
  public string Tooltip => GetString(WidgetProp.Tooltip);

  /// <summary>Gets the preferred width, or 0 to size to the content.</summary>
  public double Width => GetDouble(WidgetProp.Width);

  /// <summary>Gets the preferred height, or 0 to size to the content.</summary>
  public double Height => GetDouble(WidgetProp.Height);

  /// <summary>Gets the share of leftover space the widget takes inside a stack.</summary>
  public double Grow => GetDouble(WidgetProp.Grow);

  /// <summary>Gets the style bits (tone, bold, mono, heading, italic).</summary>
  public int Style => GetInt(WidgetProp.Style);

  /// <summary>
  /// Returns a property as the game sent it.
  /// </summary>
  /// <param name="key">The property.</param>
  /// <returns>The value, or <see cref="UiValue.Null"/> when the game did not send it.</returns>
  public UiValue Get(WidgetProp key) =>
      _props.TryGetValue(key, out var value) ? value : UiValue.Null;

  /// <summary>
  /// Tells whether the game sent a property.
  /// </summary>
  /// <param name="key">The property.</param>
  /// <returns><see langword="true"/> if the property has a value.</returns>
  public bool Has(WidgetProp key) => _props.ContainsKey(key);

  /// <summary>Returns a property as a boolean.</summary>
  /// <param name="key">The property.</param>
  /// <param name="fallback">The value when the property is missing or not a boolean.</param>
  /// <returns>The value.</returns>
  public bool GetBool(WidgetProp key, bool fallback = false) =>
      _props.TryGetValue(key, out var value) ? value.AsBool(fallback) : fallback;

  /// <summary>Returns a property as a whole number.</summary>
  /// <param name="key">The property.</param>
  /// <param name="fallback">The value when the property is missing or not a number.</param>
  /// <returns>The value.</returns>
  public int GetInt(WidgetProp key, int fallback = 0) =>
      _props.TryGetValue(key, out var value) ? value.AsInt(fallback) : fallback;

  /// <summary>Returns a property as a long whole number.</summary>
  /// <param name="key">The property.</param>
  /// <param name="fallback">The value when the property is missing or not a number.</param>
  /// <returns>The value.</returns>
  public long GetLong(WidgetProp key, long fallback = 0) =>
      _props.TryGetValue(key, out var value) && value.IsNumber ? value.AsLong() : fallback;

  /// <summary>Returns a property as a number.</summary>
  /// <param name="key">The property.</param>
  /// <param name="fallback">The value when the property is missing or not a number.</param>
  /// <returns>The value.</returns>
  public double GetDouble(WidgetProp key, double fallback = 0) =>
      _props.TryGetValue(key, out var value) ? value.AsDouble(fallback) : fallback;

  /// <summary>Returns a property as text.</summary>
  /// <param name="key">The property.</param>
  /// <param name="fallback">The value when the property is missing or not a string.</param>
  /// <returns>The value.</returns>
  public string GetString(WidgetProp key, string fallback = "") =>
      _props.TryGetValue(key, out var value) ? value.AsString(fallback) : fallback;

  /// <summary>
  /// Tells the game that the person did something to this widget. Nothing is sent while the
  /// widget is disabled.
  /// </summary>
  /// <param name="kind">What happened.</param>
  /// <param name="args">The event's arguments.</param>
  /// <returns>A task that completes once the event was sent.</returns>
  public Task<bool> Send(WidgetEvent kind, params UiValue[] args) =>
      IsEnabled ? Panel.SendAsync(Id, kind, args) : Task.FromResult(false);

  /// <summary>
  /// Changes one property and raises <see cref="Changed"/> when the value is different.
  /// </summary>
  /// <param name="key">The property.</param>
  /// <param name="value">The new value, or <see cref="UiValue.Null"/> to remove it.</param>
  internal void Set(WidgetProp key, UiValue value)
  {
    var had = _props.TryGetValue(key, out var old);
    if (value.IsNull)
    {
      if (!had)
      {
        return;
      }

      _props.Remove(key);
    }
    else
    {
      if (had && old!.Equals(value))
      {
        return;
      }

      _props[key] = value;
    }

    Changed?.Invoke(this, key);
  }

  // Takes every property of a newer node of the same widget, raising Changed only for real changes.
  internal void SetAll(IReadOnlyDictionary<WidgetProp, UiValue> props)
  {
    foreach (var key in _props.Keys.ToArray())
    {
      if (!props.ContainsKey(key))
      {
        Set(key, UiValue.Null);
      }
    }

    foreach (var (key, value) in props)
    {
      Set(key, value);
    }
  }

  internal void SetChildren(IReadOnlyList<UiWidget> children)
  {
    if (children.Count == Children.Count)
    {
      var same = true;
      for (var i = 0; i < children.Count && same; i++)
      {
        same = ReferenceEquals(children[i], Children[i]);
      }

      if (same)
      {
        return;
      }
    }

    Children = children;
    ChildrenChanged?.Invoke(this, EventArgs.Empty);
  }
}
