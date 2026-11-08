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

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Makes a PNG from the game smaller by compressing its pixel data properly.
/// </summary>
/// <remarks>
/// <para>
/// The game writes PNGs whose deflate data uses stored (uncompressed) blocks, so the file is as
/// big as the raw pixels (about 1.4 MB for 800 by 450). That is a lot to hand to a language model.
/// This class joins the <c>IDAT</c> chunks, inflates them, deflates the result again at a strong
/// level, and writes a valid PNG with correct CRCs. Every other chunk is kept as it is.
/// </para>
/// <para>
/// It never throws on bad input: a file that is not a PNG, is damaged, would inflate beyond
/// <see cref="MaxRawBytes"/>, or does not get smaller is simply not changed.
/// </para>
/// </remarks>
public static class PngRecompressor
{
  /// <summary>
  /// The most raw pixel bytes the recompressor will inflate (a 2048 square picture).
  /// </summary>
  public const int MaxRawBytes = 40 * 1024 * 1024;

  private static readonly byte[] _signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
  private static readonly uint[] _crcTable = BuildCrcTable();

  /// <summary>
  /// Recompresses a PNG.
  /// </summary>
  /// <param name="png">The PNG file bytes.</param>
  /// <returns>
  /// The smaller PNG, or <paramref name="png"/> unchanged when it could not be made smaller.
  /// </returns>
  public static ReadOnlyMemory<byte> Shrink(ReadOnlyMemory<byte> png)
  {
    try
    {
      var smaller = Recompress(png.Span);
      return smaller is not null && smaller.Length < png.Length ? smaller : png;
    }
    catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException
        or OverflowException)
    {
      // A damaged picture is better returned as it came than not at all.
      return png;
    }
  }

  private static byte[]? Recompress(ReadOnlySpan<byte> png)
  {
    if (png.Length < _signature.Length || !png[.._signature.Length].SequenceEqual(_signature))
    {
      return null;
    }

    using var compressed = new MemoryStream();
    var position = _signature.Length;
    var firstIdat = -1;
    while (position + 12 <= png.Length)
    {
      var length = BinaryPrimitives.ReadUInt32BigEndian(png[position..]);
      var type = png.Slice(position + 4, 4);
      if (length > int.MaxValue || position + 12L + length > png.Length)
      {
        return null;
      }

      if (type.SequenceEqual("IDAT"u8))
      {
        firstIdat = firstIdat < 0 ? position : firstIdat;
        compressed.Write(png.Slice(position + 8, (int)length));
      }

      position += 12 + (int)length;
      if (type.SequenceEqual("IEND"u8))
      {
        break;
      }
    }

    if (firstIdat < 0)
    {
      return null;
    }

    using var raw = new MemoryStream();
    compressed.Position = 0;
    using (var inflate = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true))
    {
      var buffer = new byte[81920];
      int read;
      while ((read = inflate.Read(buffer)) > 0)
      {
        if (raw.Length + read > MaxRawBytes)
        {
          return null;
        }

        raw.Write(buffer, 0, read);
      }
    }

    using var packed = new MemoryStream();
    using (var deflate = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
    {
      deflate.Write(raw.GetBuffer(), 0, (int)raw.Length);
    }

    return Rebuild(png, firstIdat, packed.GetBuffer().AsSpan(0, (int)packed.Length));
  }

  private static byte[] Rebuild(ReadOnlySpan<byte> png, int firstIdat, ReadOnlySpan<byte> idat)
  {
    using var output = new MemoryStream(png.Length / 4 + idat.Length);
    output.Write(_signature);
    var position = _signature.Length;
    var written = false;
    Span<byte> head = stackalloc byte[8];
    while (position + 12 <= png.Length)
    {
      var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png[position..]);
      var type = png.Slice(position + 4, 4);
      var whole = png.Slice(position, 12 + length);
      if (type.SequenceEqual("IDAT"u8))
      {
        if (!written && position == firstIdat)
        {
          BinaryPrimitives.WriteUInt32BigEndian(head, (uint)idat.Length);
          "IDAT"u8.CopyTo(head[4..]);
          output.Write(head);
          output.Write(idat);
          var crc = Crc(Crc(0xFFFFFFFF, "IDAT"u8), idat) ^ 0xFFFFFFFF;
          BinaryPrimitives.WriteUInt32BigEndian(head, crc);
          output.Write(head[..4]);
          written = true;
        }
      }
      else
      {
        output.Write(whole);
      }

      position += 12 + length;
      if (type.SequenceEqual("IEND"u8))
      {
        break;
      }
    }

    return output.ToArray();
  }

  private static uint Crc(uint crc, ReadOnlySpan<byte> data)
  {
    foreach (var b in data)
    {
      crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
    }

    return crc;
  }

  private static uint[] BuildCrcTable()
  {
    var table = new uint[256];
    for (uint n = 0; n < 256; n++)
    {
      var c = n;
      for (var k = 0; k < 8; k++)
      {
        c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
      }

      table[n] = c;
    }

    return table;
  }
}
