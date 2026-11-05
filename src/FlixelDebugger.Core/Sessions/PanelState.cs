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

using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Messages;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.Core.Sessions;

/// <summary>
/// The latest known state of one panel of one game.
/// </summary>
/// <remarks>
/// <para>
/// A panel is identified by its <see cref="Id"/>. The game numbers panels, but the numbers change whenever a panel
/// is registered or removed, so the session keeps one <see cref="PanelState"/> per id and only updates its
/// <see cref="Index"/> when a new panel list arrives.
/// </para>
/// <para>
/// <see cref="Payload"/> is the newest payload of any kind the panel sent. Panels that send several kinds
/// (console, inspector, textures) have their own typed properties on <see cref="GameSession"/>, which keep the
/// latest payload of each kind. All members are safe to read from any thread.
/// </para>
/// </remarks>
public sealed class PanelState
{
  private volatile string _name;
  private volatile PanelPayload? _payload;
  private long _version;
  private int _index;

  internal PanelState(PanelInfo info)
  {
    Id = info.Id;
    Kind = info.Kind;
    _name = info.Name;
    _index = info.Index;
  }

  /// <summary>Gets the stable identifier of the panel, for example <c>stats</c> or <c>custom.player</c>.</summary>
  public string Id { get; }

  /// <summary>Gets the kind of the panel.</summary>
  public PanelKind Kind { get; }

  /// <summary>Gets the display name of the panel.</summary>
  public string Name => _name;

  /// <summary>Gets the panel's current index in the game's panel list, which the wire protocol uses to address it.</summary>
  public int Index => Volatile.Read(ref _index);

  /// <summary>Gets the newest payload the panel sent, or <see langword="null"/> if it has not sent one yet.</summary>
  public PanelPayload? Payload => _payload;

  /// <summary>Gets how many payloads the panel has sent. It works as a version number.</summary>
  public long Version => Interlocked.Read(ref _version);

  /// <summary>Gets the payload as a custom panel payload, or <see langword="null"/> if it is not one.</summary>
  public CustomPayload? Custom => _payload as CustomPayload;

  internal void Update(PanelInfo info)
  {
    _name = info.Name;
    Volatile.Write(ref _index, info.Index);
  }

  internal void SetPayload(PanelPayload payload)
  {
    _payload = payload;
    Interlocked.Increment(ref _version);
  }
}
