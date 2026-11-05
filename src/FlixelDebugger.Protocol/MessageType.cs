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
/// The first byte of every protocol message, which says what kind of message follows.
/// </summary>
public enum MessageType : byte
{
  /// <summary>First message of every connection (game to app).</summary>
  Hello = 1,

  /// <summary>Answers <see cref="Hello"/> (app to game).</summary>
  HelloAck = 2,

  /// <summary>The panels that currently exist (game to app).</summary>
  PanelList = 3,

  /// <summary>A payload for one panel (game to app).</summary>
  PanelData = 4,

  /// <summary>A user action for one panel (app to game).</summary>
  PanelAction = 5,

  /// <summary>Round-trip probe (app to game).</summary>
  Ping = 6,

  /// <summary>Echo of <see cref="Ping"/> (game to app).</summary>
  Pong = 7,
}
