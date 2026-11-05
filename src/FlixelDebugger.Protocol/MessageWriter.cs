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

namespace FlixelDebugger.Protocol;

/// <summary>
/// Writes protocol messages into one reusable, growable, little-endian buffer.
/// </summary>
/// <remarks>
/// <para>
/// Think of this class as a single sheet of paper on a clipboard. You start a message (a fresh page),
/// fill in the fields in order, then hand the page to the transport. For the next message you wipe
/// the page and reuse it. The page is only replaced by a bigger one when a message does not fit, and
/// the new page is twice as big, so after a short warm-up writing allocates nothing.
/// </para>
/// <para>
/// Arrays are written with <see cref="BeginArray"/> and <see cref="EndArray"/>: the writer reserves
/// the two count bytes, you write the elements, then <c>EndArray(count)</c> goes back and fills the
/// count in. Arrays may be nested up to <see cref="MaxDepth"/> levels. For a simple list, see
/// <see cref="WriteArray{T}"/>.
/// </para>
/// <para>
/// Strings are encoded by hand as UTF-8 so the output is byte-identical to the Java writer. Text that
/// needs more than 65535 bytes is cut at the last complete character that fits, and unpaired
/// surrogates are written as <c>?</c>.
/// </para>
/// <para>This class is not thread safe. Use one writer per sending thread.</para>
/// </remarks>
/// <example>
/// <code>
/// var writer = new MessageWriter(); // Keep one for the whole session.
///
/// writer.BeginMessage(MessageType.PanelData);
/// writer.WriteU16(panelIndex);
/// writer.BeginArray();
/// foreach (var (name, value) in rows)
/// {
///     writer.WriteString(name);
///     writer.WriteValue(value);
/// }
/// writer.EndArray(rows.Count);
/// ReadOnlyMemory&lt;byte&gt; message = writer.Finish(); // Valid until the next BeginMessage(...).
/// await socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken);
/// </code>
/// </example>
/// <seealso cref="MessageReader"/>
public sealed class MessageWriter
{
  /// <summary>The deepest allowed nesting of <see cref="BeginArray"/> calls.</summary>
  public const int MaxDepth = 16;

  private const int DefaultCapacity = 4096;

  private readonly int[] _arrayStarts = new int[MaxDepth];
  private byte[] _buffer;
  private int _position;
  private int _depth;

  /// <summary>
  /// Creates a writer with a 4 KB starting buffer.
  /// </summary>
  public MessageWriter()
      : this(DefaultCapacity)
  {
  }

  /// <summary>
  /// Creates a writer with the given starting buffer size.
  /// </summary>
  /// <param name="capacity">Starting size in bytes. Values below 16 are raised to 16.</param>
  public MessageWriter(int capacity)
  {
    _buffer = new byte[Math.Max(16, capacity)];
  }

  /// <summary>
  /// Gets how many bytes the current message holds so far.
  /// </summary>
  public int Size => _position;

  /// <summary>
  /// Gets the current buffer size in bytes, which only ever grows.
  /// </summary>
  public int Capacity => _buffer.Length;

  /// <summary>
  /// Discards anything written so far and starts a new message.
  /// </summary>
  /// <param name="type">The message type, written as the first byte.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter BeginMessage(MessageType type)
  {
    _position = 0;
    _depth = 0;
    return WriteU8((byte)type);
  }

  /// <summary>
  /// Ends the message and returns the bytes ready to send.
  /// </summary>
  /// <remarks>
  /// The returned memory is a view of the writer's own buffer. It stays valid until the next call to
  /// <see cref="BeginMessage"/>, so send it (or copy it) before starting another message.
  /// </remarks>
  /// <returns>The finished message.</returns>
  /// <exception cref="ProtocolException">An array was started and never ended.</exception>
  public ReadOnlyMemory<byte> Finish()
  {
    if (_depth != 0)
    {
      throw new ProtocolException("Unfinished array: EndArray() was not called");
    }

    return new ReadOnlyMemory<byte>(_buffer, 0, _position);
  }

  /// <summary>
  /// Starts a counted array by reserving its <c>u16</c> count.
  /// </summary>
  /// <returns>This writer, for chaining.</returns>
  /// <exception cref="ProtocolException">Arrays are nested deeper than <see cref="MaxDepth"/>.</exception>
  public MessageWriter BeginArray()
  {
    if (_depth >= MaxDepth)
    {
      throw new ProtocolException($"Arrays nested deeper than {MaxDepth}");
    }

    Ensure(2);
    _arrayStarts[_depth++] = _position;
    _position += 2;
    return this;
  }

