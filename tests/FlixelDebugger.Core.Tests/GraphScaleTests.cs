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

using FlixelDebugger.Core.Graphs;

namespace FlixelDebugger.Core.Tests;

public class GraphScaleTests
{
  [Theory]
  [InlineData(1, 1)]
  [InlineData(0.9, 1)]
  [InlineData(1.1, 1.5)]
  [InlineData(1.5, 1.5)]
  [InlineData(1.6, 2)]
  [InlineData(2.2, 2.5)]
  [InlineData(2.6, 3)]
  [InlineData(3.1, 4)]
  [InlineData(4.5, 5)]
  [InlineData(5.5, 6)]
  [InlineData(7, 8)]
  [InlineData(9, 10)]
  [InlineData(10, 10)]
  [InlineData(11, 15)]
  [InlineData(16, 20)]
  [InlineData(100, 100)]
  [InlineData(101, 150)]
  [InlineData(0.3, 0.3)]
  [InlineData(0.07, 0.08)]
  [InlineData(12345, 15000)]
  public void NiceCeilSnapsUpToTheNextStep(double value, double expected)
  {
    Assert.Equal(expected, GraphScale.NiceCeil(value), 9);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-3)]
  [InlineData(double.NaN)]
  public void NiceCeilOfANonPositiveValueIsZero(double value)
  {
    Assert.Equal(0, GraphScale.NiceCeil(value));
  }

  [Fact]
  public void TargetAddsFifteenPercentHeadroomBeforeSnapping()
  {
    // 17.2 * 1.15 = 19.78, which snaps up to 20.
    Assert.Equal(20, GraphScale.Target(17.2, 0), 9);

    // 100 * 1.15 = 115, which snaps up to 150 rather than staying at 100.
    Assert.Equal(150, GraphScale.Target(100, 0), 9);
  }

  [Fact]
  public void TargetNeverGoesBelowTheMinimumScale()
  {
    Assert.Equal(16, GraphScale.Target(1, 16));
    Assert.Equal(16, GraphScale.Target(0, 16));
    Assert.Equal(20, GraphScale.Target(17.2, 16));
  }

  [Fact]
  public void ScalerStartsAtTheMinimumScale()
  {
    Assert.Equal(16, new GraphScaler(16).Scale);
  }

  [Fact]
  public void ScalerGrowsImmediately()
  {
    var scaler = new GraphScaler(1);
    Assert.Equal(20, scaler.Update(17.2), 9);
    Assert.Equal(150, scaler.Update(100), 9);
  }

  [Fact]
  public void ScalerKeepsTheScaleWhileTheTargetIsAtLeastHalf()
  {
    var scaler = new GraphScaler(1);
    scaler.Update(100);
    Assert.Equal(150, scaler.Scale, 9);

    // Target 80 is above half of 150 (75), so nothing changes.
    Assert.Equal(150, scaler.Update(60), 9);
  }

  [Fact]
  public void ScalerKeepsTheScaleWhenTheTargetIsExactlyHalf()
  {
    var scaler = new GraphScaler(1);
    scaler.Update(17.2);
    Assert.Equal(20, scaler.Scale, 9);

    // Target 10 is exactly half of 20. Only a target below half shrinks the scale.
    Assert.Equal(20, scaler.Update(8), 9);
  }

  [Fact]
  public void ScalerShrinksOnceTheTargetFallsBelowHalf()
  {
    var scaler = new GraphScaler(1);
    scaler.Update(17.2);

    // Target 8 is below half of 20, so the scale shrinks to the target itself.
    Assert.Equal(8, scaler.Update(6), 9);
  }

  [Fact]
  public void ScalerNeverShrinksBelowTheMinimum()
  {
    var scaler = new GraphScaler(16);
    scaler.Update(500);
    Assert.Equal(16, scaler.Update(0));
  }

  [Fact]
  public void ResetReturnsToTheMinimum()
  {
    var scaler = new GraphScaler(16);
    scaler.Update(500);
    scaler.Reset();
    Assert.Equal(16, scaler.Scale);
  }

  [Theory]
  [InlineData(0, 59)]
  [InlineData(100, 1)]
  [InlineData(50, 30)]
  [InlineData(250, 1)]
  [InlineData(-10, 59)]
  public void MapYKeepsOnePixelFreeAtTopAndBottom(double value, double expected)
  {
    Assert.Equal(expected, GraphScale.MapY(value, 100, 60), 9);
  }

  [Fact]
  public void MapYWithoutAScaleDrawsOnTheBottomRow()
  {
    Assert.Equal(59, GraphScale.MapY(10, 0, 60));
  }

  [Fact]
  public void ScalerMapsThroughItsCurrentScale()
  {
    var scaler = new GraphScaler(100);
    Assert.Equal(30, scaler.MapY(50, 60), 9);
  }
}
