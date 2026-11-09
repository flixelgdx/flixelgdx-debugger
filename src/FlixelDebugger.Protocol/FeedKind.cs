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
/// The kind of a feed, which decides how its payloads and actions are encoded.
/// </summary>
public enum FeedKind : byte
{

  /// <summary>Built-in <c>stats</c> feed.</summary>
  Stats = 1,

  /// <summary>Built-in <c>logs</c> feed.</summary>
  Logs = 2,

  /// <summary>Built-in <c>watches</c> feed.</summary>
  Watches = 3,

  /// <summary>Built-in <c>trackers</c> feed.</summary>
  Trackers = 4,

  /// <summary>Built-in <c>textures</c> feed.</summary>
  Textures = 5,

  /// <summary>Built-in <c>controls</c> feed.</summary>
  Controls = 6,

  /// <summary>Built-in <c>console</c> feed.</summary>
  Console = 7,

  /// <summary>Built-in <c>scene</c> feed.</summary>
  Scene = 8,

  /// <summary>Built-in <c>inspector</c> feed.</summary>
  Inspector = 9,

  /// <summary>Built-in <c>save</c> feed.</summary>
  Save = 10,

  /// <summary>Built-in <c>viewport</c> feed.</summary>
  Viewport = 11,

  /// <summary>Built-in <c>capture</c> feed.</summary>
  Capture = 12,

  /// <summary>Built-in <c>input</c> feed.</summary>
  Input = 13,
}
