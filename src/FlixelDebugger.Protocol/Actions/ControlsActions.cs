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

using A = FlixelDebugger.Protocol.ProtocolConstants.Actions;

namespace FlixelDebugger.Protocol.Actions;

/// <summary>
/// Pauses or resumes the game (controls panel).
/// </summary>
/// <param name="Paused"><see langword="true"/> to pause.</param>
public sealed record ControlsSetPausedAction(bool Paused) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Controls.SetPaused;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ControlsSetPausedAction Read(ref MessageReader reader) => new(reader.ReadBool());

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBool(Paused);
    }
}

/// <summary>
/// Advances the game by one frame while it is paused (controls panel).
/// </summary>
public sealed record ControlsStepAction : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Controls.Step;

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
    }
}

/// <summary>
/// Shows or hides hitboxes (controls panel).
/// </summary>
/// <param name="Hitboxes"><see langword="true"/> to show hitboxes.</param>
public sealed record ControlsSetHitboxesAction(bool Hitboxes) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Controls.SetHitboxes;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ControlsSetHitboxesAction Read(ref MessageReader reader) => new(reader.ReadBool());

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBool(Hitboxes);
    }
}

/// <summary>
/// Turns the free camera on or off (controls panel).
/// </summary>
/// <param name="FreeCam"><see langword="true"/> to turn the free camera on.</param>
public sealed record ControlsSetFreeCamAction(bool FreeCam) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Controls.SetFreeCam;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ControlsSetFreeCamAction Read(ref MessageReader reader) => new(reader.ReadBool());

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBool(FreeCam);
    }
}

/// <summary>
/// Sets the stats sample rate (controls panel).
/// </summary>
/// <param name="Rate">The rate in Hz. The game clamps it to 1 to 30.</param>
public sealed record ControlsSetRateAction(byte Rate) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Controls.SetRate;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ControlsSetRateAction Read(ref MessageReader reader) => new(reader.ReadU8());

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU8(Rate);
    }
}

/// <summary>
/// Sets the log level override (controls panel).
/// </summary>
/// <param name="Level">A log level, or <see cref="LogLevel.Default"/> for no override.</param>
public sealed record ControlsSetLogLevelAction(LogLevel Level) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Controls.SetLogLevel;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ControlsSetLogLevelAction Read(ref MessageReader reader) => new((LogLevel)reader.ReadU8());

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU8((byte)Level);
    }
}
