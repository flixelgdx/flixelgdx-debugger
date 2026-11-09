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
using FlixelDebugger.Protocol.Feeds;

namespace FlixelDebugger.Core.Tests;

internal static class TestData
{
  public static StatsPayload Stats(float fps, bool paused = false) => new(
      fps, 16.6f, 5f, 1f, 100, 2000, 6000, 3, 2, 4, 64f, 32f, 16f, 8f, 250, 1.5f, 2.5f, paused,
      12.5, 10f, 20f, 1.5f);

  public static LogLine Log(
      long seq,
      string message = "hello",
      LogLevel level = LogLevel.Info,
      string tag = "Game",
      bool uncaught = false,
      double time = 1.5,
      string callSite = "Main.java:10",
      string stack = "") => new(seq, time, level, uncaught, tag, callSite, message, stack);

  public static LogsPayload Logs(bool backlog, params LogLine[] lines) => new(backlog, lines);
}
