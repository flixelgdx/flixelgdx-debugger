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

using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// Describes a feed payload that just arrived.
/// </summary>
/// <remarks>
/// Events are raised on a background thread. A UI should copy what it needs from these arguments
/// and move to its own thread before touching any control.
/// </remarks>
public sealed class FeedUpdatedEventArgs : EventArgs
{
  internal FeedUpdatedEventArgs(GameSession session, FeedState feed, FeedPayload payload)
  {
    Session = session;
    Feed = feed;
    Payload = payload;
  }

  /// <summary>Gets the session the payload came from.</summary>
  public GameSession Session { get; }

  /// <summary>Gets the feed the payload belongs to.</summary>
  public FeedState Feed { get; }

  /// <summary>Gets the payload. It is immutable, so it is safe to hand to another thread.</summary>
  public FeedPayload Payload { get; }
}
