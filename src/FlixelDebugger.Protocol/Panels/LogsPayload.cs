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
/// A batch of log lines from the <c>logs</c> panel (kind 2).
/// </summary>
/// <param name="Backlog"><see langword="true"/> for the batch sent right after connecting.</param>
/// <param name="Lines">The log lines, oldest first.</param>
public sealed record LogsPayload(bool Backlog, IReadOnlyList<LogLine> Lines) : PanelPayload
{
  /// <summary>
  /// Reads a logs payload.
  /// </summary>
  /// <param name="reader">The reader, positioned at the first field.</param>
  /// <returns>The payload that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static LogsPayload Read(ref MessageReader reader)
  {
    var backlog = reader.ReadBool();
    return new LogsPayload(backlog, reader.ReadArray(LogLine.Read));
  }

  /// <inheritdoc/>
  public override void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteBool(Backlog).WriteArray(Lines, static (w, line) => line.Write(w));
  }
}

/// <summary>
/// One line of the game's log.
/// </summary>
/// <param name="Seq">An increasing sequence number.</param>
/// <param name="Time">Seconds since the game started.</param>
/// <param name="Level">The severity.</param>
/// <param name="Uncaught"><see langword="true"/> for uncaught exceptions and stderr output.</param>
/// <param name="Tag">The log tag.</param>
/// <param name="CallSite">Where the line was logged, for example <c>Main.java:10</c>.</param>
/// <param name="Message">
/// The message. The game cuts it to 2000 characters plus <c> (cut off)</c>.
/// </param>
/// <param name="Stack">The stack trace, which may be empty.</param>
public readonly record struct LogLine(
    long Seq,
    double Time,
    LogLevel Level,
    bool Uncaught,
    string Tag,
    string CallSite,
    string Message,
    string Stack)
{
  /// <summary>
  /// Reads one log line.
  /// </summary>
  /// <param name="reader">The reader, positioned at the start of the line.</param>
  /// <returns>The line that was read.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static LogLine Read(ref MessageReader reader)
  {
    var seq = reader.ReadI64();
    var time = reader.ReadF64();
    var level = (LogLevel)reader.ReadU8();
    var uncaught = reader.ReadBool();
    var tag = reader.ReadString();
    var callSite = reader.ReadString();
    var message = reader.ReadString();
    return new LogLine(seq, time, level, uncaught, tag, callSite, message, reader.ReadString());
  }

  /// <summary>
  /// Writes this line.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public void Write(MessageWriter writer)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.WriteI64(Seq)
        .WriteF64(Time)
        .WriteU8((byte)Level)
        .WriteBool(Uncaught)
        .WriteString(Tag)
        .WriteString(CallSite)
        .WriteString(Message)
        .WriteString(Stack);
  }
}
