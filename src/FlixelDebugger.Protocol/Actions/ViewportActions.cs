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
/// Chooses the camera the viewport map shows and the camera actions act on (viewport panel).
/// </summary>
/// <param name="Index">A position in the game's camera list.</param>
public sealed record ViewportSetCameraAction(byte Index) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Viewport.SetCamera;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportSetCameraAction Read(ref MessageReader reader) => new(reader.ReadU8());

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Index);
  }
}

/// <summary>
/// Selects the topmost object drawn at a point of the view, or clears the selection when nothing
/// is drawn there (viewport panel).
/// </summary>
/// <param name="ViewX">The x coordinate in view units.</param>
/// <param name="ViewY">The y coordinate in view units.</param>
public sealed record ViewportPickAction(float ViewX, float ViewY) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Viewport.Pick;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportPickAction Read(ref MessageReader reader)
  {
    var x = reader.ReadF32();
    return new ViewportPickAction(x, reader.ReadF32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteF32(ViewX).WriteF32(ViewY);
  }
}

/// <summary>
/// Moves an object by a distance in world units (viewport panel).
/// </summary>
/// <remarks>
/// It is a distance, not a position, so moves that arrive late or close together add up instead
/// of fighting.
/// </remarks>
/// <param name="Id">The object id.</param>
/// <param name="Dx">The horizontal distance in world units.</param>
/// <param name="Dy">The vertical distance in world units.</param>
public sealed record ViewportMoveAction(int Id, float Dx, float Dy) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Viewport.Move;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportMoveAction Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    var dx = reader.ReadF32();
    return new ViewportMoveAction(id, dx, reader.ReadF32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id).WriteF32(Dx).WriteF32(Dy);
  }
}

/// <summary>
/// Moves the camera by a distance in view units and turns the free camera on (viewport panel).
/// </summary>
/// <remarks>
/// A positive <see cref="Dx"/> moves the camera right, so the world slides left. To drag the world
/// with the pointer, send the pointer's movement with the sign flipped.
/// </remarks>
/// <param name="Dx">The horizontal distance in view units.</param>
/// <param name="Dy">The vertical distance in view units.</param>
public sealed record ViewportPanAction(float Dx, float Dy) : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Viewport.Pan;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportPanAction Read(ref MessageReader reader)
  {
    var dx = reader.ReadF32();
    return new ViewportPanAction(dx, reader.ReadF32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteF32(Dx).WriteF32(Dy);
  }
}

/// <summary>
/// Multiplies the camera zoom and keeps one point of the view still, like a pinch (viewport
/// panel).
/// </summary>
/// <param name="Factor">
/// The zoom multiplier, for example 1.25 for one wheel notch in. The game ignores a factor that is
/// not positive and keeps the zoom between 0.1 and 10.
/// </param>
/// <param name="AnchorViewX">The x coordinate to keep still, in view units before the zoom.</param>
/// <param name="AnchorViewY">The y coordinate to keep still.</param>
public sealed record ViewportZoomAction(float Factor, float AnchorViewX, float AnchorViewY)
    : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Viewport.Zoom;

  /// <summary>
  /// Reads the fields of this action.
  /// </summary>
  /// <param name="reader">The reader, positioned just after the action code.</param>
  /// <returns>The action that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportZoomAction Read(ref MessageReader reader)
  {
    var factor = reader.ReadF32();
    var x = reader.ReadF32();
    return new ViewportZoomAction(factor, x, reader.ReadF32());
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteF32(Factor).WriteF32(AnchorViewX).WriteF32(AnchorViewY);
  }
}

/// <summary>
/// Turns the free camera off, which restores the camera exactly (viewport panel).
/// </summary>
public sealed record ViewportResetCameraAction : ActionPayload
{
  /// <inheritdoc/>
  public override byte Code => A.Viewport.ResetCamera;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer) { }
}
