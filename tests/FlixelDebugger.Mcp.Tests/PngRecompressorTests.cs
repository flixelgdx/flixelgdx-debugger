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

using System.Buffers.Binary;
using System.IO.Compression;
using FlixelDebugger.Mcp.Support;

namespace FlixelDebugger.Mcp.Tests;

/// <summary>Builds and checks PNG files for the recompression tests.</summary>
internal static class PngSupport
{
  public static byte[] Raw(int width, int height)
  {
    // One filter byte per row, then RGBA pixels with a pattern that compresses well.
    var raw = new byte[height * ((width * 4) + 1)];
    for (var i = 0; i < raw.Length; i++)
    {
      raw[i] = (byte)((i / 64) % 7);
    }

    for (var row = 0; row < height; row++)
    {
      raw[row * ((width * 4) + 1)] = 0;
    }

    return raw;
  }

  public static byte[] Make(int width, int height, bool stored)
  {
    using var idat = new MemoryStream();
    using (var z = new ZLibStream(
        idat, stored ? CompressionLevel.NoCompression : CompressionLevel.Optimal, true))
    {
      z.Write(Raw(width, height));
    }

    var header = new byte[13];
    BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
    BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
    header[8] = 8;
    header[9] = 6;
    using var png = new MemoryStream();
    png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    Chunk(png, "IHDR", header);
    var bytes = idat.ToArray();
    // Two IDAT chunks, because a PNG may split its data and the recompressor must join them.
    Chunk(png, "IDAT", bytes[..(bytes.Length / 2)]);
    Chunk(png, "IDAT", bytes[(bytes.Length / 2)..]);
    Chunk(png, "IEND", []);
    return png.ToArray();
  }

  public static void AssertValid(byte[] png, byte[] expectedRaw)
  {
    Assert.True(png.AsSpan(0, 8).SequenceEqual(
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
    var position = 8;
    using var idat = new MemoryStream();
    var types = new List<string>();
    while (position < png.Length)
    {
      var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));
      var type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
      var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length));
      Assert.Equal(Crc(png.AsSpan(position + 4, 4 + length)), crc);
      types.Add(type);
      if (type == "IDAT")
      {
        idat.Write(png, position + 8, length);
      }

      position += 12 + length;
    }

    Assert.Equal(["IHDR", "IDAT", "IEND"], types);
    idat.Position = 0;
    using var inflate = new ZLibStream(idat, CompressionMode.Decompress);
    using var raw = new MemoryStream();
    inflate.CopyTo(raw);
    Assert.Equal(expectedRaw, raw.ToArray());
  }

  private static void Chunk(Stream output, string type, byte[] data)
  {
    var head = new byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(head, (uint)data.Length);
    output.Write(head);
    var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
    output.Write(body);
    BinaryPrimitives.WriteUInt32BigEndian(head, Crc(body));
    output.Write(head);
  }

  private static uint Crc(ReadOnlySpan<byte> data)
  {
    var crc = 0xFFFFFFFFu;
    foreach (var b in data)
    {
      crc ^= b;
      for (var k = 0; k < 8; k++)
      {
        crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
      }
    }

    return crc ^ 0xFFFFFFFF;
  }
}

public class PngRecompressorTests
{
  [Fact]
  public void StoredBlocksBecomeAValidSmallerPngWithTheSamePixels()
  {
    var stored = PngSupport.Make(64, 64, stored: true);

    var small = PngRecompressor.Shrink(stored).ToArray();

    Assert.True(small.Length < stored.Length / 2);
    PngSupport.AssertValid(small, PngSupport.Raw(64, 64));
  }

  [Fact]
  public void APngThatCannotGetSmallerIsReturnedAsItIs()
  {
    var packed = PngSupport.Make(64, 64, stored: false);

    var result = PngRecompressor.Shrink(packed);

    Assert.True(result.Length <= packed.Length);
    PngSupport.AssertValid(result.ToArray(), PngSupport.Raw(64, 64));
  }

  [Theory]
  [InlineData(new byte[] { })]
  [InlineData(new byte[] { 1, 2, 3, 4 })]
  [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 5, 73 })]
  public void DamagedOrForeignInputIsReturnedUnchanged(byte[] input)
  {
    Assert.Equal(input, PngRecompressor.Shrink(input).ToArray());
  }

  [Fact]
  public void CorruptPixelDataFallsBackToTheOriginalBytes()
  {
    var stored = PngSupport.Make(32, 32, stored: true);
    // Flip bytes inside the first IDAT chunk's deflate stream.
    for (var i = 40; i < 60; i++)
    {
      stored[i] ^= 0xFF;
    }

    Assert.Equal(stored, PngRecompressor.Shrink(stored).ToArray());
  }
}
