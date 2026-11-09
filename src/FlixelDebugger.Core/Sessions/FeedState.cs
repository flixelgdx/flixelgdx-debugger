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

using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Feeds;
using FlixelDebugger.Protocol.Messages;

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// The latest known state of one feed of one game.
/// </summary>
/// <remarks>
/// <para>
/// A feed is identified by its <see cref="Id"/>. The game numbers feeds, but the numbers change
/// across connections, so the session keeps one <see cref="FeedState"/> per
/// id and only updates its <see cref="Index"/> when a new feed list arrives.
/// </para>
/// <para>
/// <see cref="Payload"/> is the newest payload of any kind the feed sent. Feeds that send several
/// kinds (console, inspector, textures) have their own typed properties on
/// <see cref="GameSession"/>, which keep the latest payload of each kind. All members are safe to
/// read from any thread.
/// </para>
/// </remarks>
public sealed class FeedState
{
  private volatile FeedPayload? _payload;
  private long _version;
  private int _index;

  internal FeedState(FeedInfo info)
  {
    Id = info.Id;
    Kind = info.Kind;
    _index = info.Index;
  }

  /// <summary>
  /// Gets the stable identifier of the feed, for example <c>stats</c> or <c>scene</c>.
  /// </summary>
  public string Id { get; }

  /// <summary>Gets the kind of the feed.</summary>
  public FeedKind Kind { get; }

  /// <summary>
  /// Gets the feed's current index in the game's feed list, which the wire protocol uses to
  /// address it.
  /// </summary>
  public int Index => Volatile.Read(ref _index);

  /// <summary>
  /// Gets the newest payload the feed sent, or <see langword="null"/> if it has not sent one yet.
  /// </summary>
  public FeedPayload? Payload => _payload;

  /// <summary>Gets how many payloads the feed has sent. It works as a version number.</summary>
  public long Version => Interlocked.Read(ref _version);

  internal void Update(FeedInfo info)
  {
    Volatile.Write(ref _index, info.Index);
  }

  internal void SetPayload(FeedPayload payload)
  {
    _payload = payload;
    Interlocked.Increment(ref _version);
  }
}
