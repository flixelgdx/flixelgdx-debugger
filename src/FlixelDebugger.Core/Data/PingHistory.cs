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

namespace FlixelDebugger.Core.Data;

/// <summary>
/// Remembers the last 120 measured ping times of one game, so the network graph can draw them.
/// </summary>
/// <remarks>
/// The stats history has no ping series because the game does not send one: the debugger measures it itself.
/// This ring works like <see cref="StatsHistory"/>: the oldest value drops out when a new one arrives. It is thread
/// safe, because pings are measured on a background thread and drawn on the UI thread.
/// </remarks>
public sealed class PingHistory
{
  /// <summary>The number of values the history keeps.</summary>
  public const int Capacity = StatsHistory.Capacity;

  private readonly Lock _gate = new();
  private readonly double[] _ring = new double[Capacity];
  private int _head;
  private int _count;
  private double _latest;

  /// <summary>Gets how many values are stored, up to <see cref="Capacity"/>.</summary>
  public int Count
  {
    get
    {
      lock (_gate)
      {
        return _count;
      }
    }
  }

  /// <summary>Gets the newest value, or 0 before the first one.</summary>
  public double Latest
  {
    get
    {
      lock (_gate)
      {
        return _latest;
      }
    }
  }

  /// <summary>
  /// Adds a measured round trip time.
  /// </summary>
  /// <param name="milliseconds">The ping in milliseconds.</param>
  public void Add(double milliseconds)
  {
    lock (_gate)
    {
      _ring[_head] = milliseconds;
      _latest = milliseconds;
      _head = (_head + 1) % Capacity;
      if (_count < Capacity)
      {
        _count++;
      }
    }
  }

  /// <summary>
  /// Copies the values into a caller-owned buffer, oldest first.
  /// </summary>
  /// <param name="destination">The buffer to fill. At most <see cref="Capacity"/> values are written.</param>
  /// <returns>The number of values written.</returns>
  public int CopyTo(Span<double> destination)
  {
    lock (_gate)
    {
      var count = Math.Min(_count, destination.Length);
      var skip = _count - count;
      for (var i = 0; i < count; i++)
      {
        destination[i] = _ring[(_head - _count + skip + i + Capacity) % Capacity];
      }

      return count;
    }
  }

  /// <summary>
  /// Finds the largest stored value.
  /// </summary>
  /// <returns>The largest value, or 0 when the history is empty.</returns>
  public double Max()
  {
    lock (_gate)
    {
      var max = 0.0;
      for (var i = 0; i < _count; i++)
      {
        max = Math.Max(max, _ring[(_head - _count + i + Capacity) % Capacity]);
      }

      return max;
    }
  }

  /// <summary>
  /// Forgets every value.
  /// </summary>
  public void Clear()
  {
    lock (_gate)
    {
      Array.Clear(_ring);
      _head = 0;
      _count = 0;
      _latest = 0;
    }
  }
}
