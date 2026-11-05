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

using System.Buffers;
using System.Buffers.Binary;

namespace FlixelDebugger.Protocol;

/// <summary>
/// Reads one element of an array from a <see cref="MessageReader"/>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="reader">The reader, positioned at the start of the element.</param>
/// <returns>The element that was read.</returns>
public delegate T ReadElement<out T>(ref MessageReader reader);

/// <summary>
/// Reads protocol messages from a received byte span.
/// </summary>
/// <remarks>
/// <para>
/// Think of this type as a finger running down a printed form. You place it at the top of the
/// message, and every <c>Read...</c> call returns the next field and moves the finger past it.
/// </para>
/// <para>
/// Every read is bounds checked. A message that ends early throws a <see cref="ProtocolException"/>,
/// which the app should catch and treat as "drop this message". Bytes left over after the last field
/// you read are simply ignored, because newer protocol minor versions may append fields. Malformed
/// UTF-8 in a string becomes U+FFFD instead of throwing.
/// </para>
/// <para>
/// This is a <c>ref struct</c> over the span, so it never copies the message and can only live on the
/// stack. Pass it to other methods with <c>ref</c> so they move the same finger.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var reader = new MessageReader(received); // received is a ReadOnlySpan&lt;byte&gt;.
/// try
/// {
///     if ((MessageType)reader.ReadU8() == MessageType.Ping)
///     {
///         long nonce = reader.ReadI64();
///         Console.WriteLine($"Ping {nonce}, {reader.Remaining} trailing bytes ignored.");
///     }
/// }
/// catch (ProtocolException)
/// {
///     // Corrupt or truncated message: ignore it.
/// }
/// </code>
/// </example>
/// <seealso cref="MessageWriter"/>
public ref struct MessageReader
{
    private const char Replacement = '�';

    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    /// <summary>
    /// Creates a reader positioned at the start of a message.
    /// </summary>
    /// <param name="data">The received message.</param>
    public MessageReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    /// <summary>
    /// Gets how many bytes are left unread.
    /// </summary>
    public readonly int Remaining => _data.Length - _position;

    /// <summary>
    /// Gets how many bytes have been read so far.
    /// </summary>
    public readonly int Position => _position;

    /// <summary>
    /// Reads an unsigned 8-bit integer.
    /// </summary>
    /// <returns>A value from 0 to 255.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public byte ReadU8()
    {
        Need(1);
        return _data[_position++];
    }

    /// <summary>
    /// Reads an unsigned 16-bit integer.
    /// </summary>
    /// <returns>A value from 0 to 65535.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public ushort ReadU16()
    {
        Need(2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data[_position..]);
        _position += 2;
        return value;
    }

    /// <summary>
    /// Reads a signed 32-bit integer.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public int ReadI32()
    {
        Need(4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(_data[_position..]);
        _position += 4;
        return value;
    }

    /// <summary>
    /// Reads a signed 64-bit integer.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public long ReadI64()
    {
        Need(8);
        var value = BinaryPrimitives.ReadInt64LittleEndian(_data[_position..]);
        _position += 8;
        return value;
    }

    /// <summary>
    /// Reads a 32-bit float.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public float ReadF32()
    {
        Need(4);
        var value = BinaryPrimitives.ReadSingleLittleEndian(_data[_position..]);
        _position += 4;
        return value;
    }

    /// <summary>
    /// Reads a 64-bit float.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public double ReadF64()
    {
        Need(8);
        var value = BinaryPrimitives.ReadDoubleLittleEndian(_data[_position..]);
        _position += 8;
        return value;
    }

    /// <summary>
    /// Reads a boolean stored as one byte.
    /// </summary>
    /// <returns><see langword="true"/> for any non-zero byte.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public bool ReadBool() => ReadU8() != 0;

    /// <summary>
    /// Reads the <c>u16</c> element count at the start of an array.
    /// </summary>
    /// <returns>The number of elements that follow.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public int ReadCount() => ReadU16();

    /// <summary>
    /// Reads a whole counted array, one element at a time.
    /// </summary>
    /// <remarks>
    /// Every element takes at least one byte, so a count larger than the remaining bytes can never be
    /// satisfied. It is rejected up front, which stops a corrupt count from allocating a large array.
    /// </remarks>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="readItem">Reads one element, for example <c>PanelInfo.Read</c>.</param>
    /// <returns>The elements, in wire order.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public T[] ReadArray<T>(ReadElement<T> readItem)
    {
        ArgumentNullException.ThrowIfNull(readItem);

        var count = ReadCount();
        if (count > Remaining)
        {
            throw new ProtocolException($"Message too short: array of {count} elements but only {Remaining} bytes remain");
        }

        var items = new T[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = readItem(ref this);
        }

        return items;
    }

    /// <summary>
    /// Reads a string: a <c>u16</c> byte length followed by UTF-8 bytes.
    /// </summary>
    /// <remarks>
    /// Malformed UTF-8 sequences become U+FFFD, exactly like the Java reader. A truncated or invalid
    /// multi-byte sequence yields one U+FFFD. Bytes above 0xF4 also drop the rest of the string, which
    /// is how the Java reader behaves.
    /// </remarks>
    /// <returns>The decoded text.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public string ReadString()
    {
        int length = ReadU16();
        Need(length);
        var data = _data.Slice(_position, length);
        _position += length;

        // Every byte produces at most one char (a four-byte sequence produces two chars), so a
        // buffer as long as the byte count always fits the result.
        var rented = length <= 256 ? null : ArrayPool<char>.Shared.Rent(length);
        var buffer = rented ?? stackalloc char[256];
        try
        {
            var count = Decode(data, buffer);
            return new string(buffer[..count]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Reads a byte block: an <c>i32</c> length followed by the raw bytes.
    /// </summary>
    /// <returns>A view of the bytes inside the message. Copy them if you need to keep them.</returns>
    /// <exception cref="ProtocolException">The length is negative or the message is too short.</exception>
    public ReadOnlySpan<byte> ReadBytes()
    {
        var length = ReadI32();
        if (length < 0)
        {
            throw new ProtocolException($"Negative byte block length: {length}");
        }

        Need(length);
        var data = _data.Slice(_position, length);
        _position += length;
        return data;
    }

    /// <summary>
    /// Reads every byte that is left.
    /// </summary>
    /// <returns>A view of the remaining bytes. The reader is then at the end of the message.</returns>
    public ReadOnlySpan<byte> ReadRemaining()
    {
        var data = _data[_position..];
        _position = _data.Length;
        return data;
    }

    /// <summary>
    /// Skips bytes without reading them.
    /// </summary>
    /// <param name="count">How many bytes to skip.</param>
    /// <exception cref="ProtocolException">Fewer than <paramref name="count"/> bytes remain.</exception>
    public void Skip(int count)
    {
        Need(count);
        _position += count;
    }

    /// <summary>
    /// Skips one property value (its kind byte and its data).
    /// </summary>
    /// <exception cref="ProtocolException">The message is too short or the value kind is unknown.</exception>
    public void SkipValue()
    {
        var kind = (ValueKind)ReadU8();
        switch (kind)
        {
            case ValueKind.Bool:
                Skip(1);
                break;
            case ValueKind.Int:
            case ValueKind.Float:
                Skip(4);
                break;
            case ValueKind.Long:
            case ValueKind.Double:
                Skip(8);
                break;
            case ValueKind.String:
            case ValueKind.Error:
                Skip(ReadU16());
                break;
            default:
                throw new ProtocolException($"Unknown value kind: {(byte)kind}");
        }
    }

    private static int Decode(ReadOnlySpan<byte> data, Span<char> output)
    {
        var i = 0;
        var n = 0;
        while (i < data.Length)
        {
            int b = data[i++];
            if (b < 0x80)
            {
                output[n++] = (char)b;
                continue;
            }

            var extra = b >= 0xF0 ? 3 : b >= 0xE0 ? 2 : b >= 0xC0 ? 1 : -1;
            if (extra < 0 || b > 0xF4 || i + extra > data.Length)
            {
                output[n++] = Replacement;
                if (extra > 0)
                {
                    i = data.Length;
                }

                continue;
            }

            var codePoint = b & (0x3F >> extra);
            var valid = true;
            for (var k = 0; k < extra; k++)
            {
                int next = data[i++];
                if ((next & 0xC0) != 0x80)
                {
                    valid = false;
                }

                codePoint = (codePoint << 6) | (next & 0x3F);
            }

            if (!valid || codePoint > 0x10FFFF)
            {
                output[n++] = Replacement;
            }
            else if (codePoint >= 0x10000)
            {
                codePoint -= 0x10000;
                output[n++] = (char)(0xD800 + (codePoint >> 10));
                output[n++] = (char)(0xDC00 + (codePoint & 0x3FF));
            }
            else
            {
                output[n++] = (char)codePoint;
            }
        }

        return n;
    }

    private readonly void Need(int count)
    {
        if (count < 0 || Remaining < count)
        {
            throw new ProtocolException($"Message too short: needed {count} bytes but only {Remaining} remain");
        }
    }
}
