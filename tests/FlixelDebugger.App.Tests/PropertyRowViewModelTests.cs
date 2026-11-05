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

using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.Tests;

public class PropertyRowViewModelTests
{
  private readonly List<(int Index, PropertyValue Value)> _sent = [];

  private PropertyRowViewModel Make(
      PropertyValue value, bool editable = true, string name = "prop", int index = 3) =>
      new(index, TestData.Row(name, value, editable), (i, v) =>
      {
        _sent.Add((i, v));
        return Task.CompletedTask;
      });

  [Fact]
  public void EditorsFollowTheKindOfTheRow()
  {
    Assert.True(Make(PropertyValue.FromBool(true)).IsBoolEditor);
    Assert.True(Make(PropertyValue.FromInt(1)).IsTextEditor);
    Assert.True(Make(PropertyValue.FromLong(1)).IsTextEditor);
    Assert.True(Make(PropertyValue.FromFloat(1)).IsTextEditor);
    Assert.True(Make(PropertyValue.FromDouble(1)).IsTextEditor);
    Assert.True(Make(PropertyValue.FromString("a")).IsTextEditor);
    Assert.True(Make(PropertyValue.FromInt(1), editable: false).IsPlainText);
    Assert.True(Make(PropertyValue.FromBool(true), editable: false).IsPlainText);
  }

  [Fact]
  public void AnErrorRowIsNeverEditableAndShowsTheMessage()
  {
    var row = Make(PropertyValue.FromError("boom"));

    Assert.True(row.IsError);
    Assert.False(row.IsTextEditor);
    Assert.False(row.IsBoolEditor);
    Assert.False(row.IsPlainText);
    Assert.Equal("error: boom", row.DisplayText);
  }

  [Fact]
  public void ReadOnlyRowsFormatNumbersByTheSharedRules()
  {
    Assert.Equal("1.50", Make(PropertyValue.FromFloat(1.5f), editable: false).DisplayText);
    Assert.Equal("3", Make(PropertyValue.FromDouble(3.0), editable: false).DisplayText);
    Assert.Equal("true", Make(PropertyValue.FromBool(true), editable: false).DisplayText);
  }

  [Fact]
  public async Task ACheckboxCommitsAtOnceWithTheRowIndex()
  {
    var row = Make(PropertyValue.FromBool(true));

    row.BoolValue = false;
    await Task.Yield();

    Assert.Equal([(3, PropertyValue.FromBool(false))], _sent);
  }

  [Fact]
  public void ANewValueFromTheGameDoesNotLookLikeAnEdit()
  {
    var row = Make(PropertyValue.FromBool(true));

    row.Update(TestData.Row("prop", PropertyValue.FromBool(false), true));

    Assert.False(row.BoolValue);
    Assert.Empty(_sent);
  }

  [Theory]
  [InlineData(ValueKind.Int, "43")]
  [InlineData(ValueKind.Long, "9000000000")]
  [InlineData(ValueKind.Float, "2.25")]
  [InlineData(ValueKind.Double, "-0.5")]
  [InlineData(ValueKind.String, "hello world")]
  public async Task ATextEditorCommitsAValueOfTheSameKind(ValueKind kind, string text)
  {
    var start = kind switch
    {
      ValueKind.Int => PropertyValue.FromInt(1),
      ValueKind.Long => PropertyValue.FromLong(1),
      ValueKind.Float => PropertyValue.FromFloat(1),
      ValueKind.Double => PropertyValue.FromDouble(1),
      _ => PropertyValue.FromString("x"),
    };
    var row = Make(start);

    row.BeginEdit();
    row.EditText = text;
    await row.CommitAsync();

    var (index, value) = Assert.Single(_sent);
    Assert.Equal(3, index);
    Assert.Equal(kind, value.Kind);
    Assert.False(row.IsEditing);
    Assert.True(Core.Formatting.ValueFormat.TryParse(kind, text, out var expected));
    Assert.Equal(expected, value);
  }

