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

namespace FlixelDebugger.Protocol.Ui;

/// <summary>
/// One value of the widget protocol: a bool, a number, text, an error, nothing, bytes, or a list of
/// further values.
/// </summary>
/// <remarks>
/// <para>
/// Widget properties and event arguments are self-describing: every value starts with its kind, so
/// a list can hold a mix (a table row is a list of an id, a style, a list of cells, and a detail).
/// Think of a value as a labeled jar: the label says what is inside, and a jar can also be a box
/// of smaller jars.
/// </para>
/// <para>
/// The accessors are lenient on purpose, because a newer game may send a number where an older one
/// sent another number kind: <see cref="AsDouble"/> reads any number, and every accessor takes a
/// fallback that is returned when the value has another kind. Values are immutable.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var row = UiValue.Read(ref reader);
/// var id = row[0].AsLong();
/// var cells = row[2].Items;
/// </code>
/// </example>
public sealed class UiValue : IEquatable<UiValue>
{
  private static readonly IReadOnlyList<UiValue> _noItems = [];

  private readonly long _bits;
  private readonly object? _object;

  private UiValue(ValueKind kind, long bits, object? value)
  {
    Kind = kind;
    _bits = bits;
    _object = value;
  }

  /// <summary>Gets the <c>NULL</c> value.</summary>
  public static UiValue Null { get; } = new(ValueKind.Null, 0, null);

  /// <summary>Gets an empty list.</summary>
  public static UiValue EmptyList { get; } = new(ValueKind.List, 0, _noItems);

  /// <summary>Gets the kind of the value.</summary>
  public ValueKind Kind { get; }

  /// <summary>Gets whether the value is <c>NULL</c>.</summary>
  public bool IsNull => Kind == ValueKind.Null;

  /// <summary>Gets whether the value is one of the four number kinds.</summary>
  public bool IsNumber =>
      Kind is ValueKind.Int or ValueKind.Long or ValueKind.Float or ValueKind.Double;

  /// <summary>Gets the items of a list, or an empty list for any other kind.</summary>
  public IReadOnlyList<UiValue> Items =>
      Kind == ValueKind.List ? (IReadOnlyList<UiValue>)_object! : _noItems;

  /// <summary>Gets the number of items of a list, or 0 for any other kind.</summary>
  public int Count => Items.Count;

  /// <summary>
  /// Gets an item of a list, or <see cref="Null"/> when the index is past the end (so readers can
  /// treat missing trailing items as defaults, as the protocol says).
  /// </summary>
  /// <param name="index">The position of the item.</param>
  public UiValue this[int index]
  {
    get
    {
      var items = Items;
      return index >= 0 && index < items.Count ? items[index] : Null;
    }
  }

  /// <summary>Creates a <c>BOOL</c> value.</summary>
  /// <param name="value">The flag.</param>
  /// <returns>The value.</returns>
  public static UiValue FromBool(bool value) => new(ValueKind.Bool, value ? 1 : 0, null);

  /// <summary>Creates an <c>INT</c> value.</summary>
  /// <param name="value">The number.</param>
  /// <returns>The value.</returns>
  public static UiValue FromInt(int value) => new(ValueKind.Int, value, null);

  /// <summary>Creates a <c>LONG</c> value.</summary>
  /// <param name="value">The number.</param>
  /// <returns>The value.</returns>
  public static UiValue FromLong(long value) => new(ValueKind.Long, value, null);

  /// <summary>Creates a <c>FLOAT</c> value.</summary>
  /// <param name="value">The number.</param>
  /// <returns>The value.</returns>
  public static UiValue FromFloat(float value) =>
      new(ValueKind.Float, BitConverter.DoubleToInt64Bits(value), null);

  /// <summary>Creates a <c>DOUBLE</c> value.</summary>
  /// <param name="value">The number.</param>
  /// <returns>The value.</returns>
  public static UiValue FromDouble(double value) =>
      new(ValueKind.Double, BitConverter.DoubleToInt64Bits(value), null);

  /// <summary>Creates a <c>STRING</c> value.</summary>
  /// <param name="value">The text; <see langword="null"/> becomes empty.</param>
  /// <returns>The value.</returns>
  public static UiValue FromString(string? value) =>
      new(ValueKind.String, 0, value ?? string.Empty);

  /// <summary>Creates an <c>ERROR</c> value.</summary>
  /// <param name="message">The error message.</param>
  /// <returns>The value.</returns>
  public static UiValue FromError(string? message) =>
      new(ValueKind.Error, 0, message ?? string.Empty);

