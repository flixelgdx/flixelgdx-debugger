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
/// One thumbnail picture received from the game.
/// </summary>
/// <param name="Id">The id of the texture or object the picture shows.</param>
/// <param name="Width">The width of the PNG in pixels.</param>
/// <param name="Height">The height of the PNG in pixels.</param>
/// <param name="Png">The PNG file bytes.</param>
public readonly record struct Thumbnail(int Id, int Width, int Height, ReadOnlyMemory<byte> Png);

/// <summary>
/// Remembers thumbnails by id.
/// </summary>
/// <remarks>
/// The game sends a thumbnail only after being asked, and sends nothing at all when the pixels cannot be read.
/// This store lets the UI ask once and look the answer up later. It is thread safe.
/// </remarks>
public sealed class ThumbnailStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Thumbnail> _items = [];

    /// <summary>Gets how many thumbnails are stored.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>
    /// Looks up a thumbnail.
    /// </summary>
    /// <param name="id">The texture or object id.</param>
    /// <param name="thumbnail">The thumbnail, when found.</param>
    /// <returns><see langword="true"/> if a thumbnail for the id is stored.</returns>
    public bool TryGet(int id, out Thumbnail thumbnail)
    {
        lock (_gate)
        {
            return _items.TryGetValue(id, out thumbnail);
        }
    }

    /// <summary>
    /// Stores a thumbnail, replacing an older one with the same id.
    /// </summary>
    /// <param name="thumbnail">The thumbnail to store.</param>
    public void Set(Thumbnail thumbnail)
    {
        lock (_gate)
        {
            _items[thumbnail.Id] = thumbnail;
        }
    }

    /// <summary>
    /// Forgets every thumbnail whose id is not in the given set.
    /// </summary>
    /// <param name="keep">The ids to keep.</param>
    public void Retain(IReadOnlySet<int> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        lock (_gate)
        {
            List<int>? stale = null;
            foreach (var id in _items.Keys)
            {
                if (!keep.Contains(id))
                {
                    (stale ??= []).Add(id);
                }
            }

            if (stale is not null)
            {
                foreach (var id in stale)
                {
                    _items.Remove(id);
                }
            }
        }
    }

    /// <summary>
    /// Forgets every thumbnail.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }
}
