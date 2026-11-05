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

namespace FlixelDebugger.Core.Graphs;

/// <summary>
/// Keeps the scale of one graph steady over time: it grows at once and shrinks only when there is plenty of room.
/// </summary>
/// <remarks>
/// <para>
/// A spike must never be cut off, so a bigger target is taken immediately. A smaller target is ignored until it
/// falls below half of the current scale. Without this the chart would rescale every time a spike scrolled out
/// of view. Think of a camera lens that zooms out instantly but zooms back in slowly.
/// </para>
/// <para>This type is not thread safe; use one per graph on the UI thread.</para>
/// </remarks>
/// <example>
/// <code>
/// var scaler = new GraphScaler(minScale: 16.7);
/// var scale = scaler.Update(statsHistory.Max(StatsField.FrameMs));
/// </code>
/// </example>
public sealed class GraphScaler
{
    /// <summary>
    /// Creates a scaler.
    /// </summary>
    /// <param name="minScale">The smallest scale the graph may have.</param>
    public GraphScaler(double minScale)
    {
        MinScale = minScale;
        Scale = minScale;
    }

    /// <summary>Gets the smallest scale the graph may have.</summary>
    public double MinScale { get; }

    /// <summary>Gets the current scale, which is the value at the top of the graph.</summary>
    public double Scale { get; private set; }

    /// <summary>
    /// Feeds the largest visible sample and returns the new scale.
    /// </summary>
    /// <param name="max">The largest value currently shown.</param>
    /// <returns>The scale to draw with.</returns>
    public double Update(double max)
    {
        var target = GraphScale.Target(max, MinScale);
        if (target > Scale || target < Scale / 2)
        {
            Scale = target;
        }

        return Scale;
    }

    /// <summary>
    /// Maps a value to a pixel row using the current scale.
    /// </summary>
    /// <param name="value">The value to plot.</param>
    /// <param name="height">The graph height in pixels.</param>
    /// <returns>The vertical position in pixels, where 0 is the top.</returns>
    public double MapY(double value, double height) => GraphScale.MapY(value, Scale, height);

    /// <summary>
    /// Forgets the current scale and starts again from the minimum.
    /// </summary>
    public void Reset() => Scale = MinScale;
}
