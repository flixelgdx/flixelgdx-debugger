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

namespace FlixelDebugger.Core.Completion;

/// <summary>
/// Something that can run a console line in a game, which keeps tools independent of a real
/// connection.
/// </summary>
/// <remarks>
/// <see cref="Sessions.GameSession"/> implements this interface. Tests can pass a tiny fake
/// instead.
/// </remarks>
public interface ICommandRunner
{
  /// <summary>
  /// Sends a console line to the game to be run.
  /// </summary>
  /// <param name="line">The line to run, without the <c>&gt; </c> echo prefix.</param>
  /// <param name="cancellationToken">A token that cancels the send.</param>
  /// <returns>
  /// <see langword="true"/> if the line was sent; <see langword="false"/> if there is no connection
  /// to send it on.
  /// </returns>
  public Task<bool> RunCommandAsync(string line, CancellationToken cancellationToken = default);
}
