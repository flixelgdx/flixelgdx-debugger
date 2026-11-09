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

using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Tests.Fakes;

/// <summary>Short builders for widget trees in tests.</summary>
internal static class TestWidgets
{
  public static UiValue S(string text) => UiValue.FromString(text);

  public static UiValue I(int value) => UiValue.FromInt(value);

  public static UiValue L(long value) => UiValue.FromLong(value);

  public static UiValue F(float value) => UiValue.FromFloat(value);

  public static UiValue B(bool value) => UiValue.FromBool(value);

  public static UiValue List(params UiValue[] items) => UiValue.FromList(items);

  public static WidgetNode Node(
      WidgetType type, int id, string name = "", params (WidgetProp Key, UiValue Value)[] props) =>
      new(type, id, name, props.ToDictionary(p => p.Key, p => p.Value), []);

  public static WidgetNode With(this WidgetNode node, params WidgetNode[] children) =>
      node with { Children = children };

  public static UiValue Row(long id, params string[] cells) =>
      List(L(id), I(0), List([.. cells.Select(S)]), S(string.Empty));

  /// <summary>A panel that uses every widget type once.</summary>
  public static WidgetNode Everything() =>
      Node(WidgetType.Stack, 1).With(
          Node(
              WidgetType.Label,
              2,
              "title",
              (WidgetProp.Text, S("Hello")),
              (WidgetProp.Style, I(64))),
          Node(WidgetType.Wrap, 3).With(
              Node(WidgetType.Button, 4, "go", (WidgetProp.Text, S("Go"))),
              Node(WidgetType.Checkbox, 5, "flag", (WidgetProp.Text, S("Flag"))),
              Node(WidgetType.Toggle, 6, "toggle", (WidgetProp.Text, S("Toggle"))),
              Node(WidgetType.Dropdown, 7, "mode", (WidgetProp.Items, List(S("a"), S("b"))))),
          Node(WidgetType.Grid, 8, "", (WidgetProp.Columns, List(S("auto"), S("*")))).With(
              Node(WidgetType.Label, 9, "", (WidgetProp.Text, S("Name"))),
              Node(WidgetType.TextBox, 10, "name", (WidgetProp.Live, B(true))),
              Node(WidgetType.Label, 11, "", (WidgetProp.Text, S("Count"))),
              Node(WidgetType.Number, 12, "count", (WidgetProp.Value, UiValue.FromDouble(3)))),
          Node(WidgetType.Slider, 13, "speed"),
          Node(WidgetType.Progress, 14, "", (WidgetProp.Value, UiValue.FromDouble(0.5))),
          Node(WidgetType.Image, 15),
          Node(WidgetType.Separator, 16),
          Node(WidgetType.Color, 17, "tint", (WidgetProp.Value, I(unchecked((int)0xFF336699)))),
          Node(WidgetType.Tabs, 18, "", (WidgetProp.Items, List(S("One"), S("Two")))).With(
              Node(WidgetType.Scroll, 19).With(
                  Node(WidgetType.Table, 20, "rows",
                      (WidgetProp.Columns, List(List(S("Name"), F(0), I(1)))),
                      (WidgetProp.Items, List(Row(1, "slime"), Row(2, "bat"))))),
              Node(WidgetType.Split, 21, "", (WidgetProp.Horizontal, B(true))).With(
                  Node(WidgetType.Tree, 22, "tree", (WidgetProp.Items, List(
                      List(L(1), L(-1), S("root"), S(""), I(256), UiValue.EmptyList),
                      List(L(2), L(1), S("child"), S("x"), I(0), List(List(S("new"), I(2))))))),
                  Node(WidgetType.Expander, 23, "", (WidgetProp.Text, S("More"))).With(
                      Node(WidgetType.Properties, 24, "props", (WidgetProp.Sections, List(List(
                          S("Main"), B(true), List(List(S("hp"), I(5), B(true)))))))))),
          Node(WidgetType.Graph, 25, "fps",
              (WidgetProp.Series, List(List(S("fps"), I(0), S("")))),
              (WidgetProp.Samples, List(List(F(60)), List(F(58))))),
          Node(WidgetType.Canvas, 26, "map",
              (WidgetProp.View, List(F(320), F(180))),
              (WidgetProp.Shapes, List(List(L(3), F(10), F(10), F(20), F(20), I(16), S("box"))))),
          Node((WidgetType)99, 27));
}
