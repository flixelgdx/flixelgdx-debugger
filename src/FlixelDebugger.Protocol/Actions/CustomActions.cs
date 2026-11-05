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
/// Presses a button on a custom panel.
/// </summary>
/// <param name="ActionIndex">The action index taken from the button.</param>
public sealed record CustomPressAction(ushort ActionIndex) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Custom.Press;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static CustomPressAction Read(ref MessageReader reader) => new(reader.ReadU16());

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU16(ActionIndex);
    }
}

/// <summary>
/// Edits one property row on a custom panel.
/// </summary>
/// <param name="PropertyIndex">The position of the row in the payload's rows array.</param>
/// <param name="Value">The new value. The app never sends <see cref="ValueKind.Error"/>.</param>
public sealed record CustomSetPropertyAction(ushort PropertyIndex, PropertyValue Value) : ActionPayload
{
    /// <inheritdoc/>
    public override byte Code => A.Custom.SetProperty;

    /// <summary>
    /// Reads the fields of this action.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the action code.</param>
    /// <returns>The action that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static CustomSetPropertyAction Read(ref MessageReader reader)
    {
        var index = reader.ReadU16();
        return new CustomSetPropertyAction(index, PropertyValue.Read(ref reader));
    }

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU16(PropertyIndex);
        Value.Write(writer);
    }
}
