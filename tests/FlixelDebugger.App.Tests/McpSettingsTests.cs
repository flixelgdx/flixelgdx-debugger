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

using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.App.ViewModels;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Protocol;
using FlixelDebugger.Protocol.Messages;

namespace FlixelDebugger.App.Tests;

public class McpSettingsTests
{
  [Fact]
  public async Task McpIsOffByDefaultAndNothingStarts()
  {
    var h = new Harness();

    await h.Main.InitializeAsync();

    Assert.Empty(h.Mcp.Calls);
    Assert.Equal(string.Empty, h.Main.McpText);
  }

  [Fact]
  public async Task McpStartsWhenTheSettingIsOnAndTheStatusShowsTheAddress()
  {
    var h = new Harness();
    h.Settings.Current.McpEnabled = true;
    h.Settings.Current.McpPort = 6600;

    await h.Main.InitializeAsync();

    Assert.Equal(["start:6600"], h.Mcp.Calls);
    Assert.Equal("MCP: 127.0.0.1:6600", h.Main.McpText);
  }

  [Fact]
  public async Task AUsedMcpPortIsReportedWithoutCrashing()
  {
    var h = new Harness();
    h.Settings.Current.McpEnabled = true;
    h.Mcp.StartFailure = new PortInUseException(6511, new InvalidOperationException());

    await h.Main.InitializeAsync();

    Assert.False(h.Mcp.IsRunning);
    Assert.Contains("MCP port 6511 is already in use", h.Notifier.Messages[^1].Message);
    Assert.Equal("MCP: port 6511 unavailable", h.Main.McpText);
  }

  [Fact]
  public async Task TurningMcpOnAndOffStartsAndStopsOnlyTheMcpServer()
  {
    var h = new Harness();
    await h.Main.InitializeAsync();
    h.Host.Calls.Clear();

    await h.Main.ApplySettingsAsync(6510, false, true, 6511);

    Assert.Empty(h.Host.Calls);
    Assert.Equal(["stop", "start:6511"], h.Mcp.Calls);
    Assert.True(h.Settings.Current.McpEnabled);

    h.Mcp.Calls.Clear();
    await h.Main.ApplySettingsAsync(6510, false, false, 6511);

    Assert.Equal(["stop"], h.Mcp.Calls);
    Assert.Equal(string.Empty, h.Main.McpText);
  }

  [Fact]
  public async Task ChangingTheMcpPortRestartsMcpButUnchangedSettingsDoNot()
  {
    var h = new Harness();
    h.Settings.Current.McpEnabled = true;
    await h.Main.InitializeAsync();
    h.Mcp.Calls.Clear();

    await h.Main.ApplySettingsAsync(6510, false, true, 6511);
    Assert.Empty(h.Mcp.Calls);

    await h.Main.ApplySettingsAsync(6510, false, true, 6700);
    Assert.Equal(["stop", "start:6700"], h.Mcp.Calls);
    Assert.Equal(6700, h.Settings.Current.McpPort);
  }

  [Fact]
  public async Task TheTwoArgumentOverloadKeepsTheMcpSettings()
  {
    var h = new Harness();
    h.Settings.Current.McpEnabled = true;
    await h.Main.InitializeAsync();
    h.Mcp.Calls.Clear();

    await h.Main.ApplySettingsAsync(7001, false);

    Assert.Empty(h.Mcp.Calls);
    Assert.True(h.Settings.Current.McpEnabled);
  }

  [Fact]
  public async Task TheSettingsDialogCarriesTheMcpOptions()
  {
    var h = new Harness();
    await h.Main.InitializeAsync();
    h.Dialogs.SettingsResult = true;
    h.Dialogs.EditSettings = s =>
    {
      s.McpEnabled = true;
      s.McpPortText = "6800";
    };

    await h.Main.OpenSettingsCommand.ExecuteAsync(null);

    Assert.True(h.Settings.Current.McpEnabled);
    Assert.Equal(6800, h.Settings.Current.McpPort);
    Assert.Equal(["stop", "start:6800"], h.Mcp.Calls);
  }

  [Fact]
  public async Task ShuttingDownStopsMcp()
  {
    var h = new Harness();
    h.Settings.Current.McpEnabled = true;
    await h.Main.InitializeAsync();

    await h.Main.ShutdownAsync();

    Assert.False(h.Mcp.IsRunning);
  }

  [Fact]
  public void TheGameAnAgentDrivesIsMarkedInTheList()
  {
    var h = new Harness();
    var main = h.Main;
    var one = h.Connect("One");
    var two = h.Connect("Two");

    h.Mcp.Raise("inspect", two.Id);

    Assert.False(main.Sessions[0].IsAgentDriven);
    Assert.True(main.Sessions[1].IsAgentDriven);
    Assert.True(main.Agent.IsActive);
    Assert.NotEqual(one.Id, two.Id);

    h.Clock.Advance(TimeSpan.FromSeconds(4));
    h.Scheduler.Tick();

    Assert.False(main.Sessions[1].IsAgentDriven);
    Assert.False(main.Agent.IsActive);
  }

  [Fact]
  public void ACallThatNamesNoGameMarksTheOnlyGame()
  {
    var h = new Harness();
    var main = h.Main;
    h.Connect("One");

    h.Mcp.Raise("get_stats");

    Assert.True(main.Sessions[0].IsAgentDriven);
  }

  [Fact]
  public void SettingsExplainTheMcpOptionAndBuildTheConnectionTexts()
  {
    var settings = new SettingsViewModel(6510, false, true, 6600);

    Assert.Equal("http://127.0.0.1:6600/mcp", settings.McpUrl);
    Assert.Equal(
        "claude mcp add --transport http flixelgdx http://127.0.0.1:6600/mcp",
        settings.ClaudeCodeCommand);
    Assert.Contains("\"url\": \"http://127.0.0.1:6600/mcp\"", settings.CursorJson);
    Assert.Contains("\"mcpServers\"", settings.CursorJson);
    Assert.Equal(
        "[mcp_servers.flixelgdx]\nurl = \"http://127.0.0.1:6600/mcp\"", settings.CodexToml);

    settings.McpPortText = "7000";

    Assert.Contains("7000", settings.ClaudeCodeCommand);
    Assert.Contains("7000", settings.CodexToml);
  }

  [Theory]
  [InlineData("6511", "6510", true)]
  [InlineData("6510", "6510", false)]
  [InlineData("0", "6510", false)]
  [InlineData("abc", "6510", false)]
  [InlineData("65536", "6510", false)]
  public void TheMcpPortMustBeValidAndDifferFromTheGamePort(string mcp, string game, bool ok)
  {
    var settings = new SettingsViewModel(6510, false) { PortText = game, McpPortText = mcp };

    Assert.Equal(ok, settings.SaveCommand.CanExecute(null));
    Assert.Equal(!ok, settings.HasMcpError);
  }

  [Fact]
  public void CopyButtonsAskTheWindowToPutTextOnTheClipboard()
  {
    var settings = new SettingsViewModel(6510, false, true);
    var copied = new List<string>();
    settings.CopyRequested += (_, text) => copied.Add(text);

    settings.CopyCommand.Execute(settings.ClaudeCodeCommand);
    settings.CopyCommand.Execute(string.Empty);

    Assert.Equal([settings.ClaudeCodeCommand], copied);
  }
}
