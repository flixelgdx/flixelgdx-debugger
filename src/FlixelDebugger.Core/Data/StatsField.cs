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
/// Names every numeric field of a stats sample, so a history can be asked for one series at a time.
/// </summary>
public enum StatsField
{
  /// <summary>Frames per second.</summary>
  Fps,

  /// <summary>Whole frame time in milliseconds.</summary>
  FrameMs,

  /// <summary>CPU time per frame in milliseconds.</summary>
  CpuMs,

  /// <summary>Present time per frame in milliseconds.</summary>
  PresentMs,

  /// <summary>Draw calls.</summary>
  Draws,

  /// <summary>Triangles.</summary>
  Triangles,

  /// <summary>Vertices.</summary>
  Vertices,

  /// <summary>Render passes.</summary>
  Passes,

  /// <summary>Queue submits.</summary>
  Submits,

  /// <summary>Pipelines.</summary>
  Pipelines,

  /// <summary>Heap memory in megabytes.</summary>
  HeapMb,

  /// <summary>Native memory in megabytes.</summary>
  NativeMb,

  /// <summary>Texture memory in megabytes.</summary>
  TextureMb,

  /// <summary>Upload ring size in kilobytes.</summary>
  RingKb,

  /// <summary>Number of objects in the game.</summary>
  ObjectCount,

  /// <summary>Agent send rate in KB/s.</summary>
  SendKbps,

  /// <summary>Agent receive rate in KB/s.</summary>
  RecvKbps,

  /// <summary>Whether the game is paused: 1 for paused, 0 for running.</summary>
  Paused,

  /// <summary>Elapsed game time in seconds.</summary>
  Elapsed,

  /// <summary>Camera X position.</summary>
  CamX,

  /// <summary>Camera Y position.</summary>
  CamY,

  /// <summary>Camera zoom.</summary>
  CamZoom,
}
