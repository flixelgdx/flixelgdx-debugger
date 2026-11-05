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

using A = FlixelDebugger.Protocol.ProtocolConstants.Actions;

namespace FlixelDebugger.Protocol.Actions;

/// <summary>
/// Clears the log (logs panel).
/// </summary>
public sealed record LogsClearAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Logs.Clear;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}

/// <summary>
/// Asks for a thumbnail of one texture (textures panel). The game answers with a thumbnail payload.
/// </summary>
/// <param name="Id">The texture id.</param>
public sealed record TexturesRequestThumbnailAction(int Id) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Textures.RequestThumbnail;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static TexturesRequestThumbnailAction Read(ref MessageReader reader) =>
      new(reader.ReadI32());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id);
  }
}

/// <summary>
/// Runs a console line (console panel).
/// </summary>
/// <remarks>
/// The game rejects lines longer than <see cref="ProtocolConstants.MaxCommandChars"/> characters.
/// This library does not enforce that limit, so check it before sending.
/// </remarks>
/// <param name="Line">The line to run.</param>
public sealed record ConsoleRunAction(string Line) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Console.Run;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ConsoleRunAction Read(ref MessageReader reader) => new(reader.ReadString());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Line);
  }
}

/// <summary>
/// Asks for a fresh tree snapshot (scene panel).
/// </summary>
public sealed record SceneRequestTreeAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Scene.RequestTree;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}
