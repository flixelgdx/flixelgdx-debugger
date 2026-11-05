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
/// Pure helpers for choosing and applying the vertical scale of a performance graph.
/// </summary>
/// <remarks>
/// <para>
/// A graph needs a "top of the chart" value. If it followed the highest sample exactly, the whole
/// picture would jitter on every update. Instead the top is rounded up to a friendly number (1,
/// 1.5, 2, 2.5, 3, 4, 5, 6, 8, or 10 times a power of ten) with 15 percent headroom. Think of a
/// ruler that only comes in a few sizes: you pick the smallest one that fits.
/// </para>
/// <para>
/// To keep the graph steady over time, use <see cref="GraphScaler"/>, which adds hysteresis on top
/// of <see cref="Target"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // 20 (17.2 * 1.15 = 19.78, snapped up to 20)
/// var scale = GraphScale.Target(max: 17.2, minScale: 16.0);
/// // 59 - 0.5 * 58 = 30
/// var y = GraphScale.MapY(value: 10, scale: scale, height: 60);
/// </code>
/// </example>
public static class GraphScale
{
  /// <summary>The headroom factor applied to the largest sample before snapping.</summary>
  public const double Headroom = 1.15;

  private static readonly double[] _steps = [1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0];

  /// <summary>
  /// Rounds a value up to the next "nice" number.
  /// </summary>
  /// <remarks>
  /// The nice numbers are 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, and 10 times 10 to the power n, for any
  /// whole n. A value that already is a nice number is returned unchanged.
  /// </remarks>
  /// <param name="value">The value to round up.</param>
  /// <returns>
  /// The smallest nice number that is at least <paramref name="value"/>, or 0 if the value is not
  /// positive.
  /// </returns>
  public static double NiceCeil(double value)
  {
    if (!(value > 0) || double.IsInfinity(value))
    {
      return 0;
    }

    var exponent = (int)Math.Floor(Math.Log10(value));
    var magnitude = Math.Pow(10, exponent);
    var mantissa = value / magnitude;

    // Floating point can put the mantissa just outside [1, 10), so nudge the exponent back.
    if (mantissa < 1)
    {
      exponent--;
      magnitude = Math.Pow(10, exponent);
      mantissa = value / magnitude;
    }
    else if (mantissa >= 10)
    {
      exponent++;
      magnitude = Math.Pow(10, exponent);
      mantissa = value / magnitude;
    }

    foreach (var step in _steps)
    {
      // The tiny tolerance keeps a value such as 0.3 from jumping to 0.4 because of rounding error.
      if (mantissa <= step * (1 + 1e-9))
      {
        return step * magnitude;
      }
    }

    return 10 * magnitude;
  }

  /// <summary>
  /// Computes the ideal scale for a graph whose largest sample is <paramref name="max"/>.
  /// </summary>
  /// <param name="max">The largest value currently shown.</param>
  /// <param name="minScale">
  /// The smallest scale the graph may have, so a flat line does not zoom in forever.
  /// </param>
  /// <returns><c>max(niceCeil(max * 1.15), minScale)</c>.</returns>
  public static double Target(double max, double minScale) => Math.Max(NiceCeil(max * Headroom), minScale);

  /// <summary>
  /// Maps a value to a pixel row, where row 0 is the top of the graph.
  /// </summary>
  /// <remarks>
  /// The value is clamped to the range 0 to <paramref name="scale"/>, and one pixel is kept free at
  /// the top and at the bottom so the line is never cut off:
  /// <c>y = height - 1 - clamp(value / scale, 0, 1) * (height - 2)</c>.
  /// </remarks>
  /// <param name="value">The value to plot.</param>
  /// <param name="scale">The value at the top of the graph.</param>
  /// <param name="height">The graph height in pixels.</param>
  /// <returns>
  /// The vertical position in pixels. A scale that is not positive puts every value on the bottom
  /// row.
  /// </returns>
  public static double MapY(double value, double scale, double height)
  {
    var ratio = scale > 0 ? Math.Clamp(value / scale, 0, 1) : 0;
    return height - 1 - (ratio * (height - 2));
  }

  /// <summary>
  /// Maps a sample position to a pixel column. The newest sample sits at the right edge, and a
  /// series with fewer samples than the capacity grows in from the right.
  /// </summary>
  /// <param name="index">The sample position, where 0 is the oldest stored sample.</param>
  /// <param name="count">How many samples are stored.</param>
  /// <param name="width">The graph width in pixels.</param>
  /// <param name="capacity">How many samples fit across the whole width.</param>
  /// <returns>The horizontal position in pixels.</returns>
  public static double MapX(int index, int count, double width, int capacity = StatsHistory.Capacity)
  {
    var step = capacity > 1 ? width / (capacity - 1) : 0;
    return width - ((count - 1 - index) * step);
  }

  /// <summary>
  /// Finds the sample nearest to a pixel column, which is the inverse of <see cref="MapX"/>.
  /// </summary>
  /// <param name="x">The horizontal position in pixels.</param>
  /// <param name="count">How many samples are stored.</param>
  /// <param name="width">The graph width in pixels.</param>
  /// <param name="capacity">How many samples fit across the whole width.</param>
  /// <returns>The sample position, or -1 when there are no samples.</returns>
  public static int IndexAt(double x, int count, double width, int capacity = StatsHistory.Capacity)
  {
    if (count <= 0)
    {
      return -1;
    }

    var step = capacity > 1 ? width / (capacity - 1) : 0;
    var back = step > 0 ? (int)Math.Round((width - x) / step) : 0;
    return Math.Clamp(count - 1 - back, 0, count - 1);
  }
}
