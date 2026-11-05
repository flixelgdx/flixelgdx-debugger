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

namespace FlixelDebugger.App.Services;

/// <summary>
/// Gives access to the saved settings, so ViewModels do not need to know where the file lives.
/// </summary>
public interface ISettingsStore
{
  /// <summary>Gets the settings object. Change its values and call <see cref="Save"/> to keep them.</summary>
  public AppSettings Current { get; }

  /// <summary>
  /// Writes the settings to disk.
  /// </summary>
  /// <returns><see langword="true"/> if the settings were saved.</returns>
  public bool Save();
}

/// <summary>
/// The <see cref="ISettingsStore"/> of the real app, which saves to the JSON settings file.
/// </summary>
/// <param name="current">The settings that were loaded at startup.</param>
/// <param name="path">The file to save to, or <see langword="null"/> for the default location.</param>
public sealed class FileSettingsStore(AppSettings current, string? path = null) : ISettingsStore
{
  /// <inheritdoc/>
  public AppSettings Current { get; } = current;

  /// <inheritdoc/>
  public bool Save() => Current.Save(path);
}
