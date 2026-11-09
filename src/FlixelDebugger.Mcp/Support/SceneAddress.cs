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
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Mcp.Support;

/// <summary>
/// Works out scene paths and resolves the <c>target</c> argument of tools.
/// </summary>
/// <remarks>
/// <para>
/// The game does not send paths in the scene tree. A path is the labels from the root down joined
/// with dots (<c>state.3.0</c>), so it is computed here from the tree snapshot. A target is either
/// the numeric object id (stable) or a scene path (a snapshot, because member indexes shift when
/// members are added or removed). A path always starts with a word, never a digit, so the two
/// cannot be confused.
/// </para>
/// </remarks>
public static class SceneAddress
{
  /// <summary>
  /// Builds the path of every node of a tree, keyed by object id.
  /// </summary>
  /// <param name="tree">The tree snapshot.</param>
  /// <returns>The path of each node.</returns>
  public static Dictionary<int, string> BuildPaths(SceneTree tree)
  {
    ArgumentNullException.ThrowIfNull(tree);
    var paths = new Dictionary<int, string>(tree.Nodes.Count);
    foreach (var node in tree.Nodes)
    {
      // Parents always appear before their children, so the parent's path is already known.
      paths[node.Id] = tree.TryGetParent(node.Id, out var parent) &&
          paths.TryGetValue(parent.Id, out var parentPath)
          ? parentPath + "." + node.Label
          : node.Label;
    }

    return paths;
  }

  /// <summary>
  /// Tells whether a target is a numeric object id.
  /// </summary>
  /// <param name="target">The target text.</param>
  /// <param name="id">The id, when the target is numeric.</param>
  /// <returns><see langword="true"/> if the target is an object id.</returns>
  public static bool TryParseId(string? target, out int id)
  {
    id = 0;
    return target is not null &&
        int.TryParse(
            target.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out id);
  }

  /// <summary>
  /// Finds the object id a target points to, using the scene tree when the target is a path.
  /// </summary>
  /// <param name="tree">The latest tree snapshot of the game.</param>
  /// <param name="target">An object id or a scene path.</param>
  /// <param name="id">The object id, when found.</param>
  /// <returns><see langword="true"/> if the target resolves to a known object.</returns>
  public static bool TryResolveId(SceneTree tree, string? target, out int id)
  {
    ArgumentNullException.ThrowIfNull(tree);
    if (TryParseId(target, out id))
    {
      return true;
    }

    var path = target?.Trim();
    if (!string.IsNullOrEmpty(path))
    {
      foreach (var (nodeId, nodePath) in BuildPaths(tree))
      {
        if (string.Equals(nodePath, path, StringComparison.Ordinal))
        {
          id = nodeId;
          return true;
        }
      }
    }

    id = 0;
    return false;
  }
}
