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

namespace FlixelDebugger.Protocol;

/// <summary>
/// A name and a value: the row type used by the watches and trackers panels.
/// </summary>
/// <param name="Name">The row name.</param>
/// <param name="Value">The current value.</param>
public readonly record struct NamedValue(string Name, PropertyValue Value)
{
  /// <summary>
  /// Reads a row: a name followed by a value.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the row.</param>
  /// <returns>The row that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static NamedValue Read(ref MessageReader reader)
  {
    var name = reader.ReadString();
    return new NamedValue(name, PropertyValue.Read(ref reader));
  }

  /// <summary>
  /// Writes this row.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Name);
    Value.Write(writer);
  }
}
