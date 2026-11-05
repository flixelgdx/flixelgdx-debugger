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

namespace FlixelDebugger.Protocol.Panels;

/// <summary>
/// The state of the <c>controls</c> panel (kind 6), sent on connect and whenever it changes.
/// </summary>
/// <param name="Paused">Whether the game is paused.</param>
/// <param name="Hitboxes">Whether hitboxes are shown.</param>
/// <param name="FreeCam">Whether the free camera is on.</param>
/// <param name="Rate">The stats sample rate in Hz, from 1 to 30.</param>
/// <param name="LogLevel">
/// The log level override, or <see cref="LogLevel.Default"/> for the game's own setting.
/// </param>
/// <param name="Keybinds">The key bindings the game wants to show.</param>
public sealed record ControlsPayload(
    bool Paused,
    bool Hitboxes,
    bool FreeCam,
    byte Rate,
    LogLevel LogLevel,
    IReadOnlyList<Keybind> Keybinds) : PanelPayload
{
  /// <summary>
  /// Reads a controls payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ControlsPayload Read(ref MessageReader reader)
  {
    var paused = reader.ReadBool();
    var hitboxes = reader.ReadBool();
    var freeCam = reader.ReadBool();
    var rate = reader.ReadU8();
    var logLevel = (LogLevel)reader.ReadU8();
    return new ControlsPayload(
        paused, hitboxes, freeCam, rate, logLevel, reader.ReadArray(Keybind.Read));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteBool(Paused)
        .WriteBool(Hitboxes)
        .WriteBool(FreeCam)
        .WriteU8(Rate)
        .WriteU8((byte)LogLevel)
        .WriteArray(Keybinds, static (w, keybind) => keybind.Write(w));
  }
}

/// <summary>
/// A key binding shown in the controls panel.
/// </summary>
/// <param name="Key">The key name, for example <c>F1</c>.</param>
/// <param name="Description">What the key does.</param>
public readonly record struct Keybind(string Key, string Description)
{
  /// <summary>
  /// Reads one key binding.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the binding.</param>
  /// <returns>The binding that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static Keybind Read(ref MessageReader reader)
  {
    var key = reader.ReadString();
    return new Keybind(key, reader.ReadString());
  }

  /// <summary>
  /// Writes this binding.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteString(Key).WriteString(Description);
  }
}
