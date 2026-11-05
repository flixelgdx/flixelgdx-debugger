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

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// A snapshot of the game's scene tree with fast parent and child lookup.
/// </summary>
/// <remarks>
/// <para>
/// The game sends the tree as a flat list where every node names its parent. This class indexes that list once,
/// so asking "who are the children of node 7?" does not need a scan. A snapshot never changes after it is built,
/// so it is safe to share between threads; a new payload produces a new <see cref="SceneTree"/>.
/// </para>
/// <para>
/// A node whose parent is <c>-1</c> (or whose parent is not in the list) is a root.
/// </para>
/// </remarks>
public sealed class SceneTree
{
  private static readonly SceneNode[] _none = [];

  private readonly Dictionary<int, SceneNode> _byId;
  private readonly Dictionary<int, List<SceneNode>> _children;
  private readonly List<SceneNode> _roots = [];

  /// <summary>
  /// Builds the index for a list of nodes.
  /// </summary>
  /// <param name="nodes">The nodes, in the order the game sent them.</param>
  public SceneTree(IReadOnlyList<SceneNode> nodes)
  {
    ArgumentNullException.ThrowIfNull(nodes);
    Nodes = nodes;
    _byId = new Dictionary<int, SceneNode>(nodes.Count);
    _children = [];
    foreach (var node in nodes)
    {
      _byId[node.Id] = node;
    }

    foreach (var node in nodes)
    {
      if (node.Parent >= 0 && node.Parent != node.Id && _byId.ContainsKey(node.Parent))
      {
        if (!_children.TryGetValue(node.Parent, out var list))
        {
          list = [];
          _children[node.Parent] = list;
        }

        list.Add(node);
      }
      else
      {
        _roots.Add(node);
      }
    }
  }

  /// <summary>Gets an empty tree.</summary>
  public static SceneTree Empty { get; } = new([]);

  /// <summary>Gets every node, in the order the game sent them (parents before children).</summary>
  public IReadOnlyList<SceneNode> Nodes { get; }

  /// <summary>Gets the nodes without a parent.</summary>
  public IReadOnlyList<SceneNode> Roots => _roots;

  /// <summary>
  /// Looks up a node by id.
  /// </summary>
  /// <param name="id">The node id.</param>
  /// <param name="node">The node, when found.</param>
  /// <returns><see langword="true"/> if the tree has a node with that id.</returns>
  public bool TryGetNode(int id, out SceneNode node) => _byId.TryGetValue(id, out node);

  /// <summary>
  /// Gets the children of a node, in the order the game sent them.
  /// </summary>
  /// <param name="id">The id of the parent node.</param>
  /// <returns>The children, or an empty list if the node has none or does not exist.</returns>
  public IReadOnlyList<SceneNode> GetChildren(int id) => _children.TryGetValue(id, out var list) ? list : _none;

  /// <summary>
  /// Gets the parent of a node.
  /// </summary>
  /// <param name="id">The id of the child node.</param>
  /// <param name="parent">The parent, when the node has one.</param>
  /// <returns><see langword="true"/> if the node exists and is not a root.</returns>
  public bool TryGetParent(int id, out SceneNode parent)
  {
    if (_byId.TryGetValue(id, out var node) && node.Parent >= 0 && _byId.TryGetValue(node.Parent, out parent))
    {
      return true;
    }

    parent = default;
    return false;
  }
}
