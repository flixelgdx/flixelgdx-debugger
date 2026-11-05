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
/// The ViewModel of the settings dialog: the port and the network scope.
/// </summary>
/// <remarks>
/// The port is edited as text so a half-typed value never throws; <see cref="Error"/> explains what
/// is wrong and <see cref="SaveCommand"/> stays disabled until the text is a valid port.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
  /// <summary>The note shown next to the "listen on all interfaces" option.</summary>
  public const string AllInterfacesNote =
      "Needed for Wi-Fi debugging, so a phone on your network can reach this PC. " +
      "Anyone on the same network can then connect, so keep it off on networks you do not trust.";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Error))]
  [NotifyPropertyChangedFor(nameof(HasError))]
  [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
  private string _portText;

  [ObservableProperty]
  private bool _listenOnAllInterfaces;

  /// <summary>
  /// Creates the dialog state from the current settings.
  /// </summary>
  /// <param name="port">The current port.</param>
  /// <param name="listenOnAllInterfaces">
  /// Whether the server currently listens on all interfaces.
  /// </param>
  public SettingsViewModel(int port, bool listenOnAllInterfaces)
  {
    _portText = port.ToString(CultureInfo.InvariantCulture);
    _listenOnAllInterfaces = listenOnAllInterfaces;
  }

  /// <summary>
  /// Raised when the dialog should close. The argument is <see langword="true"/> for Save.
  /// </summary>
  public event EventHandler<bool>? CloseRequested;

  /// <summary>Gets the port, or 0 when the text is not a valid port.</summary>
  public int Port => TryParsePort(PortText, out var port) ? port : 0;

  /// <summary>
  /// Gets what is wrong with the port text, or <see langword="null"/> when it is valid.
  /// </summary>
  public string? Error => TryParsePort(PortText, out _) ? null : "Enter a port number from 1 to 65535.";

  /// <summary>Gets whether <see cref="Error"/> has a message.</summary>
  public bool HasError => Error is not null;

  [RelayCommand(CanExecute = nameof(CanSave))]
  private void Save() => CloseRequested?.Invoke(this, true);

  [RelayCommand]
  private void Cancel() => CloseRequested?.Invoke(this, false);

  private static bool TryParsePort(string text, out int port) =>
      int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535;

  private bool CanSave() => Error is null;
}
