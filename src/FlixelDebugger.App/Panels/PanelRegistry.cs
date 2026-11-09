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

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The default <see cref="IPanelRegistry"/>.
/// </summary>
/// <param name="create">Creates the ViewModel of a panel from its id and title.</param>
public sealed class PanelRegistry(Func<PanelDefinition, GamePanelViewModel> create)
    : IPanelRegistry
{
  private readonly List<PanelDefinition> _definitions = [];
  private readonly Dictionary<string, GamePanelViewModel> _viewModels =
      new(StringComparer.Ordinal);

  /// <inheritdoc/>
  public event EventHandler? Changed;

  /// <inheritdoc/>
  public IReadOnlyList<PanelDefinition> Definitions => _definitions;

  /// <inheritdoc/>
  public bool TryGet(string id, out PanelDefinition definition)
  {
    ArgumentNullException.ThrowIfNull(id);
    var index = IndexOf(id);
    definition = index < 0 ? null! : _definitions[index];
    return index >= 0;
  }

  /// <inheritdoc/>
  public GamePanelViewModel GetViewModel(string id)
  {
    ArgumentNullException.ThrowIfNull(id);
    if (_viewModels.TryGetValue(id, out var existing))
    {
      return existing;
    }

    var index = IndexOf(id);
    if (index < 0)
    {
      throw new KeyNotFoundException($"No panel is registered with the id '{id}'.");
    }

    var created = create(_definitions[index]);
    _viewModels[id] = created;
    return created;
  }

  /// <inheritdoc/>
  public bool Register(PanelDefinition definition)
  {
    var added = Upsert(definition, out var changed);
    if (changed)
    {
      Changed?.Invoke(this, EventArgs.Empty);
    }

    return added;
  }

  /// <inheritdoc/>
  public void RegisterAll(IEnumerable<PanelDefinition> definitions)
  {
    ArgumentNullException.ThrowIfNull(definitions);
    var any = false;
    foreach (var definition in definitions)
    {
      Upsert(definition, out var changed);
      any |= changed;
    }

    if (any)
    {
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  private bool Upsert(PanelDefinition definition, out bool changed)
  {
    ArgumentNullException.ThrowIfNull(definition);
    var index = IndexOf(definition.Id);
    if (index < 0)
    {
      _definitions.Add(definition);
      changed = true;
      return true;
    }

    changed = _definitions[index] != definition;
    if (changed)
    {
      _definitions[index] = definition;
      if (_viewModels.TryGetValue(definition.Id, out var viewModel))
      {
        viewModel.Title = definition.Title;
      }
    }

    return false;
  }

  private int IndexOf(string id)
  {
    for (var i = 0; i < _definitions.Count; i++)
    {
      if (string.Equals(_definitions[i].Id, id, StringComparison.Ordinal))
      {
        return i;
      }
    }

    return -1;
  }
}