  /// <summary>Creates a <c>BYTES</c> value.</summary>
  /// <param name="data">The bytes; the array is kept, not copied.</param>
  /// <returns>The value.</returns>
  public static UiValue FromBytes(byte[] data)
  {
    ArgumentNullException.ThrowIfNull(data);
    return new UiValue(ValueKind.Bytes, 0, data);
  }

  /// <summary>Creates a <c>LIST</c> value.</summary>
  /// <param name="items">The items; the list is kept, not copied.</param>
  /// <returns>The value.</returns>
  public static UiValue FromList(IReadOnlyList<UiValue> items)
  {
    ArgumentNullException.ThrowIfNull(items);
    return items.Count == 0 ? EmptyList : new UiValue(ValueKind.List, 0, items);
  }

  /// <summary>Creates a value from a feed's property value.</summary>
  /// <param name="value">The property value.</param>
  /// <returns>The same value as a <see cref="UiValue"/>.</returns>
  public static UiValue FromProperty(PropertyValue value) => value.Kind switch
  {
    ValueKind.Bool => FromBool(value.AsBool()),
    ValueKind.Int => FromInt(value.AsInt()),
    ValueKind.Long => FromLong(value.AsLong()),
    ValueKind.Float => FromFloat(value.AsFloat()),
    ValueKind.Double => FromDouble(value.AsDouble()),
    ValueKind.String => FromString(value.AsString()),
    ValueKind.Error => FromError(value.AsError()),
    _ => Null,
  };

  /// <summary>Reads a value (and, for a list, everything inside it).</summary>
  /// <param name="reader">The reader positioned at the value's kind byte.</param>
  /// <returns>The value.</returns>
  /// <exception cref="ProtocolException">The message is too short or a kind is unknown.</exception>
  public static UiValue Read(ref MessageReader reader)
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
      case ValueKind.Null:
        return Null;
      case ValueKind.Bytes:
        return FromBytes(reader.ReadBytes().ToArray());
      case ValueKind.List:
        {
          var count = reader.ReadCount();
          if (count == 0)
          {
            return EmptyList;
          }

          var items = new UiValue[count];
          for (var i = 0; i < count; i++)
          {
            items[i] = Read(ref reader);
          }

          return new UiValue(ValueKind.List, 0, items);
        }

