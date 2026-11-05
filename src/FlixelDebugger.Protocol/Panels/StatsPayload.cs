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

namespace FlixelDebugger.Protocol.Panels;

/// <summary>
/// One performance sample from the <c>stats</c> panel (kind 1), sent at the sample rate.
/// </summary>
/// <remarks>
/// The default rate is 20 Hz (range 1 to 30, changed through the controls panel). The app keeps the
/// history. This panel has no actions.
/// </remarks>
/// <param name="Fps">Frames per second.</param>
/// <param name="FrameMs">Whole frame time in milliseconds.</param>
/// <param name="CpuMs">CPU time in milliseconds.</param>
/// <param name="PresentMs">Present time in milliseconds.</param>
/// <param name="Draws">Draw calls.</param>
/// <param name="Triangles">Triangles drawn.</param>
/// <param name="Vertices">Vertices drawn.</param>
/// <param name="Passes">Render passes.</param>
/// <param name="Submits">Queue submits.</param>
/// <param name="Pipelines">Pipelines in use.</param>
/// <param name="HeapMb">Heap size in megabytes.</param>
/// <param name="NativeMb">Native memory in megabytes.</param>
/// <param name="TextureMb">Texture memory in megabytes.</param>
/// <param name="RingKb">Ring buffer usage in kilobytes.</param>
/// <param name="ObjectCount">Number of game objects.</param>
/// <param name="SendKbps">Agent send rate in kilobytes per second.</param>
/// <param name="RecvKbps">Agent receive rate in kilobytes per second.</param>
/// <param name="Paused">Whether the game is paused.</param>
/// <param name="Elapsed">Elapsed game time in seconds.</param>
/// <param name="CamX">Camera X position.</param>
/// <param name="CamY">Camera Y position.</param>
/// <param name="CamZoom">Camera zoom.</param>
public sealed record StatsPayload(
    float Fps,
    float FrameMs,
    float CpuMs,
    float PresentMs,
    int Draws,
    int Triangles,
    int Vertices,
    int Passes,
    int Submits,
    int Pipelines,
    float HeapMb,
    float NativeMb,
    float TextureMb,
    float RingKb,
    int ObjectCount,
    float SendKbps,
    float RecvKbps,
    bool Paused,
    double Elapsed,
    float CamX,
    float CamY,
    float CamZoom) : PanelPayload
{
  /// <summary>
  /// Reads a stats payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static StatsPayload Read(ref MessageReader reader)
  {
    var fps = reader.ReadF32();
    var frameMs = reader.ReadF32();
    var cpuMs = reader.ReadF32();
    var presentMs = reader.ReadF32();
    var draws = reader.ReadI32();
    var triangles = reader.ReadI32();
    var vertices = reader.ReadI32();
    var passes = reader.ReadI32();
    var submits = reader.ReadI32();
    var pipelines = reader.ReadI32();
    var heapMb = reader.ReadF32();
    var nativeMb = reader.ReadF32();
    var textureMb = reader.ReadF32();
    var ringKb = reader.ReadF32();
    var objectCount = reader.ReadI32();
    var sendKbps = reader.ReadF32();
    var recvKbps = reader.ReadF32();
    var paused = reader.ReadBool();
    var elapsed = reader.ReadF64();
    var camX = reader.ReadF32();
    var camY = reader.ReadF32();
    var camZoom = reader.ReadF32();
    return new StatsPayload(
        fps, frameMs, cpuMs, presentMs, draws, triangles, vertices, passes, submits, pipelines,
        heapMb, nativeMb, textureMb, ringKb, objectCount, sendKbps, recvKbps, paused, elapsed,
        camX, camY, camZoom);
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteF32(Fps)
        .WriteF32(FrameMs)
        .WriteF32(CpuMs)
        .WriteF32(PresentMs)
        .WriteI32(Draws)
        .WriteI32(Triangles)
        .WriteI32(Vertices)
        .WriteI32(Passes)
        .WriteI32(Submits)
        .WriteI32(Pipelines)
        .WriteF32(HeapMb)
        .WriteF32(NativeMb)
        .WriteF32(TextureMb)
        .WriteF32(RingKb)
        .WriteI32(ObjectCount)
        .WriteF32(SendKbps)
        .WriteF32(RecvKbps)
        .WriteBool(Paused)
        .WriteF64(Elapsed)
        .WriteF32(CamX)
        .WriteF32(CamY)
        .WriteF32(CamZoom);
  }
}
