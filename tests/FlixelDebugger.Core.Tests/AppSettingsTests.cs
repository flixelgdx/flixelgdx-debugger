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

using FlixelDebugger.Core.Settings;

namespace FlixelDebugger.Core.Tests;

public sealed class AppSettingsTests : IDisposable
{
  private readonly string _folder = Path.Combine(
      Path.GetTempPath(), "flixelgdx-debugger-tests-" + Guid.NewGuid().ToString("N"));

  private string FilePath => Path.Combine(_folder, "nested", "settings.json");

  public void Dispose()
  {
    if (Directory.Exists(_folder))
    {
      Directory.Delete(_folder, recursive: true);
    }
  }

  [Fact]
  public void MissingFileGivesTheDefaults()
  {
    var settings = AppSettings.Load(FilePath);
    Assert.Equal(6510, settings.Port);
    Assert.False(settings.ListenOnAllInterfaces);
    Assert.False(settings.McpEnabled);
    Assert.Equal(6511, settings.McpPort);
    Assert.Null(settings.WindowLayout);
    Assert.Empty(settings.PanelVisibility);
    Assert.Null(settings.LoadProblem);
  }

  [Fact]
  public void SaveCreatesTheFolderAndLoadReadsItBack()
  {
    var settings = new AppSettings
    {
      Port = 7000,
      McpEnabled = true,
      McpPort = 7001,
      ListenOnAllInterfaces = true,
      WindowLayout = "layout-v1:{opaque}",
    };
    settings.PanelVisibility["stats"] = false;
    settings.PanelVisibility["custom.player"] = true;

    Assert.True(settings.Save(FilePath));
    var loaded = AppSettings.Load(FilePath);

    Assert.Equal(7000, loaded.Port);
    Assert.True(loaded.McpEnabled);
    Assert.Equal(7001, loaded.McpPort);
    Assert.True(loaded.ListenOnAllInterfaces);
    Assert.Equal("layout-v1:{opaque}", loaded.WindowLayout);
    Assert.False(loaded.PanelVisibility["stats"]);
    Assert.True(loaded.PanelVisibility["custom.player"]);
    Assert.False(File.Exists(FilePath + ".tmp"));
  }

  [Fact]
  public void CorruptFileGivesTheDefaultsAndIsMovedAside()
  {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    File.WriteAllText(FilePath, "{ this is not json");

    var settings = AppSettings.Load(FilePath);

    Assert.Equal(6510, settings.Port);
    Assert.NotNull(settings.LoadProblem);
    Assert.False(File.Exists(FilePath));
    Assert.True(File.Exists(FilePath + ".bad"));
  }

  [Fact]
  public void AnEmptyFileGivesTheDefaults()
  {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    File.WriteAllText(FilePath, "null");

    var settings = AppSettings.Load(FilePath);

    Assert.Equal(6510, settings.Port);
    Assert.NotNull(settings.LoadProblem);
  }

  [Fact]
  public void ANonsensePortIsReplacedByTheDefault()
  {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    File.WriteAllText(FilePath, """{ "port": 99999, "listenOnAllInterfaces": true }""");

    var settings = AppSettings.Load(FilePath);

    Assert.Equal(6510, settings.Port);
    Assert.True(settings.ListenOnAllInterfaces);
  }

  [Fact]
  public void ANonsenseMcpPortIsReplacedByTheDefault()
  {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    File.WriteAllText(FilePath, """{ "mcpPort": 0, "mcpEnabled": true }""");

    var settings = AppSettings.Load(FilePath);

    Assert.Equal(6511, settings.McpPort);
    Assert.True(settings.McpEnabled);
  }

  [Fact]
  public void ANullVisibilityMapBecomesEmpty()
  {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
    File.WriteAllText(FilePath, """{ "panelVisibility": null }""");

    Assert.Empty(AppSettings.Load(FilePath).PanelVisibility);
  }

  [Fact]
  public void SaveReturnsFalseInsteadOfThrowingWhenTheDiskRefuses()
  {
    Directory.CreateDirectory(_folder);
    var blocker = Path.Combine(_folder, "blocker");
    File.WriteAllText(blocker, "a file where a folder should be");

    Assert.False(new AppSettings().Save(Path.Combine(blocker, "settings.json")));
  }

  [Fact]
  public void ServerOptionsFollowTheSettings()
  {
    var options =
        new AppSettings { Port = 7001, ListenOnAllInterfaces = true }.CreateServerOptions();
    Assert.Equal(7001, options.Port);
    Assert.True(options.ListenOnAllInterfaces);
  }

  [Fact]
  public void DefaultPathIsInTheApplicationDataFolder()
  {
    Assert.EndsWith(
        Path.Combine("flixelgdx-debugger", "settings.json"),
        AppSettings.DefaultPath,
        StringComparison.Ordinal);
  }
}
