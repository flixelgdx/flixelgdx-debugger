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

using System.Globalization;

namespace FlixelDebugger.Protocol;

/// <summary>
/// A property value: a <see cref="ValueKind"/> plus the data that goes with it.
/// </summary>
/// <remarks>
/// <para>
/// Inspector rows, watches, trackers and custom panels all share this one encoding. Think of it as a
/// labeled box: the label says what is inside (a bool, an int, text...), and the box holds exactly
/// that. Create one with a <c>From...</c> method and read it back with the matching <c>As...</c>
/// method, which throws <see cref="InvalidOperationException"/> if the label does not match.
/// </para>
/// <para>
/// Equality compares the kind and the exact bits, so a NaN float equals the same NaN float. The
/// <see cref="ValueKind.Error"/> kind is only ever sent by the game, never by the app.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var speed = PropertyValue.FromFloat(2.5f);
/// if (speed.Kind == ValueKind.Float)
/// {
///     Console.WriteLine(speed.AsFloat()); // 2.5
/// }
/// </code>
/// </example>
public readonly record struct PropertyValue
{
  private readonly long _bits;
  private readonly string? _text;

  private PropertyValue(ValueKind kind, long bits, string? text)
  {
    Kind = kind;
    _bits = bits;
    _text = text;
  }

  /// <summary>
  /// Gets the kind of data this value holds. A default-constructed value has kind 0, which is not valid.
  /// </summary>
  public ValueKind Kind { get; }

  /// <summary>
  /// Creates a <see cref="ValueKind.Bool"/> value.
  /// </summary>
  /// <param name="value">The boolean.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromBool(bool value) => new(ValueKind.Bool, value ? 1 : 0, null);

  /// <summary>
  /// Creates a <see cref="ValueKind.Int"/> value.
  /// </summary>
  /// <param name="value">The 32-bit integer.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromInt(int value) => new(ValueKind.Int, value, null);

  /// <summary>
  /// Creates a <see cref="ValueKind.Long"/> value.
  /// </summary>
  /// <param name="value">The 64-bit integer.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromLong(long value) => new(ValueKind.Long, value, null);

  /// <summary>
  /// Creates a <see cref="ValueKind.Float"/> value.
  /// </summary>
  /// <param name="value">The 32-bit float.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromFloat(float value) => new(ValueKind.Float, BitConverter.SingleToInt32Bits(value), null);

  /// <summary>
  /// Creates a <see cref="ValueKind.Double"/> value.
  /// </summary>
  /// <param name="value">The 64-bit float.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromDouble(double value) => new(ValueKind.Double, BitConverter.DoubleToInt64Bits(value), null);

  /// <summary>
  /// Creates a <see cref="ValueKind.String"/> value.
  /// </summary>
  /// <param name="value">The text. <see langword="null"/> becomes an empty string.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromString(string? value) => new(ValueKind.String, 0, value ?? string.Empty);

  /// <summary>
  /// Creates a <see cref="ValueKind.Error"/> value.
  /// </summary>
  /// <param name="message">The error message. <see langword="null"/> becomes an empty string.</param>
  /// <returns>The new value.</returns>
  public static PropertyValue FromError(string? message) => new(ValueKind.Error, 0, message ?? string.Empty);

  /// <summary>
  /// Reads a value: a kind byte followed by its data.
  /// </summary>
  /// <param name="reader">The reader, positioned at the kind byte.</param>
  /// <returns>The value that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short or the kind is unknown.</exception>
  public static PropertyValue Read(ref MessageReader reader)
  {
    var kind = (ValueKind)reader.ReadU8();
    switch (kind)
    {
      case ValueKind.Bool:
        return FromBool(reader.ReadBool());
      case ValueKind.Int:
        return FromInt(reader.ReadI32());
      case ValueKind.Long:
        return FromLong(reader.ReadI64());
      case ValueKind.Float:
        return FromFloat(reader.ReadF32());
      case ValueKind.Double:
        return FromDouble(reader.ReadF64());
      case ValueKind.String:
        return FromString(reader.ReadString());
      case ValueKind.Error:
        return FromError(reader.ReadString());
      default:
        throw new ProtocolException($"Unknown value kind: {(byte)kind}");
    }
  }

  /// <summary>
  /// Writes this value: a kind byte followed by its data.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  /// <exception cref="ProtocolException">This is a default-constructed value with no valid kind.</exception>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    switch (Kind)
    {
      case ValueKind.Bool:
        writer.WriteValue(_bits != 0);
        break;
      case ValueKind.Int:
        writer.WriteValue((int)_bits);
        break;
      case ValueKind.Long:
        writer.WriteValue(_bits);
        break;
      case ValueKind.Float:
        writer.WriteValue(BitConverter.Int32BitsToSingle((int)_bits));
        break;
      case ValueKind.Double:
        writer.WriteValue(BitConverter.Int64BitsToDouble(_bits));
        break;
      case ValueKind.String:
        writer.WriteValue(_text);
        break;
      case ValueKind.Error:
        writer.WriteErrorValue(_text);
        break;
      default:
        throw new ProtocolException($"Cannot write a value with kind {(byte)Kind}");
    }
  }

  /// <summary>
  /// Gets the boolean this value holds.
  /// </summary>
  /// <returns>The boolean.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.Bool"/>.</exception>
  public bool AsBool() => Expect(ValueKind.Bool) != 0;

  /// <summary>
  /// Gets the 32-bit integer this value holds.
  /// </summary>
  /// <returns>The integer.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.Int"/>.</exception>
  public int AsInt() => (int)Expect(ValueKind.Int);

  /// <summary>
  /// Gets the 64-bit integer this value holds.
  /// </summary>
  /// <returns>The integer.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.Long"/>.</exception>
  public long AsLong() => Expect(ValueKind.Long);

  /// <summary>
  /// Gets the 32-bit float this value holds.
  /// </summary>
  /// <returns>The float.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.Float"/>.</exception>
  public float AsFloat() => BitConverter.Int32BitsToSingle((int)Expect(ValueKind.Float));

  /// <summary>
  /// Gets the 64-bit float this value holds.
  /// </summary>
  /// <returns>The float.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.Double"/>.</exception>
  public double AsDouble() => BitConverter.Int64BitsToDouble(Expect(ValueKind.Double));

  /// <summary>
  /// Gets the text this value holds.
  /// </summary>
  /// <returns>The text.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.String"/>.</exception>
  public string AsString()
  {
    Expect(ValueKind.String);
    return _text ?? string.Empty;
  }

  /// <summary>
  /// Gets the error message this value holds.
  /// </summary>
  /// <returns>The message.</returns>
  /// <exception cref="InvalidOperationException">The kind is not <see cref="ValueKind.Error"/>.</exception>
  public string AsError()
  {
    Expect(ValueKind.Error);
    return _text ?? string.Empty;
  }

  /// <summary>
  /// Returns a short human-readable form such as <c>Float: 2.5</c>.
  /// </summary>
  /// <returns>The text form of this value.</returns>
  public override string ToString() => Kind switch
  {
    ValueKind.Bool => $"Bool: {AsBool()}",
    ValueKind.Int => $"Int: {AsInt().ToString(CultureInfo.InvariantCulture)}",
    ValueKind.Long => $"Long: {AsLong().ToString(CultureInfo.InvariantCulture)}",
    ValueKind.Float => $"Float: {AsFloat().ToString(CultureInfo.InvariantCulture)}",
    ValueKind.Double => $"Double: {AsDouble().ToString(CultureInfo.InvariantCulture)}",
    ValueKind.String => $"String: \"{AsString()}\"",
    ValueKind.Error => $"Error: \"{AsError()}\"",
    _ => "Invalid",
  };

  private long Expect(ValueKind expected)
  {
    if (Kind != expected)
    {
      throw new InvalidOperationException($"The value is {Kind}, not {expected}");
    }

    return _bits;
  }
}