      default:
        throw new ProtocolException($"Unknown value kind: {(byte)kind}");
    }
  }

  /// <summary>Writes this value (and everything inside a list).</summary>
  /// <param name="writer">The writer.</param>
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
        writer.WriteValue((float)BitConverter.Int64BitsToDouble(_bits));
        break;
      case ValueKind.Double:
        writer.WriteValue(BitConverter.Int64BitsToDouble(_bits));
        break;
      case ValueKind.String:
        writer.WriteValue((string)_object!);
        break;
      case ValueKind.Error:
        writer.WriteErrorValue((string)_object!);
        break;
      case ValueKind.Null:
        writer.WriteU8((byte)ValueKind.Null);
        break;
      case ValueKind.Bytes:
        writer.WriteU8((byte)ValueKind.Bytes).WriteBytes((byte[])_object!);
        break;
      default:
        writer.WriteU8((byte)ValueKind.List).BeginArray();
        foreach (var item in Items)
        {
          item.Write(writer);
        }

        writer.EndArray(Items.Count);
        break;
    }
  }

  /// <summary>Returns the value as a bool.</summary>
  /// <param name="fallback">What to return for any kind other than <c>BOOL</c>.</param>
  /// <returns>The flag, or the fallback.</returns>
  public bool AsBool(bool fallback = false) => Kind == ValueKind.Bool ? _bits != 0 : fallback;

  /// <summary>Returns the value as a long; any number kind is converted like a cast.</summary>
  /// <param name="fallback">What to return for a value that is not a number.</param>
  /// <returns>The number, or the fallback.</returns>
  public long AsLong(long fallback = 0) => Kind switch
  {
    ValueKind.Int or ValueKind.Long => _bits,
    ValueKind.Float or ValueKind.Double => (long)BitConverter.Int64BitsToDouble(_bits),
    _ => fallback,
  };

  /// <summary>Returns the value as an int; any number kind is converted like a cast.</summary>
  /// <param name="fallback">What to return for a value that is not a number.</param>
  /// <returns>The number, or the fallback.</returns>
  public int AsInt(int fallback = 0) => IsNumber ? (int)AsLong() : fallback;

  /// <summary>Returns the value as a double; any number kind is converted.</summary>
  /// <param name="fallback">What to return for a value that is not a number.</param>
  /// <returns>The number, or the fallback.</returns>
  public double AsDouble(double fallback = 0) => Kind switch
  {
    ValueKind.Int or ValueKind.Long => _bits,
    ValueKind.Float or ValueKind.Double => BitConverter.Int64BitsToDouble(_bits),
    _ => fallback,
  };

  /// <summary>Returns the value as a float; any number kind is converted.</summary>
  /// <param name="fallback">What to return for a value that is not a number.</param>
  /// <returns>The number, or the fallback.</returns>
  public float AsFloat(float fallback = 0) => IsNumber ? (float)AsDouble() : fallback;

  /// <summary>Returns the text of a <c>STRING</c> or <c>ERROR</c> value.</summary>
  /// <param name="fallback">What to return for any other kind.</param>
  /// <returns>The text, or the fallback.</returns>
  public string AsString(string fallback = "") =>
      Kind is ValueKind.String or ValueKind.Error ? (string)_object! : fallback;

  /// <summary>Returns the bytes of a <c>BYTES</c> value.</summary>
  /// <returns>The bytes, or <see langword="null"/> for any other kind.</returns>
  public byte[]? AsBytes() => Kind == ValueKind.Bytes ? (byte[])_object! : null;

  /// <summary>Converts a simple value to a feed property value.</summary>
  /// <returns>The property value; lists, bytes, and nulls become an empty string.</returns>
  public PropertyValue ToProperty() => Kind switch
  {
    ValueKind.Bool => PropertyValue.FromBool(AsBool()),
    ValueKind.Int => PropertyValue.FromInt((int)_bits),
    ValueKind.Long => PropertyValue.FromLong(_bits),
    ValueKind.Float => PropertyValue.FromFloat(AsFloat()),
    ValueKind.Double => PropertyValue.FromDouble(AsDouble()),
    ValueKind.Error => PropertyValue.FromError(AsString()),
    _ => PropertyValue.FromString(AsString()),
  };

  /// <summary>Returns the value as text, the way a table cell shows it.</summary>
  /// <returns>The display text.</returns>
  public string ToDisplay() => Kind switch
  {
    ValueKind.Bool => _bits != 0 ? "true" : "false",
    ValueKind.Int or ValueKind.Long => _bits.ToString(CultureInfo.InvariantCulture),
    ValueKind.Float or ValueKind.Double => FormatNumber(AsDouble()),
    ValueKind.String or ValueKind.Error => (string)_object!,
    ValueKind.Bytes => $"{((byte[])_object!).Length} bytes",
    ValueKind.List => string.Join(", ", Items.Select(item => item.ToDisplay())),
    _ => string.Empty,
  };

  /// <summary>
  /// Converts the value to plain .NET objects: bool, long, double, string, byte array, a list of
  /// objects, or <see langword="null"/>. Tools that print JSON use it.
  /// </summary>
  /// <returns>The plain object.</returns>
  public object? ToPlain() => Kind switch
  {
    ValueKind.Bool => _bits != 0,
    ValueKind.Int or ValueKind.Long => _bits,
    ValueKind.Float or ValueKind.Double => AsDouble(),
    ValueKind.String => (string)_object!,
    ValueKind.Error => "error: " + (string)_object!,
    ValueKind.Bytes => (byte[])_object!,
    ValueKind.List => Items.Select(item => item.ToPlain()).ToList(),
    _ => null,
  };

  /// <inheritdoc/>
  public bool Equals(UiValue? other)
  {
    if (other is null || other.Kind != Kind)
    {
      return false;
    }

    return Kind switch
    {
      ValueKind.String or ValueKind.Error => (string)_object! == (string)other._object!,
      ValueKind.Bytes => ((byte[])_object!).AsSpan().SequenceEqual((byte[])other._object!),
      ValueKind.List => Items.SequenceEqual(other.Items),
      _ => _bits == other._bits,
    };
  }

  /// <inheritdoc/>
  public override bool Equals(object? obj) => obj is UiValue other && Equals(other);

  /// <inheritdoc/>
  public override int GetHashCode() => HashCode.Combine(Kind, _bits, Count);

  /// <inheritdoc/>
  public override string ToString() => $"{Kind}: {ToDisplay()}";

  private static string FormatNumber(double value) =>
      value == Math.Round(value) && Math.Abs(value) < 1e15
          ? value.ToString("0", CultureInfo.InvariantCulture)
          : value.ToString("0.###", CultureInfo.InvariantCulture);
}
