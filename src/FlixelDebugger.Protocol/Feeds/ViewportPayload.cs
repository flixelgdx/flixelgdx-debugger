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

namespace FlixelDebugger.Protocol.Feeds;

/// <summary>
/// The state of the <c>viewport</c> feed (kind 11): a live map of what one camera shows.
/// </summary>
/// <remarks>
/// <para>
/// Think of it as a minimap the game draws for the app: instead of pixels it sends one rectangle
/// per object, so the app can pick objects, drag them, and fly the camera on a device without a
/// keyboard or mouse.
/// </para>
/// <para>
/// <b>View space.</b> Every box is in the view space of the camera: <c>0, 0</c> is the top left
/// corner of what the camera shows, and the visible area is <see cref="ViewWidth"/> by
/// <see cref="ViewHeight"/>. For an object with a scroll factor of 1, one view unit is one world
/// unit. Boxes are listed in draw order, bottom first, so the last box that contains a point is
/// the one on top.
/// </para>
/// </remarks>
/// <param name="Camera">The index of the camera the map shows.</param>
/// <param name="CameraCount">
/// How many cameras the game has. 0 means there is no camera and no boxes.
/// </param>
/// <param name="ViewWidth">The visible width in view units (world width divided by zoom).</param>
/// <param name="ViewHeight">The visible height in view units.</param>
/// <param name="Zoom">The zoom of the camera.</param>
/// <param name="ScrollX">The camera scroll in world units.</param>
/// <param name="ScrollY">The camera scroll in world units.</param>
/// <param name="FreeCam">Whether the free camera is on.</param>
/// <param name="Paused">Whether the game is paused.</param>
/// <param name="Hitboxes">Whether hitboxes are shown.</param>
/// <param name="SelectedId">The id of the inspector's selection, or -1.</param>
/// <param name="Types">
/// The type names boxes refer to by index. The indices are only valid inside this payload.
/// </param>
/// <param name="Boxes">The boxes, in draw order, bottom first.</param>
public sealed record ViewportPayload(
    byte Camera,
    byte CameraCount,
    float ViewWidth,
    float ViewHeight,
    float Zoom,
    float ScrollX,
    float ScrollY,
    bool FreeCam,
    bool Paused,
    bool Hitboxes,
    int SelectedId,
    IReadOnlyList<string> Types,
    IReadOnlyList<ViewportBox> Boxes) : FeedPayload
{
  /// <summary>
  /// Reads a viewport payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportPayload Read(ref MessageReader reader)
  {
    var camera = reader.ReadU8();
    var cameraCount = reader.ReadU8();
    var viewWidth = reader.ReadF32();
    var viewHeight = reader.ReadF32();
    var zoom = reader.ReadF32();
    var scrollX = reader.ReadF32();
    var scrollY = reader.ReadF32();
    var freeCam = reader.ReadBool();
    var paused = reader.ReadBool();
    var hitboxes = reader.ReadBool();
    var selectedId = reader.ReadI32();
    var types = reader.ReadArray(static (ref MessageReader r) => r.ReadString());
    var boxes = reader.ReadArray(ViewportBox.Read);
    return new ViewportPayload(
        camera,
        cameraCount,
        viewWidth,
        viewHeight,
        zoom,
        scrollX,
        scrollY,
        freeCam,
        paused,
        hitboxes,
        selectedId,
        types,
        boxes);
  }

  /// <summary>
  /// Gets the type name of a box, or an empty string when its type index is out of range.
  /// </summary>
  /// <param name="box">A box of this payload.</param>
  /// <returns>The simple class name of the object.</returns>
  public string TypeOf(ViewportBox box) =>
      box.TypeIndex < Types.Count ? Types[box.TypeIndex] : string.Empty;

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteU8(Camera)
        .WriteU8(CameraCount)
        .WriteF32(ViewWidth)
        .WriteF32(ViewHeight)
        .WriteF32(Zoom)
        .WriteF32(ScrollX)
        .WriteF32(ScrollY)
        .WriteBool(FreeCam)
        .WriteBool(Paused)
        .WriteBool(Hitboxes)
        .WriteI32(SelectedId)
        .WriteArray(Types, static (w, type) => w.WriteString(type))
        .WriteArray(Boxes, static (w, box) => box.Write(w));
  }
}

/// <summary>
/// The flag bits of a <see cref="ViewportBox"/>: whether it is selected, a group, or inactive.
/// </summary>
[Flags]
public enum ViewportBoxState : byte
{
  /// <summary>No flag is set.</summary>
  None = 0,

  /// <summary>The object is the inspector's selection.</summary>
  Selected = 1,

  /// <summary>The object is a group; its members have boxes of their own.</summary>
  Group = 2,

  /// <summary>The object is not active, so it is drawn but not updated.</summary>
  Inactive = 4,
}

/// <summary>
/// The area one object is drawn in, in the view space of the viewport's camera.
/// </summary>
/// <param name="Id">The object id, the same one the scene and inspector feeds use.</param>
/// <param name="X">The left edge in view units.</param>
/// <param name="Y">The top edge in view units.</param>
/// <param name="Width">The width in view units.</param>
/// <param name="Height">The height in view units.</param>
/// <param name="TypeIndex">
/// The index of its type name in <see cref="ViewportPayload.Types"/>.
/// </param>
/// <param name="Flags">What kind of box this is.</param>
public readonly record struct ViewportBox(
    int Id,
    float X,
    float Y,
    float Width,
    float Height,
    ushort TypeIndex,
    ViewportBoxState Flags)
{
  /// <summary>Gets whether the object is the inspector's selection.</summary>
  public bool IsSelected => (Flags & ViewportBoxState.Selected) != 0;

  /// <summary>Gets whether the object is a group.</summary>
  public bool IsGroup => (Flags & ViewportBoxState.Group) != 0;

  /// <summary>Gets whether the object is not active.</summary>
  public bool IsInactive => (Flags & ViewportBoxState.Inactive) != 0;

  /// <summary>
  /// Tells whether a point in view space lies inside this box (edges included).
  /// </summary>
  /// <param name="x">The x coordinate in view units.</param>
  /// <param name="y">The y coordinate in view units.</param>
  /// <returns><see langword="true"/> if the point is inside.</returns>
  public bool Contains(float x, float y) =>
      x >= X && y >= Y && x <= X + Width && y <= Y + Height;

  /// <summary>
  /// Reads one box.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the box.</param>
  /// <returns>The box that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ViewportBox Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    var x = reader.ReadF32();
    var y = reader.ReadF32();
    var width = reader.ReadF32();
    var height = reader.ReadF32();
    var typeIndex = reader.ReadU16();
    return new ViewportBox(id, x, y, width, height, typeIndex, (ViewportBoxState)reader.ReadU8());
  }

  /// <summary>
  /// Writes this box.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id)
        .WriteF32(X)
        .WriteF32(Y)
        .WriteF32(Width)
        .WriteF32(Height)
        .WriteU16(TypeIndex)
        .WriteU8((byte)Flags);
  }
}
