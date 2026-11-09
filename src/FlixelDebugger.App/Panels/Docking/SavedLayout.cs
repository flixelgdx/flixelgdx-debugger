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

using System.Text.Json.Serialization;
using Dock.Model.Core;

namespace FlixelDebugger.App.Panels.Docking;

/// <summary>
/// The saved form of the dock layout: only the splits, the tab groups, and the tabs.
/// </summary>
/// <remarks>
/// <para>
/// Dock.Avalonia can serialize its whole model, but that writes about thirty settings for every
/// tab, group, and splitter, almost all of them defaults. This record keeps only what the debugger
/// needs to rebuild the layout, and <see cref="DockPanelLayout"/> converts between the two.
/// Splitters are not stored, because one always sits between two neighbors of a split.
/// </para>
/// <para>
/// A file with a different <see cref="Version"/> is ignored, so the default layout is used instead
/// of a layout this version cannot read.
/// </para>
/// </remarks>
internal sealed class SavedLayout
{
  /// <summary>The format version this code writes and reads.</summary>
  public const int CurrentVersion = 1;

  /// <summary>Gets or sets the format version of the saved text.</summary>
  public int Version { get; set; }

  /// <summary>Gets or sets the top split of the layout.</summary>
  public SavedNode? Root { get; set; }

  /// <summary>Gets or sets the closed tabs, with the group each one was closed from.</summary>
  public List<SavedTab>? Hidden { get; set; }
}

/// <summary>
/// One part of the saved layout: a split when <see cref="Split"/> is set, otherwise a tab group.
/// </summary>
internal sealed class SavedNode
{
  /// <summary>
  /// Gets or sets the id of the dock, or <see langword="null"/> when it has none.
  /// </summary>
  public string? Id { get; set; }

  /// <summary>
  /// Gets or sets the share of the parent split this part takes, or <see langword="null"/> to share
  /// the space evenly.
  /// </summary>
  public double? Size { get; set; }

  /// <summary>
  /// Gets or sets the direction of a split, or <see langword="null"/> for a tab group.
  /// </summary>
  [JsonConverter(typeof(JsonStringEnumConverter<Orientation>))]
  public Orientation? Split { get; set; }

  /// <summary>Gets or sets the parts of a split, in order.</summary>
  public List<SavedNode>? Children { get; set; }

  /// <summary>Gets or sets the open tabs of a group, in order.</summary>
  public List<SavedTab>? Tabs { get; set; }

  /// <summary>
  /// Gets or sets the id of the tab in front in a group, or <see langword="null"/> for the first.
  /// </summary>
  public string? Active { get; set; }
}

/// <summary>
/// One saved tab: the panel id and the title to show until a game announces the panel again.
/// </summary>
internal sealed class SavedTab
{
  /// <summary>Gets or sets the panel id.</summary>
  public string Id { get; set; } = "";

  /// <summary>Gets or sets the tab title.</summary>
  public string? Title { get; set; }

  /// <summary>
  /// Gets or sets the group a closed tab came from, so it reopens there. Open tabs leave it out.
  /// </summary>
  public string? Group { get; set; }
}

/// <summary>
/// The generated JSON metadata for <see cref="SavedLayout"/>, which avoids reflection at run time.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SavedLayout))]
internal sealed partial class SavedLayoutJsonContext : JsonSerializerContext;
