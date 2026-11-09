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

using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Core.Data;

/// <summary>
/// Keeps the last 120 stats samples of one game, one ring per field.
/// </summary>
/// <remarks>
/// <para>
/// The game sends one stats sample at a time and expects the app to keep the history. Each field
/// has its own ring of <see cref="Capacity"/> values, so a graph can read exactly the series it
/// draws. Think of a flight recorder: new readings overwrite the oldest ones, and nothing is
/// allocated while it records.
/// </para>
/// <para>
/// All values are stored as <see cref="double"/>, which holds every <see cref="float"/> and
/// <see cref="int"/> of the protocol without loss. The class is thread safe: the receive loop adds
/// samples while the UI reads.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var buffer = new double[StatsHistory.Capacity];
/// // buffer[0..count] holds the series, oldest first.
/// var count = history.CopyTo(StatsField.Fps, buffer);
/// </code>
/// </example>
public sealed class StatsHistory
{
  /// <summary>The number of samples each ring keeps.</summary>
  public const int Capacity = 120;

  private static readonly int _fieldCount = Enum.GetValues<StatsField>().Length;

  private readonly Lock _gate = new();
  private readonly double[] _rings = new double[_fieldCount * Capacity];
  private readonly double[] _latest = new double[_fieldCount];
  private StatsPayload? _latestPayload;
  private long _total;
  private int _head;
  private int _count;

  /// <summary>
  /// Gets the number of samples currently stored, from 0 to <see cref="Capacity"/>.
  /// </summary>
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

  /// <summary>
  /// Gets how many samples were ever added. It changes whenever new data arrives, so it works as a
  /// version number.
  /// </summary>
  public long TotalSamples
  {
    get
    {
      lock (_gate)
      {
        return _total;
      }
    }
  }

  /// <summary>
  /// Gets the most recent sample, or <see langword="null"/> before the first one arrives.
  /// </summary>
  public StatsPayload? LatestPayload
  {
    get
    {
      lock (_gate)
      {
        return _latestPayload;
      }
    }
  }

  /// <summary>
  /// Adds a sample, overwriting the oldest one once the rings are full.
  /// </summary>
  /// <param name="sample">The sample to add.</param>
  public void Add(StatsPayload sample)
  {
    ArgumentNullException.ThrowIfNull(sample);
    lock (_gate)
    {
      Store(StatsField.Fps, sample.Fps);
      Store(StatsField.FrameMs, sample.FrameMs);
      Store(StatsField.CpuMs, sample.CpuMs);
      Store(StatsField.PresentMs, sample.PresentMs);
      Store(StatsField.Draws, sample.Draws);
      Store(StatsField.Triangles, sample.Triangles);
      Store(StatsField.Vertices, sample.Vertices);
      Store(StatsField.Passes, sample.Passes);
      Store(StatsField.Submits, sample.Submits);
      Store(StatsField.Pipelines, sample.Pipelines);
      Store(StatsField.HeapMb, sample.HeapMb);
      Store(StatsField.NativeMb, sample.NativeMb);
      Store(StatsField.TextureMb, sample.TextureMb);
      Store(StatsField.RingKb, sample.RingKb);
      Store(StatsField.ObjectCount, sample.ObjectCount);
      Store(StatsField.SendKbps, sample.SendKbps);
      Store(StatsField.RecvKbps, sample.RecvKbps);
      Store(StatsField.Paused, sample.Paused ? 1 : 0);
      Store(StatsField.Elapsed, sample.Elapsed);
      Store(StatsField.CamX, sample.CamX);
      Store(StatsField.CamY, sample.CamY);
      Store(StatsField.CamZoom, sample.CamZoom);

      _head = (_head + 1) % Capacity;
      if (_count < Capacity)
      {
        _count++;
      }

      _total++;
      _latestPayload = sample;
    }
  }

  /// <summary>
  /// Gets the newest value of a field.
  /// </summary>
  /// <param name="field">The field to read.</param>
  /// <returns>The newest value, or 0 before the first sample.</returns>
  public double Latest(StatsField field)
  {
    lock (_gate)
    {
      return _latest[(int)field];
    }
  }

  /// <summary>
  /// Gets the value of a field at a position in the series.
  /// </summary>
  /// <param name="field">The field to read.</param>
  /// <param name="index">The position, where 0 is the oldest stored sample.</param>
  /// <returns>The value at that position.</returns>
  /// <exception cref="ArgumentOutOfRangeException">
  /// <paramref name="index"/> is not below <see cref="Count"/>.
  /// </exception>
  public double At(StatsField field, int index)
  {
    lock (_gate)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(index);
      ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
      return _rings[(int)field * Capacity + RingPosition(index)];
    }
  }

  /// <summary>
  /// Copies a series into a caller-owned buffer, oldest sample first.
  /// </summary>
  /// <remarks>
  /// Reuse the same buffer on every redraw so no memory is allocated per frame.
  /// </remarks>
  /// <param name="field">The field to copy.</param>
  /// <param name="destination">
  /// The buffer to fill. At most <see cref="Capacity"/> values are written.
  /// </param>
  /// <returns>The number of values written.</returns>
  public int CopyTo(StatsField field, Span<double> destination)
  {
    lock (_gate)
    {
      var count = Math.Min(_count, destination.Length);
      var skip = _count - count;
      var ring = _rings.AsSpan((int)field * Capacity, Capacity);
      for (var i = 0; i < count; i++)
      {
        destination[i] = ring[RingPosition(skip + i)];
      }

      return count;
    }
  }

  /// <summary>
  /// Returns a series as a new array, oldest sample first. This allocates; prefer
  /// <see cref="CopyTo"/> in a draw loop.
  /// </summary>
  /// <param name="field">The field to read.</param>
  /// <returns>A new array holding <see cref="Count"/> values.</returns>
  public double[] GetSeries(StatsField field)
  {
    lock (_gate)
    {
      var series = new double[_count];
      CopyTo(field, series);
      return series;
    }
  }

  /// <summary>
  /// Finds the largest stored value of a field.
  /// </summary>
  /// <param name="field">The field to scan.</param>
  /// <returns>The largest value, or 0 when there are no samples.</returns>
  public double Max(StatsField field)
  {
    lock (_gate)
    {
      var max = 0.0;
      var ring = _rings.AsSpan((int)field * Capacity, Capacity);
      for (var i = 0; i < _count; i++)
      {
        max = i == 0 ? ring[RingPosition(i)] : Math.Max(max, ring[RingPosition(i)]);
      }

      return max;
    }
  }

  /// <summary>
  /// Forgets every sample.
  /// </summary>
  public void Clear()
  {
    lock (_gate)
    {
      Array.Clear(_rings);
      Array.Clear(_latest);
      _head = 0;
      _count = 0;
      _total++;
      _latestPayload = null;
    }
  }

  private void Store(StatsField field, double value)
  {
    _rings[(int)field * Capacity + _head] = value;
    _latest[(int)field] = value;
  }

  // Maps "0 is the oldest" to the slot in the ring. The caller holds the lock.
  private int RingPosition(int index) => (_head - _count + index + Capacity) % Capacity;
}
