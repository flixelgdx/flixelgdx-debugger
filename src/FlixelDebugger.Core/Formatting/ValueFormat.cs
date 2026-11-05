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
using FlixelDebugger.Protocol;

namespace FlixelDebugger.Core.Formatting;

/// <summary>
/// Turns property values into the text the panels show, and edit text back into values.
/// </summary>
/// <remarks>
/// <para>
/// Every panel that shows a <see cref="PropertyValue"/> (watches, trackers, the inspector, custom panels) uses the
/// same rules, so a number never looks different from one panel to the next:
/// </para>
/// <list type="bullet">
/// <item><description>a whole number is shown without decimals (<c>3</c>, not <c>3.00</c>);</description></item>
/// <item><description>any other number is shown with two decimals (<c>1.50</c>);</description></item>
/// <item><description>an <see cref="ValueKind.Error"/> value becomes <c>error: message</c>.</description></item>
/// </list>
/// </remarks>
public static class ValueFormat
{
    /// <summary>The prefix of the text shown for an error value.</summary>
    public const string ErrorPrefix = "error: ";

    /// <summary>
    /// Formats a value for display.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text to show.</returns>
    public static string Display(PropertyValue value) => value.Kind switch
    {
        ValueKind.Bool => value.AsBool() ? "true" : "false",
        ValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        ValueKind.Long => value.AsLong().ToString(CultureInfo.InvariantCulture),
        ValueKind.Float => Number(value.AsFloat()),
        ValueKind.Double => Number(value.AsDouble()),
        ValueKind.String => value.AsString(),
        ValueKind.Error => ErrorPrefix + value.AsError(),
        _ => string.Empty,
    };

    /// <summary>
    /// Formats a value as the text of an editor, which keeps all digits so that committing an unchanged text cannot lose precision.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text to put in the editor.</returns>
    public static string EditText(PropertyValue value) => value.Kind switch
    {
        ValueKind.Float => value.AsFloat().ToString(CultureInfo.InvariantCulture),
        ValueKind.Double => value.AsDouble().ToString(CultureInfo.InvariantCulture),
        _ => Display(value),
    };

    /// <summary>
    /// Formats a number: whole numbers as integers, others with two decimals.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text to show.</returns>
    public static string Number(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tells whether a value is an error value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> if the game failed to read it.</returns>
    public static bool IsError(PropertyValue value) => value.Kind == ValueKind.Error;

    /// <summary>
    /// Reads edit text as a value of a given kind.
    /// </summary>
    /// <param name="kind">The kind of the row being edited. The new value has the same kind.</param>
    /// <param name="text">The text the person typed.</param>
    /// <param name="value">The parsed value, when the text is valid.</param>
    /// <returns><see langword="true"/> if the text is a valid value of that kind.</returns>
    public static bool TryParse(ValueKind kind, string? text, out PropertyValue value)
    {
        text ??= string.Empty;
        var trimmed = text.Trim();
        var culture = CultureInfo.InvariantCulture;
        switch (kind)
        {
            case ValueKind.String:
                value = PropertyValue.FromString(text);
                return true;
            case ValueKind.Bool when bool.TryParse(trimmed, out var flag):
                value = PropertyValue.FromBool(flag);
                return true;
            case ValueKind.Int when int.TryParse(trimmed, NumberStyles.AllowLeadingSign, culture, out var i):
                value = PropertyValue.FromInt(i);
                return true;
            case ValueKind.Long when long.TryParse(trimmed, NumberStyles.AllowLeadingSign, culture, out var l):
                value = PropertyValue.FromLong(l);
                return true;
            case ValueKind.Float when float.TryParse(trimmed, NumberStyles.Float, culture, out var f) && float.IsFinite(f):
                value = PropertyValue.FromFloat(f);
                return true;
            case ValueKind.Double when double.TryParse(trimmed, NumberStyles.Float, culture, out var d) && double.IsFinite(d):
                value = PropertyValue.FromDouble(d);
                return true;
            default:
                value = default;
                return false;
        }
    }
}
