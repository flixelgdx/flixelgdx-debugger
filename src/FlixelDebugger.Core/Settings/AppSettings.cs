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

using System.Text.Json;
using System.Text.Json.Serialization;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Protocol;

namespace FlixelDebugger.Core.Settings;

/// <summary>
/// The debugger's small settings file: the port, the network scope, the window layout, and which
/// panels are visible.
/// </summary>
/// <remarks>
/// <para>
/// The file is JSON at <c>flixelgdx-debugger/settings.json</c> inside the per-user application data
/// folder (<see cref="Environment.SpecialFolder.ApplicationData"/>). Settings are a convenience,
/// never a requirement, so loading and saving never throw: a missing or corrupt file gives the
/// defaults, and a failed save returns <see langword="false"/>. A corrupt file is moved aside to
/// <c>settings.json.bad</c> so the next save does not destroy evidence of what went wrong.
/// </para>
/// <para>
/// The window layout is an opaque string. The UI fills it (for example with a serialized dock
/// layout) and Core stores it without looking inside. Panel visibility is keyed by panel <i>id</i>,
/// because indices change between connections but ids do not.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var settings = AppSettings.Load();
/// settings.PanelVisibility["stats"] = false;
/// settings.Save();
/// </code>
/// </example>
public sealed class AppSettings
{
  /// <summary>The name of the settings folder inside the application data folder.</summary>
  public const string FolderName = "flixelgdx-debugger";

  /// <summary>The name of the settings file.</summary>
  public const string FileName = "settings.json";

  /// <summary>Gets or sets the port the server listens on.</summary>
  public int Port { get; set; } = ProtocolConstants.DefaultPort;

  /// <summary>
  /// Gets or sets whether the server listens on all network interfaces, for debugging over Wi-Fi,
  /// instead of only on this PC.
  /// </summary>
  public bool ListenOnAllInterfaces { get; set; }

  /// <summary>
  /// Gets or sets the last window layout. It is an opaque string that only the UI understands.
  /// </summary>
  public string? WindowLayout { get; set; }

  /// <summary>
  /// Gets or sets whether each panel is visible, by panel id. A panel that is not listed uses its
  /// default.
  /// </summary>
  public Dictionary<string, bool> PanelVisibility { get; set; } = [];

  /// <summary>
  /// Gets a message describing why the file could not be read, or <see langword="null"/> if loading
  /// worked.
  /// </summary>
  [JsonIgnore]
  public string? LoadProblem { get; private set; }

  /// <summary>Gets the default location of the settings file.</summary>
  public static string DefaultPath => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName, FileName);

  /// <summary>
  /// Reads the settings file.
  /// </summary>
  /// <param name="path">
  /// The file to read, or <see langword="null"/> for <see cref="DefaultPath"/>.
  /// </param>
  /// <returns>
  /// The settings. This is the defaults when the file is missing or corrupt, and never
  /// <see langword="null"/>.
  /// </returns>
  public static AppSettings Load(string? path = null)
  {
    path ??= DefaultPath;
    if (!File.Exists(path))
    {
      return new AppSettings();
    }

    try
    {
      var json = File.ReadAllText(path);
      var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
      if (settings is null)
      {
        return Defaults($"The settings file is empty: {path}", path);
      }

      settings.Sanitize();
      return settings;
    }
    catch (JsonException e)
    {
      return Defaults($"The settings file is not valid JSON ({e.Message}): {path}", path);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
      return Defaults($"The settings file could not be read ({e.Message}): {path}", null);
    }
  }

  /// <summary>
  /// Writes the settings file, creating its folder if needed.
  /// </summary>
  /// <remarks>
  /// The text goes to a temporary file first, which then replaces the real one, so a crash in the
  /// middle of saving cannot leave a half-written settings file.
  /// </remarks>
  /// <param name="path">
  /// The file to write, or <see langword="null"/> for <see cref="DefaultPath"/>.
  /// </param>
  /// <returns>
  /// <see langword="true"/> if the file was written; <see langword="false"/> if the disk refused.
  /// </returns>
  public bool Save(string? path = null)
  {
    path ??= DefaultPath;
    try
    {
      var folder = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(folder))
      {
        Directory.CreateDirectory(folder);
      }

      var temp = path + ".tmp";
      File.WriteAllText(
          temp, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
      File.Move(temp, path, overwrite: true);
      return true;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
      return false;
    }
  }

  /// <summary>
  /// Creates the server options that match these settings.
  /// </summary>
  /// <returns>New options with the port and the network scope filled in.</returns>
  public DebugServerOptions CreateServerOptions() => new()
  {
    Port = Port,
    ListenOnAllInterfaces = ListenOnAllInterfaces,
  };

  private static AppSettings Defaults(string problem, string? corruptPath)
  {
    if (corruptPath is not null)
    {
      try
      {
        File.Move(corruptPath, corruptPath + ".bad", overwrite: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
        // Moving the bad file aside is a courtesy. The defaults work either way.
      }
    }

    return new AppSettings { LoadProblem = problem };
  }

  // A hand-edited file may hold values that make no sense. Replace them instead of failing later.
  private void Sanitize()
  {
    if (Port is < 1 or > 65535)
    {
      Port = ProtocolConstants.DefaultPort;
    }

    PanelVisibility ??= [];
  }
}

/// <summary>
/// The generated JSON metadata for <see cref="AppSettings"/>, which avoids reflection at run time.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
