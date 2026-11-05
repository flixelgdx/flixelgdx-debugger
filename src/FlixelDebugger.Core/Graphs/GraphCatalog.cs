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

using FlixelDebugger.Core.Data;

namespace FlixelDebugger.Core.Graphs;

/// <summary>
/// Describes one graph of the performance panel.
/// </summary>
/// <param name="Title">The name above the graph.</param>
/// <param name="Field">
/// The stats series to draw, or <see langword="null"/> for the ping series, which the debugger
/// measures itself.
/// </param>
/// <param name="MinScale">The smallest scale of the graph (see <see cref="GraphScaler"/>).</param>
/// <param name="Decimals">How many decimals the values are shown with.</param>
public sealed record GraphSpec(string Title, StatsField? Field, double MinScale, int Decimals);

/// <summary>
/// Describes one collapsible section of the performance panel.
/// </summary>
/// <param name="Title">The section name.</param>
/// <param name="ExpandedByDefault">Whether the section starts open.</param>
/// <param name="Graphs">The graphs inside, top to bottom.</param>
public sealed record GraphSection(string Title, bool ExpandedByDefault, IReadOnlyList<GraphSpec> Graphs);

/// <summary>
/// The sections and graphs of the performance panel.
/// </summary>
/// <remarks>
/// The list lives in Core, not in the UI, so a test can check it and a new graph is one line.
/// Graphs whose minimum scale is 1 simply have no meaningful floor of their own.
/// </remarks>
public static class GraphCatalog
{
  /// <summary>Gets the sections in the order the panel shows them.</summary>
  public static IReadOnlyList<GraphSection> Sections { get; } =
  [
      new("Core", true,
      [
          new("FPS", StatsField.Fps, 10, 0),
          new("Frame ms", StatsField.FrameMs, 16.7, 2),
          new("Draw calls", StatsField.Draws, 10, 0),
      ]),
      new("Render", false,
      [
          new("CPU ms", StatsField.CpuMs, 1, 2),
          new("Present ms", StatsField.PresentMs, 1, 2),
          new("Triangles", StatsField.Triangles, 100, 0),
          new("Vertices", StatsField.Vertices, 1, 0),
          new("Passes", StatsField.Passes, 1, 0),
          new("Submits", StatsField.Submits, 1, 0),
          new("Pipelines", StatsField.Pipelines, 1, 0),
      ]),
      new("Memory", false,
      [
          new("Heap MB", StatsField.HeapMb, 10, 1),
          new("Native MB", StatsField.NativeMb, 1, 1),
          new("Texture MB", StatsField.TextureMb, 1, 1),
          new("Ring KB", StatsField.RingKb, 1, 1),
      ]),
      new("Network", false,
      [
          new("Send KB/s", StatsField.SendKbps, 1, 1),
          new("Receive KB/s", StatsField.RecvKbps, 1, 1),
          new("Ping ms", null, 1, 1),
      ]),
  ];
}
