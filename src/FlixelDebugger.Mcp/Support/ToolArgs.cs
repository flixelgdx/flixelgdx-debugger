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
using System.Text.Json;
using ModelContextProtocol;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Reads tool arguments that an agent may write as a number or as a string.
/// </summary>
/// <remarks>
/// Agents often write an object id as the number 378 where a scene path would be a string,
/// or a list where a comma separated string is expected. These helpers accept both, and fail
/// with a message that names the argument and the type it expected, because that message is all
/// the agent sees.
/// </remarks>
internal static class ToolArgs
{
  /// <summary>
  /// Reads an argument that is an object id or a string (a scene path, a name).
  /// </summary>
  /// <param name="value">The argument as the agent sent it.</param>
  /// <param name="name">The argument name, for the error message.</param>
  /// <returns>The text; a number is written without a decimal point.</returns>
  public static string Text(JsonElement value, string name)
  {
    switch (value.ValueKind)
    {
      case JsonValueKind.String:
        return value.GetString() ?? string.Empty;
      case JsonValueKind.Number:
        return value.TryGetInt64(out var whole)
            ? whole.ToString(CultureInfo.InvariantCulture)
            : value.GetRawText();
      default:
        throw new McpException(
            $"Argument '{name}' must be a string or a number (an object id or a scene path), " +
            $"but got JSON {Describe(value)}.");
    }
  }

  /// <summary>
  /// Reads an argument that is a list of names, written as an array or a comma separated string.
  /// </summary>
  /// <param name="value">The argument as the agent sent it, or <see langword="null"/>.</param>
  /// <param name="name">The argument name, for the error message.</param>
  /// <returns>The names, or <see langword="null"/> when the argument was left out.</returns>
  public static string[]? List(JsonElement? value, string name)
  {
    if (value is not { } element || element.ValueKind is JsonValueKind.Null)
    {
      return null;
    }

    if (element.ValueKind == JsonValueKind.String)
    {
      return Split(element.GetString());
    }

    if (element.ValueKind == JsonValueKind.Array)
    {
      var names = new List<string>();
      foreach (var item in element.EnumerateArray())
      {
        if (item.ValueKind != JsonValueKind.String)
        {
          throw new McpException(
              $"Argument '{name}' must be an array of strings or a comma separated string, " +
              $"but an item is JSON {Describe(item)}.");
        }

        names.AddRange(Split(item.GetString()));
      }

      return [.. names];
    }

    throw new McpException(
        $"Argument '{name}' must be an array of strings or a comma separated string, " +
        $"but got JSON {Describe(element)}.");
  }

  private static string[] Split(string? text) => (text ?? string.Empty)
      .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

  private static string Describe(JsonElement value) => value.ValueKind switch
  {
    JsonValueKind.Object => "object",
    JsonValueKind.Array => "array",
    JsonValueKind.True or JsonValueKind.False => "boolean",
    JsonValueKind.Null => "null",
    JsonValueKind.Number => "number",
    _ => "string",
  };
}
