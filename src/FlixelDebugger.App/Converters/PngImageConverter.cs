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

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace FlixelDebugger.App.Converters;

/// <summary>
/// Turns PNG bytes into a bitmap that an <c>Image</c> can show.
/// </summary>
/// <remarks>
/// The ViewModels keep thumbnails as plain bytes so they need no UI types. This converter is the one place where
/// the bytes become a <see cref="Bitmap"/>. It is called only when the picture changes, not on every refresh.
/// </remarks>
public sealed class PngImageConverter : IValueConverter
{
  /// <summary>Gets the shared instance, for use in XAML with <c>x:Static</c>.</summary>
  public static PngImageConverter Instance { get; } = new();

  /// <inheritdoc/>
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is not byte[] bytes || bytes.Length == 0)
    {
      return null;
    }

    try
    {
      using var stream = new MemoryStream(bytes, writable: false);
      return new Bitmap(stream);
    }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
    {
      // A broken picture must not break the panel: show nothing instead.
      return null;
    }
  }

  /// <inheritdoc/>
  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
