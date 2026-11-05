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

using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.Core.Formatting;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// One row of a property grid: a name and either an editor or plain text.
/// </summary>
/// <remarks>
/// <para>
/// The inspector and the custom panels share this class. Which editor a row gets depends on its kind: a checkbox for
/// a bool, a text box for a number or a string, plain text for a read-only row, and red <c>error: message</c> text
/// for a value the game failed to read.
/// </para>
/// <para>
/// <b>Editing.</b> A text editor commits when the person presses Enter or leaves the box
/// (<see cref="CommitAsync"/>), never on every key. A checkbox commits at once. While the person edits, a new value from
/// the game is only remembered, not shown, so typing is never overwritten; it appears when the edit ends. An edit
/// that was not changed sends nothing, and text that does not parse is dropped and the game's value is shown again.
/// </para>
/// </remarks>
public sealed partial class PropertyRowViewModel : ObservableObject
{
  private readonly Func<int, PropertyValue, Task> _commit;
  private PropertyRow _latest;
  private string _editStartText = string.Empty;
  private bool _applying;

  [ObservableProperty]
  private string _displayText = string.Empty;

  [ObservableProperty]
  private string _editText = string.Empty;

  [ObservableProperty]
  private bool _boolValue;

  [ObservableProperty]
  private bool _hasInputError;

  /// <summary>
  /// Creates a row.
  /// </summary>
  /// <param name="index">The position of the row, which is what the protocol uses to name it.</param>
  /// <param name="row">The row from the game.</param>
  /// <param name="commit">Sends an edit: it receives the row's index and the new value.</param>
  public PropertyRowViewModel(int index, PropertyRow row, Func<int, PropertyValue, Task> commit)
  {
    ArgumentNullException.ThrowIfNull(commit);
    Index = index;
    Name = row.Name;
    Kind = row.Value.Kind;
    Editable = row.Editable;
    _commit = commit;
    _latest = row;
    Apply(row);
  }

  /// <summary>Gets the position of the row in its section or panel.</summary>
  public int Index { get; }

  /// <summary>Gets the row name.</summary>
  public string Name { get; }

  /// <summary>Gets the kind of the value.</summary>
  public ValueKind Kind { get; }

  /// <summary>Gets whether the game accepts edits of this row.</summary>
  public bool Editable { get; }

  /// <summary>Gets whether the person is editing the row right now.</summary>
  public bool IsEditing { get; private set; }

  /// <summary>Gets whether the game failed to read the value.</summary>
  public bool IsError => Kind == ValueKind.Error;

  /// <summary>Gets whether the row shows a checkbox.</summary>
  public bool IsBoolEditor => Editable && Kind == ValueKind.Bool;

  /// <summary>Gets whether the row shows a text box.</summary>
  public bool IsTextEditor => Editable && Kind is ValueKind.Int or ValueKind.Long or ValueKind.Float or ValueKind.Double or ValueKind.String;

  /// <summary>Gets whether the row shows plain text.</summary>
  public bool IsPlainText => !IsBoolEditor && !IsTextEditor && !IsError;

  /// <summary>
  /// Tells whether a new row from the game fits this ViewModel, so it can be updated in place.
  /// </summary>
  /// <param name="row">The row from the game.</param>
  /// <returns><see langword="true"/> if the name, kind, and editable flag are unchanged.</returns>
  public bool Matches(PropertyRow row) => row.Name == Name && row.Value.Kind == Kind && row.Editable == Editable;

  /// <summary>
  /// Takes a new value from the game. While the row is being edited the value is kept for later.
  /// </summary>
  /// <param name="row">The row from the game.</param>
  public void Update(PropertyRow row)
  {
    _latest = row;
    if (!IsEditing)
    {
      Apply(row);
    }
  }

  /// <summary>
  /// Marks the start of an edit, which protects the typed text from updates.
  /// </summary>
  public void BeginEdit()
  {
    if (!IsEditing)
    {
      IsEditing = true;
      _editStartText = EditText;
    }
  }

  /// <summary>
  /// Ends the edit and sends the typed value if it is valid and changed.
  /// </summary>
  /// <returns>A task that completes when the value was sent.</returns>
  public async Task CommitAsync()
  {
    if (!IsEditing)
    {
      return;
    }

    IsEditing = false;
    if (EditText == _editStartText)
    {
      Apply(_latest);
      return;
    }

    if (ValueFormat.TryParse(Kind, EditText, out var value))
    {
      // Keep the typed text until the game confirms it with the next update.
      HasInputError = false;
      await _commit(Index, value);
    }
    else
    {
      Apply(_latest);
    }
  }

  /// <summary>
  /// Ends the edit without sending anything and shows the game's value again.
  /// </summary>
  public void CancelEdit()
  {
    IsEditing = false;
    Apply(_latest);
  }

  partial void OnBoolValueChanged(bool value)
  {
    if (!_applying && IsBoolEditor)
    {
      _ = _commit(Index, PropertyValue.FromBool(value));
    }
  }

  private void Apply(PropertyRow row)
  {
    _applying = true;
    DisplayText = ValueFormat.Display(row.Value);
    EditText = ValueFormat.EditText(row.Value);
    BoolValue = row.Value.Kind == ValueKind.Bool && row.Value.AsBool();
    HasInputError = false;
    _applying = false;
  }
}
