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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Graphs;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The ViewModel of one performance graph: its title, its latest value, and its steady vertical
/// scale.
/// </summary>
/// <remarks>
/// <para>
/// The drawing itself is done by <c>GraphControl</c>. Everything it needs to decide lives here, so
/// it can be tested without a window: the series to draw (<see cref="CopyTo"/>), the scale
/// (<see cref="Scaler"/>, which grows at once and shrinks only when there is plenty of room), and
/// the text of the labels.
/// </para>
/// <para>
/// A graph is created only while its section is expanded and is thrown away when the section
/// collapses, so a hidden graph costs nothing.
/// </para>
/// </remarks>
public sealed partial class GraphViewModel : ObservableObject
{
  private StatsHistory? _stats;
  private PingHistory? _pings;

  [ObservableProperty]
  private string _latestText = "-";

  [ObservableProperty]
  private string _scaleText = string.Empty;

  /// <summary>
  /// Creates a graph.
  /// </summary>
  /// <param name="spec">
  /// Describes the series, the minimum scale, and the number of decimals.
  /// </param>
  public GraphViewModel(GraphSpec spec)
  {
    ArgumentNullException.ThrowIfNull(spec);
    Spec = spec;
    Scaler = new GraphScaler(spec.MinScale);
    ScaleText = FormatScale(Scaler.Scale);
  }

  /// <summary>Raised after <see cref="Update"/>, when the graph should be drawn again.</summary>
  public event EventHandler? Invalidated;

  /// <summary>Gets what the graph shows.</summary>
  public GraphSpec Spec { get; }

  /// <summary>Gets the name above the graph.</summary>
  public string Title => Spec.Title;

  /// <summary>Gets the scaler that keeps the vertical scale steady.</summary>
  public GraphScaler Scaler { get; }

  /// <summary>Gets how many samples the series has right now.</summary>
  public int Count => Spec.Field is not null ? _stats?.Count ?? 0 : _pings?.Count ?? 0;

  /// <summary>
  /// Points the graph at a game's history and starts the scale again.
  /// </summary>
  /// <param name="session">The game to draw, or <see langword="null"/> for none.</param>
  public void Bind(IGameSession? session)
  {
    _stats = session?.Stats;
    _pings = session?.Pings;
    Scaler.Reset();
    Update();
  }

  /// <summary>
  /// Copies the series into a buffer, oldest first. Reuse one buffer so drawing allocates nothing.
  /// </summary>
  /// <param name="destination">The buffer to fill.</param>
  /// <returns>The number of values written.</returns>
  public int CopyTo(Span<double> destination) => Spec.Field is { } field
      ? _stats?.CopyTo(field, destination) ?? 0
      : _pings?.CopyTo(destination) ?? 0;

  /// <summary>
  /// Formats one value of the series with this graph's number of decimals.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>The text.</returns>
  public string FormatValue(double value) =>
      value.ToString(
          "F" + Spec.Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

  /// <summary>
  /// Feeds the largest visible value to the scaler, refreshes the labels, and asks for a redraw.
  /// </summary>
  public void Update()
  {
    var max = Spec.Field is { } field ? _stats?.Max(field) ?? 0 : _pings?.Max() ?? 0;
    var latest = Spec.Field is { } f ? _stats?.Latest(f) ?? 0 : _pings?.Latest ?? 0;
    Scaler.Update(max);
    ScaleText = FormatScale(Scaler.Scale);
    LatestText = Count == 0 ? "-" : FormatValue(latest);
    Invalidated?.Invoke(this, EventArgs.Empty);
  }

  private static string FormatScale(double scale) =>
      scale.ToString("0.##", CultureInfo.InvariantCulture);
}