  [Fact]
  public async Task AnUnchangedEditSendsNothing()
  {
    var row = Make(PropertyValue.FromFloat(0.1f));

    row.BeginEdit();
    await row.CommitAsync();

    Assert.Empty(_sent);
    Assert.Equal("0.1", row.EditText);
  }

  [Fact]
  public async Task TextThatIsNotAValidNumberIsDroppedAndTheGamesValueReturns()
  {
    var row = Make(PropertyValue.FromInt(42));

    row.BeginEdit();
    row.EditText = "forty";
    await row.CommitAsync();

    Assert.Empty(_sent);
    Assert.Equal("42", row.EditText);
  }

  [Fact]
  public void WhileARowIsBeingEditedIncomingValuesAreHeldBack()
  {
    var row = Make(PropertyValue.FromString("a"));
    row.BeginEdit();
    row.EditText = "typing";

    row.Update(TestData.Row("prop", PropertyValue.FromString("from game"), true));

    Assert.Equal("typing", row.EditText);
    Assert.Equal("a", row.DisplayText);
    Assert.True(row.IsEditing);

    row.CancelEdit();

    Assert.Equal("from game", row.EditText);
    Assert.Equal("from game", row.DisplayText);
  }

  [Fact]
  public async Task TheHeldBackValueAppearsAfterTheEditEnds()
  {
    var row = Make(PropertyValue.FromString("a"));
    row.BeginEdit();
    row.Update(TestData.Row("prop", PropertyValue.FromString("b"), true));

    await row.CommitAsync();

    Assert.Equal("b", row.EditText);
    Assert.Empty(_sent);
  }

  [Fact]
  public void MatchesComparesNameKindAndEditableFlag()
  {
    var row = Make(PropertyValue.FromInt(1), name: "n");

    Assert.True(row.Matches(TestData.Row("n", PropertyValue.FromInt(9), true)));
    Assert.False(row.Matches(TestData.Row("other", PropertyValue.FromInt(9), true)));
    Assert.False(row.Matches(TestData.Row("n", PropertyValue.FromLong(9), true)));
    Assert.False(row.Matches(TestData.Row("n", PropertyValue.FromInt(9), false)));
  }

  [Fact]
  public void AGridKeepsRowsThatStillMatchAndReplacesTheOthers()
  {
    var grid = new PropertyGridViewModel((_, _) => Task.CompletedTask);
    grid.Update(
        [TestData.Row("a", PropertyValue.FromInt(1), true),
            TestData.Row("b", PropertyValue.FromInt(2), true)]);
    var a = grid.Rows[0];
    var b = grid.Rows[1];

    grid.Update(
        [TestData.Row("a", PropertyValue.FromInt(10), true),
            TestData.Row("b", PropertyValue.FromString("now text"), true),
            TestData.Row("c", PropertyValue.FromBool(true))]);

    Assert.Same(a, grid.Rows[0]);
    Assert.Equal("10", a.DisplayText);
    Assert.NotSame(b, grid.Rows[1]);
    Assert.Equal(ValueKind.String, grid.Rows[1].Kind);
    Assert.Equal(3, grid.Rows.Count);
    Assert.Equal([0, 1, 2], grid.Rows.Select(r => r.Index));

    grid.Update([]);

    Assert.Empty(grid.Rows);
  }

  [Fact]
  public void AGridKeepsTheRowBeingEditedEvenWhenItsKindChanges()
  {
    var grid = new PropertyGridViewModel((_, _) => Task.CompletedTask);
    grid.Update([TestData.Row("a", PropertyValue.FromInt(1), true)]);
    var a = grid.Rows[0];
    a.BeginEdit();
    a.EditText = "77";

    grid.Update([TestData.Row("a", PropertyValue.FromError("gone"), true)]);

    Assert.Same(a, grid.Rows[0]);
    Assert.Equal("77", a.EditText);
    Assert.True(grid.IsEditing);
  }
}
