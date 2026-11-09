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
/// The kind byte that starts every property value, which says how the data after it is encoded.
/// </summary>
public enum ValueKind : byte
{
  /// <summary>A boolean.</summary>
  Bool = 1,

  /// <summary>A signed 32-bit integer.</summary>
  Int = 2,

  /// <summary>A signed 64-bit integer.</summary>
  Long = 3,

  /// <summary>A 32-bit float.</summary>
  Float = 4,

  /// <summary>A 64-bit float.</summary>
  Double = 5,

  /// <summary>A string.</summary>
  String = 6,

  /// <summary>
  /// An error message, used when reading the value threw an exception. Only the game sends it.
  /// </summary>
  Error = 7,

  /// <summary>No value, for example a cleared selection.</summary>
  Null = 8,

  /// <summary>Raw bytes, for example a PNG picture.</summary>
  Bytes = 9,

  /// <summary>
  /// A list of values, which may have different kinds and may be lists themselves.
  /// </summary>
  List = 10,
}
