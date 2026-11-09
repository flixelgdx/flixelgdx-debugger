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
/// A full tree snapshot from the <c>scene</c> feed (kind 8).
/// </summary>
/// <remarks>
/// Trees larger than 65535 nodes are cut by the game. Parents always appear before their children.
/// </remarks>
/// <param name="Nodes">The nodes of the tree, parents first.</param>
public sealed record ScenePayload(IReadOnlyList<SceneNode> Nodes) : FeedPayload
{
  /// <summary>
  /// Reads a scene payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ScenePayload Read(ref MessageReader reader) =>
      new(reader.ReadArray(SceneNode.Read));

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteArray(Nodes, static (w, node) => node.Write(w));
  }
}

/// <summary>
/// One node of the scene tree.
/// </summary>
/// <param name="Id">The node id.</param>
/// <param name="Parent">The parent node id, or -1 for a root.</param>
/// <param name="Type">The type name.</param>
/// <param name="Label">The display label.</param>
/// <param name="TrackedRoot">
/// <see langword="true"/> for objects added through the inspector's <c>track()</c>.
/// </param>
public readonly record struct SceneNode(
    int Id, int Parent, string Type, string Label, bool TrackedRoot)
{
  /// <summary>
  /// Reads one node.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the node.</param>
  /// <returns>The node that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static SceneNode Read(ref MessageReader reader)
  {
    var id = reader.ReadI32();
    var parent = reader.ReadI32();
    var type = reader.ReadString();
    var label = reader.ReadString();
    return new SceneNode(id, parent, type, label, reader.ReadBool());
  }

  /// <summary>
  /// Writes this node.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI32(Id).WriteI32(Parent).WriteString(Type).WriteString(Label)
        .WriteBool(TrackedRoot);
  }
}
