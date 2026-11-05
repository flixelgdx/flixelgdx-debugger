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

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// Places a camera's view inside a canvas, letterboxed so it keeps its aspect ratio.
/// </summary>
/// <remarks>
/// <para>
/// The viewport panel draws what the camera shows as a rectangle in the middle of its canvas, as
/// large as fits. Think of a photo in a frame with a mat: the photo is scaled until it touches two
/// sides, and the mat fills the rest. This struct is the arithmetic of that frame. It turns a
/// canvas pixel into a point of view space and back, so drawing and mouse handling agree.
/// </para>
/// <para>
/// One view unit is <see cref="Scale"/> canvas pixels in both directions. View space has its origin
/// at the top left corner of what the camera shows, so a point keeps its view coordinates while
/// the camera scrolls. Only a zoom (a new view size) changes the fit.
/// </para>
/// </remarks>
/// <param name="Left">The canvas x coordinate of the left edge of the view.</param>
/// <param name="Top">The canvas y coordinate of the top edge of the view.</param>
/// <param name="Scale">How many canvas pixels one view unit takes, or 0 for no view.</param>
/// <param name="Width">The width of the view in canvas pixels.</param>
/// <param name="Height">The height of the view in canvas pixels.</param>
public readonly record struct ViewportFit(
    double Left, double Top, double Scale, double Width, double Height)
{
  /// <summary>
  /// Gets whether there is no view to draw, for example before the first payload.
  /// </summary>
  public bool IsEmpty => !(Scale > 0);

  /// <summary>
  /// Fits a view into a canvas, centered, with a margin on every side.
  /// </summary>
  /// <param name="canvasWidth">The canvas width in pixels.</param>
  /// <param name="canvasHeight">The canvas height in pixels.</param>
  /// <param name="viewWidth">The view width in view units.</param>
  /// <param name="viewHeight">The view height in view units.</param>
  /// <param name="margin">The empty space to keep around the view, in pixels.</param>
  /// <returns>The fit, or an empty one when a size is not positive.</returns>
  public static ViewportFit Compute(
      double canvasWidth,
      double canvasHeight,
      double viewWidth,
      double viewHeight,
      double margin)
  {
    var roomWidth = canvasWidth - (2 * margin);
    var roomHeight = canvasHeight - (2 * margin);
    if (!(roomWidth > 0 && roomHeight > 0 && viewWidth > 0 && viewHeight > 0) ||
        !double.IsFinite(viewWidth) || !double.IsFinite(viewHeight))
    {
      return default;
    }

    var scale = Math.Min(roomWidth / viewWidth, roomHeight / viewHeight);
    var width = viewWidth * scale;
    var height = viewHeight * scale;
    return new ViewportFit(
        (canvasWidth - width) / 2, (canvasHeight - height) / 2, scale, width, height);
  }

  /// <summary>
  /// Converts a canvas x coordinate into view space.
  /// </summary>
  /// <param name="x">The canvas x coordinate in pixels.</param>
  /// <returns>The view x coordinate.</returns>
  public double ToViewX(double x) => (x - Left) / Scale;

  /// <summary>
  /// Converts a canvas y coordinate into view space.
  /// </summary>
  /// <param name="y">The canvas y coordinate in pixels.</param>
  /// <returns>The view y coordinate.</returns>
  public double ToViewY(double y) => (y - Top) / Scale;

  /// <summary>
  /// Converts a view x coordinate into a canvas x coordinate.
  /// </summary>
  /// <param name="viewX">The view x coordinate.</param>
  /// <returns>The canvas x coordinate in pixels.</returns>
  public double ToCanvasX(double viewX) => Left + (viewX * Scale);

  /// <summary>
  /// Converts a view y coordinate into a canvas y coordinate.
  /// </summary>
  /// <param name="viewY">The view y coordinate.</param>
  /// <returns>The canvas y coordinate in pixels.</returns>
  public double ToCanvasY(double viewY) => Top + (viewY * Scale);
}
