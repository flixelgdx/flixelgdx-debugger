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
/// The kind of a panel, which decides how its payloads and actions are encoded.
/// </summary>
public enum PanelKind : byte
{
  /// <summary>A panel registered by game code, with any id.</summary>
  Custom = 0,

  /// <summary>Built-in <c>stats</c> panel.</summary>
  Stats = 1,

  /// <summary>Built-in <c>logs</c> panel.</summary>
  Logs = 2,

  /// <summary>Built-in <c>watches</c> panel.</summary>
  Watches = 3,

  /// <summary>Built-in <c>trackers</c> panel.</summary>
  Trackers = 4,

  /// <summary>Built-in <c>textures</c> panel.</summary>
  Textures = 5,

  /// <summary>Built-in <c>controls</c> panel.</summary>
  Controls = 6,

  /// <summary>Built-in <c>console</c> panel.</summary>
  Console = 7,

  /// <summary>Built-in <c>scene</c> panel.</summary>
  Scene = 8,

  /// <summary>Built-in <c>inspector</c> panel.</summary>
  Inspector = 9,

  /// <summary>Built-in <c>save</c> panel.</summary>
  Save = 10,
}
