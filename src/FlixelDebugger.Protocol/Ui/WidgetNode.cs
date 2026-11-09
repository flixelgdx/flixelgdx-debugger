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
/// One widget of a <c>PANEL_TREE</c>: its type, id, name, the properties the game sent, and its
/// children.
/// </summary>
/// <param name="Type">The widget type. Unknown codes are kept and drawn as nothing.</param>
/// <param name="Id">The id, unique inside the panel.</param>
/// <param name="Name">The optional stable name for tools, or empty.</param>
/// <param name="Props">The properties that differ from their defaults, by key.</param>
/// <param name="Children">The child widgets, in order.</param>
public sealed record WidgetNode(
    WidgetType Type,
    int Id,
    string Name,
    IReadOnlyDictionary<WidgetProp, UiValue> Props,
    IReadOnlyList<WidgetNode> Children)
{
  /// <summary>Reads one widget and all of its children.</summary>
  /// <param name="reader">The reader positioned at the widget's type byte.</param>
  /// <returns>The widget.</returns>
  public static WidgetNode Read(ref MessageReader reader)
  {
    var type = (WidgetType)reader.ReadU8();
    var id = reader.ReadI32();
    var name = reader.ReadString();
    var count = reader.ReadCount();
    var props = new Dictionary<WidgetProp, UiValue>(count);
    for (var i = 0; i < count; i++)
    {
      var key = (WidgetProp)reader.ReadU8();
      props[key] = UiValue.Read(ref reader);
    }

    var childCount = reader.ReadCount();
    var children = new WidgetNode[childCount];
    for (var i = 0; i < childCount; i++)
    {
      children[i] = Read(ref reader);
    }

    return new WidgetNode(type, id, name, props, children);
  }

  /// <summary>Writes this widget and all of its children.</summary>
  /// <param name="writer">The writer.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8((byte)Type).WriteI32(Id).WriteString(Name).BeginArray();
    foreach (var (key, value) in Props.OrderBy(pair => pair.Key))
    {
      writer.WriteU8((byte)key);
      value.Write(writer);
    }

    writer.EndArray(Props.Count).BeginArray();
    foreach (var child in Children)
    {
      child.Write(writer);
    }

    writer.EndArray(Children.Count);
  }

  /// <summary>
  /// Returns a property, or <see langword="null"/> when the game did not send it.
  /// </summary>
  /// <param name="key">The property key.</param>
  /// <returns>The value, or <see langword="null"/>.</returns>
  public UiValue? Get(WidgetProp key) => Props.TryGetValue(key, out var value) ? value : null;

  /// <summary>Walks this widget and every widget below it, parents first.</summary>
  /// <returns>Every widget of the subtree.</returns>
  public IEnumerable<WidgetNode> Walk()
  {
    yield return this;
    foreach (var child in Children)
    {
      foreach (var node in child.Walk())
      {
        yield return node;
      }
    }
  }
}
