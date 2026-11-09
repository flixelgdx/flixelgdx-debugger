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
/// Carries a whole new widget tree of one panel.
/// </summary>
/// <param name="panel">The panel.</param>
/// <param name="root">The tree's root widget.</param>
public sealed class PanelTreeEventArgs(UiPanelState panel, WidgetNode root) : EventArgs
{
  /// <summary>Gets the panel.</summary>
  public UiPanelState Panel { get; } = panel;

  /// <summary>Gets the tree's root widget.</summary>
  public WidgetNode Root { get; } = root;
}

/// <summary>
/// Carries the changes of one patch of one panel.
/// </summary>
/// <param name="panel">The panel.</param>
/// <param name="ops">The changes, in order.</param>
public sealed class PanelPatchEventArgs(UiPanelState panel, IReadOnlyList<PatchOp> ops) : EventArgs
{
  /// <summary>Gets the panel.</summary>
  public UiPanelState Panel { get; } = panel;

  /// <summary>Gets the changes, in order.</summary>
  public IReadOnlyList<PatchOp> Ops { get; } = ops;
}

/// <summary>
/// Carries a request of the game to the app, such as "show this panel".
/// </summary>
/// <param name="request">The request.</param>
public sealed class AppRequestEventArgs(AppRequest request) : EventArgs
{
  /// <summary>Gets the request.</summary>
  public AppRequest Request { get; } = request;
}
