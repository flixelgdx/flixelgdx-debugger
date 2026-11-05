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
using Microsoft.Extensions.DependencyInjection;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// The default <see cref="IPanelRegistry"/>.
/// </summary>
public sealed class PanelRegistry : IPanelRegistry
{
    private readonly IServiceProvider _services;
    private readonly List<PanelDefinition> _definitions = [];
    private readonly Dictionary<string, PanelViewModel> _viewModels = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a registry.
    /// </summary>
    /// <param name="services">Passed to the factories that create the ViewModels.</param>
    /// <param name="definitions">The panels that exist from the start, in menu order.</param>
    public PanelRegistry(IServiceProvider services, IEnumerable<PanelDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(definitions);
        _services = services;
        foreach (var definition in definitions)
        {
            Upsert(definition);
        }
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public IReadOnlyList<PanelDefinition> Definitions => _definitions;

    /// <inheritdoc/>
    public bool TryGet(string id, out PanelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(id);
        var index = IndexOf(id);
        definition = index >= 0 ? _definitions[index] : null!;
        return index >= 0;
    }

    /// <inheritdoc/>
    public bool TryFind(string idOrTitle, out PanelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(idOrTitle);
        var text = idOrTitle.Trim();
        foreach (var candidate in _definitions)
        {
            if (string.Equals(candidate.Id, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Title, text, StringComparison.OrdinalIgnoreCase))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null!;
        return false;
    }

    /// <inheritdoc/>
    public PanelViewModel GetViewModel(string id)
    {
        if (!TryGet(id, out var definition))
        {
            throw new KeyNotFoundException($"No panel is registered with the id '{id}'.");
        }

        if (!_viewModels.TryGetValue(id, out var viewModel))
        {
            viewModel = definition.Create(_services);
            _viewModels[id] = viewModel;
        }

        return viewModel;
    }

    /// <inheritdoc/>
    public void Register(PanelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Upsert(definition);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public bool RegisterCustom(string id, string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (IndexOf(id) >= 0)
        {
            return false;
        }

        var name = string.IsNullOrWhiteSpace(title) ? id : title;
        Register(new PanelDefinition(id, name, sp => new CustomPanelViewModel(
            id,
            name,
            sp.GetRequiredService<Sessions.IActiveSession>(),
            sp.GetRequiredService<Services.IUiScheduler>())));
        return true;
    }

    private int IndexOf(string id) => _definitions.FindIndex(d => string.Equals(d.Id, id, StringComparison.Ordinal));

    private void Upsert(PanelDefinition definition)
    {
        var index = IndexOf(definition.Id);
        if (index >= 0)
        {
            _definitions[index] = definition;
            _viewModels.Remove(definition.Id);
        }
        else
        {
            _definitions.Add(definition);
        }
    }
}
