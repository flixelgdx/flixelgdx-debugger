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

using FlixelDebugger.App.ViewModels;

namespace FlixelDebugger.App.Tests;

public class SettingsViewModelTests
{
  [Theory]
  [InlineData("6510", true, 6510)]
  [InlineData("1", true, 1)]
  [InlineData("65535", true, 65535)]
  [InlineData("0", false, 0)]
  [InlineData("65536", false, 0)]
  [InlineData("abc", false, 0)]
  [InlineData("", false, 0)]
  [InlineData("-5", false, 0)]
  public void OnlyValidPortsCanBeSaved(string text, bool valid, int port)
  {
    var settings = new SettingsViewModel(6510, false) { PortText = text };

    Assert.Equal(valid, settings.SaveCommand.CanExecute(null));
    Assert.Equal(!valid, settings.HasError);
    Assert.Equal(port, settings.Port);
  }

  [Fact]
  public void SaveAndCancelRequestTheMatchingClose()
  {
    var settings = new SettingsViewModel(6510, true);
    var results = new List<bool>();
    settings.CloseRequested += (_, accepted) => results.Add(accepted);

    settings.SaveCommand.Execute(null);
    settings.CancelCommand.Execute(null);

    Assert.Equal([true, false], results);
    Assert.True(settings.ListenOnAllInterfaces);
    Assert.Contains("Wi-Fi", SettingsViewModel.AllInterfacesNote);
  }
}
