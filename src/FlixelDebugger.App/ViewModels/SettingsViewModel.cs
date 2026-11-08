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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The ViewModel of the settings dialog: the ports, the network scope, and the MCP server for AI
/// agents.
/// </summary>
/// <remarks>
/// The ports are edited as text so a half-typed value never throws; <see cref="Error"/> and
/// <see cref="McpError"/> explain what is wrong and <see cref="SaveCommand"/> stays disabled until
/// both are valid. The "connect your agent" texts follow the MCP port as it is typed.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
  /// <summary>The note shown next to the "listen on all interfaces" option.</summary>
  public const string AllInterfacesNote =
      "Needed for Wi-Fi debugging, so a phone on your network can reach this PC. " +
      "Anyone on the same network can then connect, so keep it off on networks you do not trust.";

  /// <summary>The note shown next to the MCP option.</summary>
  public const string McpNote =
      "Lets AI agents such as Claude Code, Cursor, and Codex read and control the connected " +
      "games through the debugger, the way you do. It only accepts connections from this PC. " +
      "An agent can edit values, press keys, and take screenshots, so turn it on only when you " +
      "want that.";

  /// <summary>The name the connection snippets give the server.</summary>
  public const string ServerName = "flixelgdx";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Error))]
  [NotifyPropertyChangedFor(nameof(HasError))]
  [NotifyPropertyChangedFor(nameof(McpError))]
  [NotifyPropertyChangedFor(nameof(HasMcpError))]
  [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
  private string _portText;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(McpError))]
  [NotifyPropertyChangedFor(nameof(HasMcpError))]
  [NotifyPropertyChangedFor(nameof(McpUrl))]
  [NotifyPropertyChangedFor(nameof(ClaudeCodeCommand))]
  [NotifyPropertyChangedFor(nameof(CursorJson))]
  [NotifyPropertyChangedFor(nameof(CodexToml))]
  [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
  private string _mcpPortText;

  [ObservableProperty]
  private bool _listenOnAllInterfaces;

  [ObservableProperty]
  private bool _mcpEnabled;

  /// <summary>
  /// Creates the dialog state from the current settings.
  /// </summary>
  /// <param name="port">The current port.</param>
  /// <param name="listenOnAllInterfaces">
  /// Whether the server currently listens on all interfaces.
  /// </param>
  /// <param name="mcpEnabled">Whether the MCP server is turned on.</param>
  /// <param name="mcpPort">The current MCP port.</param>
  public SettingsViewModel(
      int port, bool listenOnAllInterfaces, bool mcpEnabled = false, int mcpPort = 6511)
  {
    _portText = port.ToString(CultureInfo.InvariantCulture);
    _mcpPortText = mcpPort.ToString(CultureInfo.InvariantCulture);
    _listenOnAllInterfaces = listenOnAllInterfaces;
    _mcpEnabled = mcpEnabled;
  }

  /// <summary>
  /// Raised when the dialog should close. The argument is <see langword="true"/> for Save.
  /// </summary>
  public event EventHandler<bool>? CloseRequested;

  /// <summary>
  /// Raised when a copy button was pressed. The window puts the text on the clipboard, because a
  /// ViewModel must not touch controls.
  /// </summary>
  public event EventHandler<string>? CopyRequested;

  /// <summary>Gets the port, or 0 when the text is not a valid port.</summary>
  public int Port => TryParsePort(PortText, out var port) ? port : 0;

  /// <summary>Gets the MCP port, or 0 when the text is not a valid port.</summary>
  public int McpPort => TryParsePort(McpPortText, out var port) ? port : 0;

  /// <summary>
  /// Gets what is wrong with the port text, or <see langword="null"/> when it is valid.
  /// </summary>
  public string? Error =>
      TryParsePort(PortText, out _) ? null : "Enter a port number from 1 to 65535.";

  /// <summary>Gets whether <see cref="Error"/> has a message.</summary>
  public bool HasError => Error is not null;

  /// <summary>
  /// Gets what is wrong with the MCP port text, or <see langword="null"/> when it is valid.
  /// </summary>
  public string? McpError
  {
    get
    {
      if (!TryParsePort(McpPortText, out var port))
      {
        return "Enter a port number from 1 to 65535.";
      }

      return TryParsePort(PortText, out var game) && game == port
          ? "The MCP port must differ from the game port."
          : null;
    }
  }

  /// <summary>Gets whether <see cref="McpError"/> has a message.</summary>
  public bool HasMcpError => McpError is not null;

  /// <summary>Gets the address agents connect to.</summary>
  public string McpUrl => $"http://127.0.0.1:{McpPortOrDefault}/mcp";

  /// <summary>Gets the command that adds the server to Claude Code.</summary>
  public string ClaudeCodeCommand => $"claude mcp add --transport http {ServerName} {McpUrl}";

  /// <summary>Gets the <c>.cursor/mcp.json</c> text for Cursor.</summary>
  public string CursorJson =>
      "{\n  \"mcpServers\": {\n    \"" + ServerName + "\": {\n      \"url\": \"" + McpUrl +
      "\"\n    }\n  }\n}";

  /// <summary>Gets the <c>~/.codex/config.toml</c> text for Codex.</summary>
  public string CodexToml => "[mcp_servers." + ServerName + "]\nurl = \"" + McpUrl + "\"";

  private int McpPortOrDefault => McpPort == 0 ? 6511 : McpPort;

  [RelayCommand(CanExecute = nameof(CanSave))]
  private void Save() => CloseRequested?.Invoke(this, true);

  [RelayCommand]
  private void Cancel() => CloseRequested?.Invoke(this, false);

  [RelayCommand]
  private void Copy(string? text)
  {
    if (!string.IsNullOrEmpty(text))
    {
      CopyRequested?.Invoke(this, text);
    }
  }

  private static bool TryParsePort(string text, out int port) =>
      int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) &&
          port is >= 1 and <= 65535;

  private bool CanSave() => Error is null && McpError is null;
}
