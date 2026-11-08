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

using System.Text.Json;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Checks tool arguments against the tool's input schema before the tool runs.
/// </summary>
/// <remarks>
/// When an argument has the wrong JSON type, the MCP SDK only answers "An error occurred invoking
/// the tool", which tells a language model nothing. This check runs first and names the argument,
/// the expected type, and what was received.
/// </remarks>
internal static class ArgumentCheck
{
  /// <summary>
  /// Finds the first problem with the arguments of a call.
  /// </summary>
  /// <param name="schema">The tool's input schema (a JSON Schema object).</param>
  /// <param name="arguments">The arguments the agent sent, or <see langword="null"/>.</param>
  /// <returns>A message naming the problem, or <see langword="null"/> when all is well.</returns>
  public static string? Find(JsonElement schema, IDictionary<string, JsonElement>? arguments)
  {
    if (schema.ValueKind != JsonValueKind.Object ||
        !schema.TryGetProperty("properties", out var properties) ||
        properties.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    if (schema.TryGetProperty("required", out var required) &&
        required.ValueKind == JsonValueKind.Array)
    {
      foreach (var item in required.EnumerateArray())
      {
        var name = item.GetString();
        if (name is not null && (arguments is null || !arguments.ContainsKey(name)))
        {
          return $"Missing required argument '{name}' (expected {Expected(properties, name)}).";
        }
      }
    }

    if (arguments is null)
    {
      return null;
    }

    foreach (var (name, value) in arguments)
    {
      if (!properties.TryGetProperty(name, out var property) ||
          !property.TryGetProperty("type", out var type) || Accepts(type, value))
      {
        continue;
      }

      return $"Argument '{name}' must be {Expected(properties, name)}, " +
          $"but got JSON {Kind(value)}: {Shorten(value.GetRawText())}";
    }

    return null;
  }

  private static bool Accepts(JsonElement type, JsonElement value)
  {
    if (type.ValueKind == JsonValueKind.Array)
    {
      foreach (var option in type.EnumerateArray())
      {
        if (Accepts(option, value))
        {
          return true;
        }
      }

      return false;
    }

    return type.GetString() switch
    {
      "string" => value.ValueKind == JsonValueKind.String,
      "integer" => value.ValueKind == JsonValueKind.Number &&
          (value.TryGetInt64(out _) || IsWhole(value)),
      "number" => value.ValueKind == JsonValueKind.Number,
      "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
      "array" => value.ValueKind == JsonValueKind.Array,
      "object" => value.ValueKind == JsonValueKind.Object,
      "null" => value.ValueKind == JsonValueKind.Null,
      _ => true,
    };
  }

  private static bool IsWhole(JsonElement value) =>
      value.TryGetDouble(out var number) && number == Math.Floor(number);

  private static string Expected(JsonElement properties, string name)
  {
    if (!properties.TryGetProperty(name, out var property) ||
        !property.TryGetProperty("type", out var type))
    {
      return "a value";
    }

    var names = type.ValueKind == JsonValueKind.Array
        ? type.EnumerateArray().Select(t => t.GetString()).Where(t => t != "null")
        : [type.GetString()];
    var text = string.Join(" or ", names.Select(n => n == "integer" ? "a whole number" : "a " + n));
    return text.Length == 0 ? "a value" : text;
  }

  private static string Kind(JsonElement value) => value.ValueKind switch
  {
    JsonValueKind.String => "string",
    JsonValueKind.Number => "number",
    JsonValueKind.True or JsonValueKind.False => "boolean",
    JsonValueKind.Array => "array",
    JsonValueKind.Object => "object",
    _ => "null",
  };

  private static string Shorten(string text) => text.Length <= 60 ? text : text[..57] + "...";
}
