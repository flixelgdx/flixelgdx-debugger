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

using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// What the app knows about one panel the game built: its entry in the panel list and the current
/// state of its widgets, with every patch applied.
/// </summary>
/// <remarks>
/// <para>
/// The game sends a panel as a whole tree once, then only small patches. This class keeps the
/// tree's
/// shape and a current property table per widget, so anyone who arrives late (a view model that was
/// just created, or a tool that reads the panel) can ask for an up-to-date
/// <see cref="Snapshot"/>. Think of it as a whiteboard copy of the game's panel: the game phones in
/// each change and this class erases and rewrites just that spot.
/// </para>
/// <para>
/// Trees and patches arrive on the network thread while readers may be on any thread, so every
/// access takes a short lock. A snapshot is a new immutable tree; nothing it returns changes later.
/// </para>
/// </remarks>
public sealed class UiPanelState
{
  private readonly Lock _gate = new();
  private readonly Dictionary<int, Dictionary<WidgetProp, UiValue>> _props = [];
  private readonly HashSet<string> _openOwners = new(StringComparer.Ordinal);
  private WidgetNode? _tree;
  private volatile string _title;
  private long _version;
  private int _index;
  private int _dock;
  private volatile bool _shown;

  /// <summary>
  /// Creates the state of a panel from its entry in a panel list. A session does this by itself;
  /// tests and tools that replay recorded messages can do it too.
  /// </summary>
  /// <param name="info">The panel's entry.</param>
  public UiPanelState(PanelInfo info)
  {
    Id = info.Id;
    _title = info.Title;
    _index = info.Index;
    _dock = (int)info.Dock;
    _shown = info.Shown;
  }

  /// <summary>Gets the stable id of the panel.</summary>
  public string Id { get; }

  /// <summary>Gets the name on the panel's tab.</summary>
  public string Title => _title;

  /// <summary>Gets the panel's index in the latest panel list.</summary>
  public int Index => Volatile.Read(ref _index);

  /// <summary>Gets where the panel goes the first time the app sees its id.</summary>
  public PanelDock Dock => (PanelDock)Volatile.Read(ref _dock);

  /// <summary>Gets whether the panel is shown the first time the app sees its id.</summary>
  public bool Shown => _shown;

  /// <summary>Gets a number that goes up with every tree and every patch.</summary>
  public long Version => Interlocked.Read(ref _version);

  /// <summary>Gets whether a tree arrived since the panel was last opened.</summary>
  public bool HasTree
  {
    get
    {
      lock (_gate)
      {
        return _tree is not null;
      }
    }
  }

  /// <summary>
  /// Builds the current widget tree, with every patch applied.
  /// </summary>
  /// <returns>The tree, or <see langword="null"/> before the first tree arrived.</returns>
  public WidgetNode? Snapshot()
  {
    lock (_gate)
    {
      return _tree is null ? null : Build(_tree);
    }
  }

  /// <summary>
  /// Returns one property of one widget as it is now.
  /// </summary>
  /// <param name="widget">The widget id.</param>
  /// <param name="key">The property.</param>
  /// <returns>
  /// The value, or <see langword="null"/> when the widget or property is unknown.
  /// </returns>
  public UiValue? Get(int widget, WidgetProp key)
  {
    lock (_gate)
    {
      return _props.TryGetValue(widget, out var props) && props.TryGetValue(key, out var value)
          ? value
          : null;
    }
  }

  internal bool IsWanted
  {
    get
    {
      lock (_gate)
      {
        return _openOwners.Count > 0;
      }
    }
  }

  /// <summary>Takes the title, index, dock, and shown flag from a newer panel list entry.</summary>
  /// <param name="info">The panel's newer entry. Its id must match.</param>
  public void Update(PanelInfo info)
  {
    _title = info.Title;
    _shown = info.Shown;
    Volatile.Write(ref _index, info.Index);
    Volatile.Write(ref _dock, (int)info.Dock);
  }

  // Returns whether the panel changed from closed to open or back.
  internal bool SetOpen(string owner, bool open)
  {
    lock (_gate)
    {
      var before = _openOwners.Count > 0;
      if (open)
      {
        _openOwners.Add(owner);
      }
      else
      {
        _openOwners.Remove(owner);
      }

      return before != _openOwners.Count > 0;
    }
  }

  /// <summary>
  /// Forgets the widgets. The game sends a whole new tree the next time the panel opens.
  /// </summary>
  public void ClearTree()
  {
    lock (_gate)
    {
      _tree = null;
      _props.Clear();
    }

    Interlocked.Increment(ref _version);
  }

  /// <summary>Replaces every widget with a whole new tree.</summary>
  /// <param name="root">The root widget the game sent.</param>
  public void ApplyTree(WidgetNode root)
  {
    ArgumentNullException.ThrowIfNull(root);
    lock (_gate)
    {
      _tree = root;
      _props.Clear();
      foreach (var node in root.Walk())
      {
        _props[node.Id] = new Dictionary<WidgetProp, UiValue>(node.Props);
      }
    }

    Interlocked.Increment(ref _version);
  }

  /// <summary>
  /// Applies changes to the widgets. Changes for widgets that are not in the tree are skipped.
  /// </summary>
  /// <param name="ops">The changes, in order.</param>
  public void ApplyPatch(IReadOnlyList<PatchOp> ops)
  {
    ArgumentNullException.ThrowIfNull(ops);
    lock (_gate)
    {
      foreach (var op in ops)
      {
        if (!_props.TryGetValue(op.Widget, out var props))
        {
          continue;
        }

        if (op.Code == PatchCode.Set)
        {
          props[op.Key] = op.Value;
        }
        else if (op.Code == PatchCode.Append)
        {
          var type = FindType(op.Widget);
          props.TryGetValue(op.Key, out var old);
          props[op.Key] = UiPatch.Append(old, op.Value, UiPatch.LimitOf(type, props));
        }
      }
    }

    Interlocked.Increment(ref _version);
  }

  private WidgetType FindType(int id)
  {
    foreach (var node in _tree!.Walk())
    {
      if (node.Id == id)
      {
        return node.Type;
      }
    }

    return 0;
  }

  private WidgetNode Build(WidgetNode node)
  {
    var children = new WidgetNode[node.Children.Count];
    for (var i = 0; i < children.Length; i++)
    {
      children[i] = Build(node.Children[i]);
    }

    var props = _props.TryGetValue(node.Id, out var current)
        ? new Dictionary<WidgetProp, UiValue>(current)
        : new Dictionary<WidgetProp, UiValue>(node.Props);
    return node with { Props = props, Children = children };
  }
}
