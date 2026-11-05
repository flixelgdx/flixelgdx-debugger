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
using FlixelDebugger.Core.Sessions;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// The state of a thumbnail preview: the picture, a zoom level, and a status line.
/// </summary>
/// <remarks>
/// <para>
/// The inspector and the textures panel both show a PNG the game sends on request. The game may never answer (when it
/// cannot read the pixels), so the preview counts refresh ticks while it waits and gives up after a timeout
/// instead of saying "waiting" forever.
/// </para>
/// <para>
/// The ViewModel only holds the PNG bytes. The view turns them into a bitmap, so this class needs no UI.
/// </para>
/// </remarks>
public sealed partial class ThumbnailPreviewViewModel : ObservableObject
{
    /// <summary>The smallest zoom factor.</summary>
    public const double MinZoom = 0.5;

    /// <summary>The largest zoom factor.</summary>
    public const double MaxZoom = 4;

    /// <summary>The zoom slider step.</summary>
    public const double ZoomStep = 0.25;

    private readonly int _timeoutTicks;
    private int _waitedTicks;
    private bool _waiting;

    [ObservableProperty]
    private double _zoom = 1;

    [ObservableProperty]
    private byte[]? _image;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>
    /// Creates a preview.
    /// </summary>
    /// <param name="timeoutTicks">How many <see cref="Tick"/> calls to wait for an answer before saying there is none.</param>
    public ThumbnailPreviewViewModel(int timeoutTicks)
    {
        _timeoutTicks = timeoutTicks;
    }

    /// <summary>Gets the width the picture is drawn at, which is its width times <see cref="Zoom"/>.</summary>
    public double DisplayWidth => ImageWidth * Zoom;

    /// <summary>Gets the height the picture is drawn at, which is its height times <see cref="Zoom"/>.</summary>
    public double DisplayHeight => ImageHeight * Zoom;

    /// <summary>Gets whether a picture is available.</summary>
    public bool HasImage => Image is not null;

    /// <summary>Gets the id of the picture that is shown, or -1 for none.</summary>
    public int ShownId { get; private set; } = -1;

    /// <summary>
    /// Forgets the picture and starts waiting for a new answer.
    /// </summary>
    public void BeginWaiting()
    {
        Clear();
        _waiting = true;
        _waitedTicks = 0;
        Status = "Waiting for the game...";
    }

    /// <summary>
    /// Forgets the picture and stops waiting.
    /// </summary>
    public void Clear()
    {
        _waiting = false;
        ShownId = -1;
        Image = null;
        ImageWidth = 0;
        ImageHeight = 0;
        Status = string.Empty;
    }

    /// <summary>
    /// Shows a thumbnail, unless it is the one already shown.
    /// </summary>
    /// <param name="thumbnail">The thumbnail from the game.</param>
    public void Show(Thumbnail thumbnail)
    {
        _waiting = false;
        if (Image is not null && ShownId == thumbnail.Id && thumbnail.Png.Span.SequenceEqual(Image))
        {
            return;
        }

        ShownId = thumbnail.Id;
        ImageWidth = thumbnail.Width;
        ImageHeight = thumbnail.Height;
        Image = thumbnail.Png.ToArray();
        Status = string.Create(CultureInfo.InvariantCulture, $"{thumbnail.Width} x {thumbnail.Height} px");
    }

    /// <summary>
    /// Counts one refresh while waiting. When the wait runs out the status says the game did not answer.
    /// </summary>
    public void Tick()
    {
        if (_waiting && ++_waitedTicks > _timeoutTicks)
        {
            _waiting = false;
            Status = "The game did not send a preview.";
        }
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayWidth));
        OnPropertyChanged(nameof(DisplayHeight));
    }

    partial void OnImageWidthChanged(int value) => OnPropertyChanged(nameof(DisplayWidth));

    partial void OnImageHeightChanged(int value) => OnPropertyChanged(nameof(DisplayHeight));

    partial void OnImageChanged(byte[]? value) => OnPropertyChanged(nameof(HasImage));
}
