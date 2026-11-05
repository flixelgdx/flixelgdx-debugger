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
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Formatting;

/// <summary>
/// The groups of the stats panel.
/// </summary>
public enum StatsGroup
{
  /// <summary>Numbers about the running client: speed, memory, and drawing.</summary>
  Client,

  /// <summary>The state of the game world.</summary>
  World,

  /// <summary>The main camera.</summary>
  Camera,
}

/// <summary>
/// How a value in the stats panel should be colored.
/// </summary>
public enum StatsTone
{
  /// <summary>The normal text color.</summary>
  Normal,

  /// <summary>Good news: green.</summary>
  Good,

  /// <summary>Something to notice: amber.</summary>
  Warning,
}

/// <summary>
/// One row of the stats panel.
/// </summary>
/// <param name="Group">The group the row belongs to.</param>
/// <param name="Name">The label in the left column.</param>
/// <param name="Value">The text in the right column.</param>
/// <param name="Tone">How to color the value.</param>
public readonly record struct StatsEntry(StatsGroup Group, string Name, string Value, StatsTone Tone);

/// <summary>
/// Builds the rows of the stats panel from the newest sample.
/// </summary>
public static class StatsTable
{
  /// <summary>The text shown for a value that is not known yet.</summary>
  public const string Unknown = "-";

  /// <summary>The text shown for a ping that was not measured yet.</summary>
  public const string PingUnknown = "n/a";

  /// <summary>The number of rows <see cref="Build"/> returns.</summary>
  public const int RowCount = 13;

  /// <summary>
  /// Builds all rows.
  /// </summary>
  /// <param name="sample">
  /// The newest stats sample, or <see langword="null"/> before the first one.
  /// </param>
  /// <param name="pingMs">
  /// The last measured ping in milliseconds, or <see langword="null"/> if none was measured.
  /// </param>
  /// <returns>Exactly <see cref="RowCount"/> rows, always in the same order.</returns>
  public static StatsEntry[] Build(StatsPayload? sample, double? pingMs)
  {
    var ping = pingMs is { } ms ? Fixed(ms, 0) + " ms" : PingUnknown;
    var state = sample is null ? Unknown : sample.Paused ? "PAUSED" : "RUNNING";
    var tone = sample is null ? StatsTone.Normal : sample.Paused ? StatsTone.Warning : StatsTone.Good;
    return
    [
        new(StatsGroup.Client, "FPS", Of(sample, s => Fixed(s.Fps, 0)), StatsTone.Normal),
        new(StatsGroup.Client, "Frame ms", Of(sample, s => Fixed(s.FrameMs, 2)), StatsTone.Normal),
        new(StatsGroup.Client, "Java heap MB", Of(sample, s => Fixed(s.HeapMb, 1)), StatsTone.Normal),
        new(StatsGroup.Client, "Native MB", Of(sample, s => Fixed(s.NativeMb, 1)), StatsTone.Normal),
        new(StatsGroup.Client, "Objects", Of(sample, s => s.ObjectCount.ToString(CultureInfo.InvariantCulture)), StatsTone.Normal),
        new(StatsGroup.Client, "Draw calls", Of(sample, s => s.Draws.ToString(CultureInfo.InvariantCulture)), StatsTone.Normal),
        new(StatsGroup.Client, "Triangles", Of(sample, s => s.Triangles.ToString(CultureInfo.InvariantCulture)), StatsTone.Normal),
        new(StatsGroup.Client, "Ping", ping, StatsTone.Normal),
        new(StatsGroup.World, "State", state, tone),
        new(StatsGroup.World, "Elapsed", Of(sample, s => Fixed(s.Elapsed, 1) + " s"), StatsTone.Normal),
        new(StatsGroup.Camera, "X", Of(sample, s => Fixed(s.CamX, 1)), StatsTone.Normal),
        new(StatsGroup.Camera, "Y", Of(sample, s => Fixed(s.CamY, 1)), StatsTone.Normal),
        new(StatsGroup.Camera, "Zoom", Of(sample, s => Fixed(s.CamZoom, 2)), StatsTone.Normal),
    ];
  }

  private static string Of(StatsPayload? sample, Func<StatsPayload, string> format) => sample is null ? Unknown : format(sample);

  private static string Fixed(double value, int decimals) =>
      value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
