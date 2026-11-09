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

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// The ids of the built-in feeds, as the protocol specifies them.
/// </summary>
/// <remarks>
/// Feeds are matched by id, never by index, because the game may renumber feeds whenever the list
/// changes.
/// </remarks>
public static class BuiltInFeeds
{
  /// <summary>The performance statistics feed.</summary>
  public const string Stats = "stats";

  /// <summary>The log feed.</summary>
  public const string Logs = "logs";

  /// <summary>The watches feed.</summary>
  public const string Watches = "watches";

  /// <summary>The trackers feed.</summary>
  public const string Trackers = "trackers";

  /// <summary>The textures feed.</summary>
  public const string Textures = "textures";

  /// <summary>The controls feed.</summary>
  public const string Controls = "controls";

  /// <summary>The console feed.</summary>
  public const string Console = "console";

  /// <summary>The scene tree feed.</summary>
  public const string Scene = "scene";

  /// <summary>The inspector feed.</summary>
  public const string Inspector = "inspector";

  /// <summary>The save data feed.</summary>
  public const string Save = "save";

  /// <summary>The viewport feed: a live map of what one camera shows.</summary>
  public const string Viewport = "viewport";

  /// <summary>The capture feed: pictures of the game's screen.</summary>
  public const string Capture = "capture";

  /// <summary>The input feed: keys, pointer, scroll, and text sent to the game.</summary>
  public const string Input = "input";
}
