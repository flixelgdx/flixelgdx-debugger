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

namespace FlixelDebugger.Core.Data;

/// <summary>
/// A "click twice to confirm" guard for dangerous buttons.
/// </summary>
/// <remarks>
/// <para>
/// The first <see cref="Press"/> arms the guard and returns <see langword="false"/>. A second press within
/// <see cref="Window"/> returns <see langword="true"/>, which means "the person confirmed". If the window runs out, the guard
/// disarms by itself, so a click minutes later starts over. Think of the cover on a fire alarm: you have to lift it
/// before you can press the button.
/// </para>
/// <para>Time comes from a <see cref="TimeProvider"/>, so tests can move the clock by hand.</para>
/// </remarks>
/// <example>
/// <code>
/// var arm = new ConfirmArm(TimeProvider.System);
/// if (arm.Press())
/// {
///     await session.SaveResetAllAsync();
/// }
/// </code>
/// </example>
public sealed class ConfirmArm
{
  /// <summary>The default time the guard stays armed: three seconds.</summary>
  public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(3);

  private readonly TimeProvider _clock;
  private long _armedAt;
  private bool _armed;

  /// <summary>
  /// Creates a disarmed guard.
  /// </summary>
  /// <param name="clock">The clock to measure the window with.</param>
  /// <param name="window">How long the guard stays armed, or <see langword="null"/> for <see cref="DefaultWindow"/>.</param>
  public ConfirmArm(TimeProvider clock, TimeSpan? window = null)
  {
    ArgumentNullException.ThrowIfNull(clock);
    _clock = clock;
    Window = window ?? DefaultWindow;
  }

  /// <summary>Gets how long the guard stays armed after the first press.</summary>
  public TimeSpan Window { get; }

  /// <summary>Gets whether the next press confirms. This turns <see langword="false"/> by itself when the window ran out.</summary>
  public bool IsArmed => _armed && _clock.GetElapsedTime(_armedAt) <= Window;

  /// <summary>
  /// Registers a click.
  /// </summary>
  /// <returns><see langword="true"/> if the guard was armed, which also disarms it. <see langword="false"/> if this press only armed it.</returns>
  public bool Press()
  {
    if (IsArmed)
    {
      _armed = false;
      return true;
    }

    _armedAt = _clock.GetTimestamp();
    _armed = true;
    return false;
  }

  /// <summary>
  /// Disarms the guard without confirming.
  /// </summary>
  public void Disarm() => _armed = false;
}
