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

namespace FlixelDebugger.Protocol.Tests;

/// <summary>
/// Helpers that load the fixture files and decode them the way the app would.
/// </summary>
internal static class Fixture
{
  public const string Unicode = "héllo 世界 \U0001F600";

  public static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

  public static byte[] Load(string name) =>
      File.ReadAllBytes(Path.Combine(Directory, name + ".bin"));

  public static IEnumerable<string> AllNames() =>
      System.IO.Directory.GetFiles(Directory, "*.bin")
          .Select(path => Path.GetFileNameWithoutExtension(path))
          .Order(StringComparer.Ordinal);

  public static TheoryData<string> AllNamesData()
  {
    var data = new TheoryData<string>();
    foreach (var name in AllNames())
    {
      data.Add(name);
    }

    return data;
  }

  /// <summary>
  /// Creates a decoder that already knows the panel indices listed in the fixtures README.
  /// </summary>
  public static MessageDecoder NewDecoder()
  {
    var decoder = new MessageDecoder();
    Assert.IsType<PanelList>(decoder.Decode(Load("msg_panel_list")));
    return decoder;
  }

  /// <summary>
  /// Decodes a whole-message fixture as the expected type and checks it re-encodes to the same
  /// bytes.
  /// </summary>
  public static T Message<T>(string name)
      where T : ProtocolMessage
  {
    var bytes = Load(name);
    var message = Assert.IsType<T>(NewDecoder().Decode(bytes));
    Assert.Equal(bytes, message.ToArray());
    return message;
  }

  /// <summary>
  /// Decodes a PANEL_DATA fixture, checks the panel index and payload type, and checks the
  /// re-encode.
  /// </summary>
  public static T Payload<T>(string name, ushort panel)
      where T : Panels.PanelPayload
  {
    var data = Message<PanelData>(name);
    Assert.Equal(panel, data.Panel);
    return Assert.IsType<T>(data.Payload);
  }

  /// <summary>
  /// Decodes a PANEL_ACTION fixture, checks the panel index, action code and type, and checks the
  /// re-encode.
  /// </summary>
  public static T Action<T>(string name, ushort panel, byte code)
      where T : Actions.ActionPayload
  {
    var action = Message<PanelAction>(name);
    Assert.Equal(panel, action.Panel);
    Assert.Equal(code, action.Action.Code);
    return Assert.IsType<T>(action.Action);
  }

  /// <summary>
  /// Reads a value fixture and checks it re-encodes to the same bytes.
  /// </summary>
  public static PropertyValue Value(string name)
  {
    var bytes = Load(name);
    var reader = new MessageReader(bytes);
    var value = PropertyValue.Read(ref reader);
    Assert.Equal(0, reader.Remaining);

    var writer = new MessageWriter();
    writer.WriteValue(value);
    Assert.Equal(bytes, writer.Finish().ToArray());
    return value;
  }
}
