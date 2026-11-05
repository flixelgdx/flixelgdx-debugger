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

using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Data;

/// <summary>
/// The columns the texture list can be sorted by.
/// </summary>
public enum TextureSortKey
{
  /// <summary>The texture id.</summary>
  Id,

  /// <summary>The name or path, ignoring case.</summary>
  Name,

  /// <summary>The pixel area (width times height).</summary>
  Size,

  /// <summary>The format name, ignoring case.</summary>
  Format,

  /// <summary>The GPU memory.</summary>
  GpuBytes,

  /// <summary>The number of sprites using the texture.</summary>
  Users,

  /// <summary>
  /// Whether the texture is a duplicate. Duplicates sort after the others when ascending.
  /// </summary>
  Duplicate,
}

/// <summary>
/// Sorts the textures of the textures panel.
/// </summary>
/// <remarks>
/// The sort is stable and always breaks ties by id, so the order never flickers between two
/// refreshes of the same list.
/// </remarks>
public static class TextureSorter
{
  /// <summary>
  /// Sorts a list of textures into a new list.
  /// </summary>
  /// <param name="source">The textures in the order the game sent them.</param>
  /// <param name="key">The column to sort by.</param>
  /// <param name="descending"><see langword="true"/> for the largest value first.</param>
  /// <returns>A new, sorted list.</returns>
  public static List<TextureEntry> Sort(
      IReadOnlyList<TextureEntry> source, TextureSortKey key, bool descending)
  {
    ArgumentNullException.ThrowIfNull(source);
    var list = new List<TextureEntry>(source);
    list.Sort((a, b) =>
    {
      var result = CompareBy(key, a, b);
      if (descending)
      {
        result = -result;
      }

      return result != 0 ? result : a.Id.CompareTo(b.Id);
    });
    return list;
  }

  private static int CompareBy(TextureSortKey key, TextureEntry a, TextureEntry b) => key switch
  {
    TextureSortKey.Name => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
    TextureSortKey.Size => ((long)a.Width * a.Height).CompareTo((long)b.Width * b.Height),
    TextureSortKey.Format => string.Compare(a.Format, b.Format, StringComparison.OrdinalIgnoreCase),
    TextureSortKey.GpuBytes => a.GpuBytes.CompareTo(b.GpuBytes),
    TextureSortKey.Users => a.Users.CompareTo(b.Users),
    TextureSortKey.Duplicate => a.Duplicate.CompareTo(b.Duplicate),
    _ => a.Id.CompareTo(b.Id),
  };
}
