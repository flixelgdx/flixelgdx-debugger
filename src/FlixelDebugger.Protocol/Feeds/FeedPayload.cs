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

using Subtypes = FlixelDebugger.Protocol.ProtocolConstants.Subtypes;

namespace FlixelDebugger.Protocol.Feeds;

/// <summary>
/// The decoded payload of a FEED_DATA message. Its concrete type depends on the feed kind.
/// </summary>
/// <remarks>
/// Payloads never include the message type byte or the feed index. Feeds with more than one
/// payload kind (textures, console, inspector, and capture) start every payload with a subtype
/// byte, and the matching <c>Write</c> method writes it too. Subtypes this library does not know
/// decode to <see cref="UnknownPayload"/>, as the protocol requires.
/// </remarks>
public abstract record FeedPayload
{
  /// <summary>
  /// Writes this payload, including its subtype byte when the feed kind has one.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public abstract void Write(MessageWriter writer);

  /// <summary>
  /// Reads a payload for the given feed kind.
  /// </summary>
  /// <param name="kind">
  /// The kind of the feed, or <see langword="null"/> if the feed index is not known.
  /// </param>
  /// <param name="reader">The reader, positioned just after the feed index.</param>
  /// <returns>
  /// The payload, or an <see cref="UnknownPayload"/> if the kind or subtype is not understood.
  /// </returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static FeedPayload Read(FeedKind? kind, ref MessageReader reader)
  {
    if (kind is null)
    {
      return new UnknownPayload(null, reader.ReadRemaining().ToArray());
    }

    switch (kind.Value)
    {
      case FeedKind.Stats:
        return StatsPayload.Read(ref reader);
      case FeedKind.Logs:
        return LogsPayload.Read(ref reader);
      case FeedKind.Watches:
        return WatchesPayload.Read(ref reader);
      case FeedKind.Trackers:
        return TrackersPayload.Read(ref reader);
      case FeedKind.Controls:
        return ControlsPayload.Read(ref reader);
      case FeedKind.Scene:
        return ScenePayload.Read(ref reader);
      case FeedKind.Save:
        return SavePayload.Read(ref reader);
      case FeedKind.Viewport:
        return ViewportPayload.Read(ref reader);
      case FeedKind.Input:
        return InputStatePayload.Read(ref reader);
      case FeedKind.Capture:
        {
          var start = reader;
          return reader.ReadU8() switch
          {
            Subtypes.Capture.State => CaptureStatePayload.Read(ref reader),
            Subtypes.Capture.Image => CaptureImagePayload.Read(ref reader),
            _ => new UnknownPayload(kind, start.ReadRemaining().ToArray()),
          };
        }

      case FeedKind.Textures:
        {
          var start = reader;
          return reader.ReadU8() switch
          {
            Subtypes.Textures.List => TexturesListPayload.Read(ref reader),
            Subtypes.Textures.Thumbnail => TexturesThumbnailPayload.Read(ref reader),
            _ => new UnknownPayload(kind, start.ReadRemaining().ToArray()),
          };
        }

      case FeedKind.Console:
        {
          var start = reader;
          return reader.ReadU8() switch
          {
            Subtypes.Console.Commands => ConsoleCommandsPayload.Read(ref reader),
            Subtypes.Console.Result => ConsoleResultPayload.Read(ref reader),
            _ => new UnknownPayload(kind, start.ReadRemaining().ToArray()),
          };
        }

      case FeedKind.Inspector:
        {
          var start = reader;
          return reader.ReadU8() switch
          {
            Subtypes.Inspector.State => InspectorStatePayload.Read(ref reader),
            Subtypes.Inspector.Thumbnail => InspectorThumbnailPayload.Read(ref reader),
            _ => new UnknownPayload(kind, start.ReadRemaining().ToArray()),
          };
        }

      default:
        return new UnknownPayload(kind, reader.ReadRemaining().ToArray());
    }
  }
}
