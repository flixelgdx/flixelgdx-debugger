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

using Subtypes = FlixelDebugger.Protocol.ProtocolConstants.Subtypes;

namespace FlixelDebugger.Protocol.Panels;

/// <summary>
/// The list of console commands from the <c>console</c> panel (kind 7, subtype 1).
/// </summary>
/// <remarks>
/// Sent on connect and whenever the set of commands changes. Command output itself arrives through
/// the logs panel.
/// </remarks>
/// <param name="Commands">The available commands.</param>
public sealed record ConsoleCommandsPayload(IReadOnlyList<ConsoleCommand> Commands) : PanelPayload
{
    /// <summary>
    /// Reads a console commands payload.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the subtype byte.</param>
    /// <returns>The payload that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ConsoleCommandsPayload Read(ref MessageReader reader) => new(reader.ReadArray(ConsoleCommand.Read));

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU8(Subtypes.Console.Commands)
            .WriteArray(Commands, static (w, command) => command.Write(w));
    }
}

/// <summary>
/// One console command.
/// </summary>
/// <param name="Name">The command name.</param>
/// <param name="Usage">How to call it, for example <c>quit [code]</c>.</param>
/// <param name="Description">A short description.</param>
public readonly record struct ConsoleCommand(string Name, string Usage, string Description)
{
    /// <summary>
    /// Reads one command.
    /// </summary>
    /// <param name="reader">The reader, positioned at the start of the command.</param>
    /// <returns>The command that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ConsoleCommand Read(ref MessageReader reader)
    {
        var name = reader.ReadString();
        var usage = reader.ReadString();
        return new ConsoleCommand(name, usage, reader.ReadString());
    }

    /// <summary>
    /// Writes this command.
    /// </summary>
    /// <param name="writer">The writer to append to.</param>
    public void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString(Name).WriteString(Usage).WriteString(Description);
    }
}

/// <summary>
/// The result of a console command from the <c>console</c> panel (kind 7, subtype 2).
/// </summary>
/// <param name="Status">Whether the command ran, failed, or was not found.</param>
/// <param name="Message">A message about the result.</param>
public sealed record ConsoleResultPayload(ConsoleStatus Status, string Message) : PanelPayload
{
    /// <summary>
    /// Reads a console result payload.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the subtype byte.</param>
    /// <returns>The payload that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static ConsoleResultPayload Read(ref MessageReader reader)
    {
        var status = (ConsoleStatus)reader.ReadU8();
        return new ConsoleResultPayload(status, reader.ReadString());
    }

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteU8(Subtypes.Console.Result).WriteU8((byte)Status).WriteString(Message);
    }
}
