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

namespace FlixelDebugger.Core.Tests;

public class StatsHistoryTests
{
    [Fact]
    public void StartsEmpty()
    {
        var history = new StatsHistory();
        Assert.Equal(0, history.Count);
        Assert.Null(history.LatestPayload);
        Assert.Equal(0, history.Latest(StatsField.Fps));
        Assert.Empty(history.GetSeries(StatsField.Fps));
    }

    [Fact]
    public void KeepsSamplesInArrivalOrder()
    {
        var history = new StatsHistory();
        for (var i = 1; i <= 5; i++)
        {
            history.Add(TestData.Stats(i));
        }

        Assert.Equal(5, history.Count);
        Assert.Equal([1d, 2, 3, 4, 5], history.GetSeries(StatsField.Fps));
        Assert.Equal(5, history.Latest(StatsField.Fps));
    }

    [Fact]
    public void RingWrapsAfterOneHundredTwentySamples()
    {
        var history = new StatsHistory();
        for (var i = 0; i < 130; i++)
        {
            history.Add(TestData.Stats(i));
        }

        var series = history.GetSeries(StatsField.Fps);
        Assert.Equal(StatsHistory.Capacity, history.Count);
        Assert.Equal(120, series.Length);
        Assert.Equal(10, series[0]);
        Assert.Equal(129, series[^1]);
        Assert.Equal(10, history.At(StatsField.Fps, 0));
        Assert.Equal(129, history.At(StatsField.Fps, 119));
        Assert.Equal(130, history.TotalSamples);
    }

    [Fact]
    public void EveryFieldHasItsOwnRing()
    {
        var history = new StatsHistory();
        history.Add(TestData.Stats(60, paused: true));
        Assert.Equal(60, history.Latest(StatsField.Fps));
        Assert.Equal(100, history.Latest(StatsField.Draws));
        Assert.Equal(2000, history.Latest(StatsField.Triangles));
        Assert.Equal(250, history.Latest(StatsField.ObjectCount));
        Assert.Equal(1, history.Latest(StatsField.Paused));
        Assert.Equal(12.5, history.Latest(StatsField.Elapsed));
        Assert.Equal(1.5, history.Latest(StatsField.CamZoom));
    }

    [Fact]
    public void CopyToFillsACallerBufferWithoutAllocating()
    {
        var history = new StatsHistory();
        for (var i = 0; i < 125; i++)
        {
            history.Add(TestData.Stats(i));
        }

        Span<double> buffer = stackalloc double[StatsHistory.Capacity];
        var count = history.CopyTo(StatsField.Fps, buffer);
        Assert.Equal(120, count);
        Assert.Equal(5, buffer[0]);
        Assert.Equal(124, buffer[119]);
    }

    [Fact]
    public void CopyToWithASmallBufferKeepsTheNewestSamples()
    {
        var history = new StatsHistory();
        for (var i = 0; i < 10; i++)
        {
            history.Add(TestData.Stats(i));
        }

        Span<double> buffer = stackalloc double[3];
        Assert.Equal(3, history.CopyTo(StatsField.Fps, buffer));
        Assert.Equal([7d, 8, 9], buffer.ToArray());
    }

    [Fact]
    public void MaxScansTheStoredWindowOnly()
    {
        var history = new StatsHistory();
        history.Add(TestData.Stats(500));
        for (var i = 0; i < 120; i++)
        {
            history.Add(TestData.Stats(i));
        }

        // The 500 sample was pushed out of the ring by 120 newer ones.
        Assert.Equal(119, history.Max(StatsField.Fps));
    }

    [Fact]
    public void AtRejectsAnIndexPastTheEnd()
    {
        var history = new StatsHistory();
        history.Add(TestData.Stats(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.At(StatsField.Fps, 1));
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var history = new StatsHistory();
        history.Add(TestData.Stats(1));
        history.Clear();
        Assert.Equal(0, history.Count);
        Assert.Null(history.LatestPayload);
        Assert.Equal(0, history.Latest(StatsField.Fps));
    }
}
