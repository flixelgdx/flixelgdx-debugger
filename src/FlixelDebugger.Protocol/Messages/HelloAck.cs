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

namespace FlixelDebugger.Protocol.Messages;

/// <summary>
/// The app's answer to <see cref="Hello"/>.
/// </summary>
/// <remarks>
/// The app closes the connection when the major versions differ. Minors may differ freely.
/// </remarks>
/// <param name="ProtocolMajor">The protocol major version the app speaks.</param>
/// <param name="ProtocolMinor">The protocol minor version the app speaks.</param>
public sealed record HelloAck(ushort ProtocolMajor, ushort ProtocolMinor) : ProtocolMessage
{
    /// <inheritdoc/>
    public override MessageType Type => MessageType.HelloAck;

    /// <summary>
    /// Reads the fields of a HELLO_ACK message.
    /// </summary>
    /// <param name="reader">The reader, positioned just after the type byte.</param>
    /// <returns>The message that was read.</returns>
    /// <exception cref="ProtocolException">The message is too short.</exception>
    public static HelloAck Read(ref MessageReader reader)
    {
        var major = reader.ReadU16();
        return new HelloAck(major, reader.ReadU16());
    }

    /// <inheritdoc/>
    public override void Write(MessageWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.BeginMessage(MessageType.HelloAck)
            .WriteU16(ProtocolMajor)
            .WriteU16(ProtocolMinor);
    }
}