  /// <summary>
  /// Ends the innermost array and fills in its element count.
  /// </summary>
  /// <param name="count">How many elements were written since the matching <see cref="BeginArray"/>.</param>
  /// <returns>This writer, for chaining.</returns>
  /// <exception cref="ProtocolException">There is no open array, or <paramref name="count"/> is not 0 to 65535.</exception>
  public MessageWriter EndArray(int count)
  {
    if (_depth == 0)
    {
      throw new ProtocolException("EndArray() without BeginArray()");
    }

    if (count is < 0 or > ProtocolConstants.MaxArrayCount)
    {
      throw new ProtocolException($"Array count out of range: {count}");
    }

    BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_arrayStarts[--_depth]), (ushort)count);
    return this;
  }

  /// <summary>
  /// Writes a whole list as a counted array, one element at a time.
  /// </summary>
  /// <typeparam name="T">The element type.</typeparam>
  /// <param name="items">The elements to write.</param>
  /// <param name="writeItem">Writes one element.</param>
  /// <returns>This writer, for chaining.</returns>
  /// <exception cref="ProtocolException">There are more than 65535 elements.</exception>
  public MessageWriter WriteArray<T>(IReadOnlyList<T> items, Action<MessageWriter, T> writeItem)
  {
    ArgumentNullException.ThrowIfNull(items);
    ArgumentNullException.ThrowIfNull(writeItem);

    var count = items.Count;
    if (count > ProtocolConstants.MaxArrayCount)
    {
      throw new ProtocolException($"Array count out of range: {count}");
    }

    BeginArray();
    for (var i = 0; i < count; i++)
    {
      writeItem(this, items[i]);
    }

    return EndArray(count);
  }

  /// <summary>
  /// Writes an unsigned 8-bit integer.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteU8(byte value)
  {
    Ensure(1);
    _buffer[_position++] = value;
    return this;
  }

  /// <summary>
  /// Writes an unsigned 16-bit integer.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteU16(ushort value)
  {
    Ensure(2);
    BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position), value);
    _position += 2;
    return this;
  }

  /// <summary>
  /// Writes a signed 32-bit integer.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteI32(int value)
  {
    Ensure(4);
    BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), value);
    _position += 4;
    return this;
  }

  /// <summary>
  /// Writes a signed 64-bit integer.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteI64(long value)
  {
    Ensure(8);
    BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position), value);
    _position += 8;
    return this;
  }

  /// <summary>
  /// Writes a 32-bit float.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteF32(float value)
  {
    Ensure(4);
    BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_position), value);
    _position += 4;
    return this;
  }

  /// <summary>
  /// Writes a 64-bit float.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteF64(double value)
  {
    Ensure(8);
    BinaryPrimitives.WriteDoubleLittleEndian(_buffer.AsSpan(_position), value);
    _position += 8;
    return this;
  }

  /// <summary>
  /// Writes a boolean as one byte, 0 or 1.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteBool(bool value) => WriteU8(value ? (byte)1 : (byte)0);

  /// <summary>
  /// Writes a string as a <c>u16</c> byte length followed by UTF-8 bytes.
  /// </summary>
  /// <param name="text">The text to write. <see langword="null"/> is written as an empty string.</param>
  /// <returns>This writer, for chaining.</returns>
  /// <inheritdoc cref="WriteString(ReadOnlySpan{char})" path="/remarks"/>
  public MessageWriter WriteString(string? text) => WriteString(text.AsSpan());

  /// <summary>
  /// Writes a string as a <c>u16</c> byte length followed by UTF-8 bytes.
  /// </summary>
  /// <remarks>
  /// Text that needs more than 65535 bytes is cut at the last complete character that fits, so the
  /// output is always valid UTF-8. A surrogate pair is never split. Unpaired surrogates are written
  /// as <c>?</c>.
  /// </remarks>
  /// <param name="text">The text to write.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteString(ReadOnlySpan<char> text)
  {
    // First pass: find how many characters fit and how many bytes they need.
    var bytes = 0;
    var end = 0;
    while (end < text.Length)
    {
      var c = text[end];
      int width;
      var chars = 1;
      if (c < 0x80)
      {
        width = 1;
      }
      else if (c < 0x800)
      {
        width = 2;
      }
      else if (char.IsHighSurrogate(c) && end + 1 < text.Length && char.IsLowSurrogate(text[end + 1]))
      {
        width = 4;
        chars = 2;
      }
      else if (char.IsSurrogate(c))
      {
        width = 1;
      }
      else
      {
        width = 3;
      }

      if (bytes + width > ProtocolConstants.MaxStringBytes)
      {
        break;
      }

      bytes += width;
      end += chars;
    }

    Ensure(2 + bytes);
    BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position), (ushort)bytes);
    _position += 2;

    // Second pass: encode the characters that fit.
    for (var i = 0; i < end; i++)
    {
      var c = text[i];
      if (c < 0x80)
      {
        _buffer[_position++] = (byte)c;
      }
      else if (c < 0x800)
      {
        _buffer[_position++] = (byte)(0xC0 | (c >> 6));
        _buffer[_position++] = (byte)(0x80 | (c & 0x3F));
      }
      else if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(text[i + 1]))
      {
        var cp = char.ConvertToUtf32(c, text[++i]);
        _buffer[_position++] = (byte)(0xF0 | (cp >> 18));
        _buffer[_position++] = (byte)(0x80 | ((cp >> 12) & 0x3F));
        _buffer[_position++] = (byte)(0x80 | ((cp >> 6) & 0x3F));
        _buffer[_position++] = (byte)(0x80 | (cp & 0x3F));
      }
      else if (char.IsSurrogate(c))
      {
        _buffer[_position++] = (byte)'?';
      }
      else
      {
        _buffer[_position++] = (byte)(0xE0 | (c >> 12));
        _buffer[_position++] = (byte)(0x80 | ((c >> 6) & 0x3F));
        _buffer[_position++] = (byte)(0x80 | (c & 0x3F));
      }
    }

    return this;
  }

  /// <summary>
  /// Writes a byte block as an <c>i32</c> length followed by the raw bytes.
  /// </summary>
  /// <param name="data">The bytes to write.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteBytes(ReadOnlySpan<byte> data)
  {
    Ensure(4 + data.Length);
    BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), data.Length);
    _position += 4;
    data.CopyTo(_buffer.AsSpan(_position));
    _position += data.Length;
    return this;
  }

  /// <summary>
  /// Copies bytes into the message as they are, with no length prefix.
  /// </summary>
  /// <param name="data">The bytes to copy.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteRaw(ReadOnlySpan<byte> data)
  {
    Ensure(data.Length);
    data.CopyTo(_buffer.AsSpan(_position));
    _position += data.Length;
    return this;
  }

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.Bool"/>.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteValue(bool value) => WriteU8((byte)ValueKind.Bool).WriteBool(value);

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.Int"/>.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteValue(int value) => WriteU8((byte)ValueKind.Int).WriteI32(value);

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.Long"/>.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteValue(long value) => WriteU8((byte)ValueKind.Long).WriteI64(value);

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.Float"/>.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteValue(float value) => WriteU8((byte)ValueKind.Float).WriteF32(value);

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.Double"/>.
  /// </summary>
  /// <param name="value">The value.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteValue(double value) => WriteU8((byte)ValueKind.Double).WriteF64(value);

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.String"/>.
  /// </summary>
  /// <param name="value">The text. <see langword="null"/> is written as an empty string.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteValue(string? value) => WriteU8((byte)ValueKind.String).WriteString(value);

  /// <summary>
  /// Writes a property value of kind <see cref="ValueKind.Error"/>.
  /// </summary>
  /// <param name="message">A short description of the failure.</param>
  /// <returns>This writer, for chaining.</returns>
  public MessageWriter WriteErrorValue(string? message) => WriteU8((byte)ValueKind.Error).WriteString(message);

  /// <summary>
  /// Writes a <see cref="PropertyValue"/> of any kind.
  /// </summary>
  /// <param name="value">The value to write.</param>
  /// <returns>This writer, for chaining.</returns>
  /// <exception cref="ProtocolException">The value has no valid kind (it is a default struct).</exception>
  public MessageWriter WriteValue(PropertyValue value)
  {
    value.Write(this);
    return this;
  }

  private void Ensure(int extra)
  {
    if (_buffer.Length - _position >= extra)
    {
      return;
    }

    var needed = (long)_position + extra;
    long capacity = _buffer.Length;
    while (capacity < needed)
    {
      capacity <<= 1;
    }

    if (capacity > Array.MaxLength)
    {
      throw new ProtocolException("Message too large");
    }

    Array.Resize(ref _buffer, (int)capacity);
  }
}
