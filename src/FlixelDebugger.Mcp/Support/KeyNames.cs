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

using System.Collections.Frozen;
using System.Globalization;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Turns key names such as <c>SPACE</c>, <c>A</c>, or <c>LEFT</c> into the framework's key codes.
/// </summary>
/// <remarks>
/// <para>
/// The wire protocol sends the FlixelGDX key code (<c>FlixelKey</c> in the framework), which is
/// neither an ASCII code nor an operating system scan code. An agent should not have to know the
/// numbers, so the MCP tools take names. The table below mirrors the <c>FlixelKey</c> constants.
/// A plain number from 1 to 255 is accepted as a raw code too.
/// </para>
/// <para>
/// Names are not case sensitive, and spaces and hyphens count as underscores, so
/// <c>page up</c> and <c>PAGE_UP</c> are the same key. The digits <c>0</c> to <c>9</c> mean the
/// number row keys. A few friendly aliases exist (<c>ESC</c>, <c>CTRL</c>, <c>SHIFT</c>,
/// <c>ALT</c>, <c>RETURN</c>, <c>DELETE</c>).
/// </para>
/// </remarks>
public static class KeyNames
{
  private static readonly (string Name, int Code)[] _table =
  [
      ("NUM_0", 7), ("NUM_1", 8), ("NUM_2", 9), ("NUM_3", 10), ("NUM_4", 11), ("NUM_5", 12),
      ("NUM_6", 13), ("NUM_7", 14), ("NUM_8", 15), ("NUM_9", 16), ("A", 29), ("ALT_LEFT", 57),
      ("ALT_RIGHT", 58), ("APOSTROPHE", 75), ("AT", 77), ("B", 30), ("BACK", 4),
      ("BACKSLASH", 73), ("C", 31), ("CALL", 5), ("CAMERA", 27), ("CAPS_LOCK", 115),
      ("CLEAR", 28), ("COMMA", 55), ("D", 32), ("DEL", 67), ("BACKSPACE", 67),
      ("FORWARD_DEL", 112), ("DPAD_CENTER", 23), ("DPAD_DOWN", 20), ("DPAD_LEFT", 21),
      ("DPAD_RIGHT", 22), ("DPAD_UP", 19), ("CENTER", 23), ("DOWN", 20), ("LEFT", 21),
      ("RIGHT", 22), ("UP", 19), ("E", 33), ("ENDCALL", 6), ("ENTER", 66), ("ENVELOPE", 65),
      ("EQUALS", 70), ("EXPLORER", 64), ("F", 34), ("FOCUS", 80), ("G", 35), ("GRAVE", 68),
      ("H", 36), ("HEADSETHOOK", 79), ("HOME", 3), ("I", 37), ("J", 38), ("K", 39), ("L", 40),
      ("LEFT_BRACKET", 71), ("M", 41), ("MEDIA_FAST_FORWARD", 90), ("MEDIA_NEXT", 87),
      ("MEDIA_PLAY_PAUSE", 85), ("MEDIA_PREVIOUS", 88), ("MEDIA_REWIND", 89), ("MEDIA_STOP", 86),
      ("MENU", 82), ("MINUS", 69), ("MUTE", 91), ("N", 42), ("NOTIFICATION", 83), ("NUM", 78),
      ("O", 43), ("P", 44), ("PAUSE", 121), ("PERIOD", 56), ("PLUS", 81), ("POUND", 18),
      ("POWER", 26), ("PRINT_SCREEN", 120), ("Q", 45), ("R", 46), ("RIGHT_BRACKET", 72),
      ("S", 47), ("SCROLL_LOCK", 116), ("SEARCH", 84), ("SEMICOLON", 74), ("SHIFT_LEFT", 59),
      ("SHIFT_RIGHT", 60), ("SLASH", 76), ("SOFT_LEFT", 1), ("SOFT_RIGHT", 2), ("SPACE", 62),
      ("STAR", 17), ("SYM", 63), ("T", 48), ("TAB", 61), ("U", 49), ("V", 50),
      ("VOLUME_DOWN", 25), ("VOLUME_UP", 24), ("W", 51), ("X", 52), ("Y", 53), ("Z", 54),
      ("CONTROL_LEFT", 129), ("CONTROL_RIGHT", 130), ("ESCAPE", 111), ("END", 123),
      ("INSERT", 124), ("PAGE_UP", 92), ("PAGE_DOWN", 93), ("PICTSYMBOLS", 94),
      ("SWITCH_CHARSET", 95), ("NUMPAD_0", 144), ("NUMPAD_1", 145), ("NUMPAD_2", 146),
      ("NUMPAD_3", 147), ("NUMPAD_4", 148), ("NUMPAD_5", 149), ("NUMPAD_6", 150),
      ("NUMPAD_7", 151), ("NUMPAD_8", 152), ("NUMPAD_9", 153), ("NUMPAD_DIVIDE", 154),
      ("NUMPAD_MULTIPLY", 155), ("NUMPAD_SUBTRACT", 156), ("NUMPAD_ADD", 157),
      ("NUMPAD_DOT", 158), ("NUMPAD_COMMA", 159), ("NUMPAD_ENTER", 160), ("NUMPAD_EQUALS", 161),
      ("NUMPAD_LEFT_PAREN", 162), ("NUMPAD_RIGHT_PAREN", 163), ("NUM_LOCK", 143),
      ("WORLD_1", 240), ("WORLD_2", 241), ("COLON", 243), ("F1", 131), ("F2", 132), ("F3", 133),
      ("F4", 134), ("F5", 135), ("F6", 136), ("F7", 137), ("F8", 138), ("F9", 139), ("F10", 140),
      ("F11", 141), ("F12", 142), ("F13", 183), ("F14", 184), ("F15", 185), ("F16", 186),
      ("F17", 187), ("F18", 188), ("F19", 189), ("F20", 190), ("F21", 191), ("F22", 192),
      ("F23", 193), ("F24", 194), ("ESC", 111), ("RETURN", 66), ("CTRL", 129), ("CONTROL", 129),
      ("SHIFT", 59), ("ALT", 57), ("PGUP", 92), ("PGDN", 93), ("PAGEUP", 92), ("PAGEDOWN", 93),
      ("DELETE", 112), ("0", 7), ("1", 8), ("2", 9), ("3", 10), ("4", 11), ("5", 12), ("6", 13),
      ("7", 14), ("8", 15), ("9", 16), ("UP_ARROW", 19), ("DOWN_ARROW", 20), ("LEFT_ARROW", 21),
      ("RIGHT_ARROW", 22),
  ];

  private static readonly FrozenDictionary<string, int> _codes = Build();

  /// <summary>
  /// Looks up a key by name or raw code.
  /// </summary>
  /// <param name="name">The key name, for example <c>SPACE</c>, or a number from 1 to 255.</param>
  /// <param name="code">The framework key code, when found.</param>
  /// <returns><see langword="true"/> if the key is known.</returns>
  public static bool TryGet(string? name, out int code)
  {
    code = 0;
    if (string.IsNullOrWhiteSpace(name))
    {
      return false;
    }

    var key = name.Trim().Replace(' ', '_').Replace('-', '_');
    if (_codes.TryGetValue(key, out code))
    {
      return true;
    }

    return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out code) &&
        code is >= 1 and <= 255;
  }

  /// <summary>
  /// Gets a sample of valid names, for error messages.
  /// </summary>
  /// <returns>A short comma separated list of common key names.</returns>
  public static string Examples() =>
      "A-Z, 0-9, SPACE, ENTER, ESCAPE, TAB, BACKSPACE, UP, DOWN, LEFT, RIGHT, SHIFT, CTRL, ALT, " +
      "F1-F12, PAGE_UP, PAGE_DOWN, HOME, END, INSERT, NUMPAD_0-NUMPAD_9";

  private static FrozenDictionary<string, int> Build()
  {
    var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var (name, code) in _table)
    {
      map.TryAdd(name, code);
    }

    return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
  }
}
