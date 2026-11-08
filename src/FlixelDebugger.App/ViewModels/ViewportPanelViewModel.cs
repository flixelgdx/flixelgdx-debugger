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

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.ViewModels;

/// <summary>
/// A mouse button, as the viewport panel tells them apart.
/// </summary>
public enum ViewportButton
{
  /// <summary>The left button: picks, moves the selection, or pans on empty space.</summary>
  Left,

  /// <summary>The middle button: always pans.</summary>
  Middle,

  /// <summary>The right button: always pans.</summary>
  Right,
}

/// <summary>
/// The ViewModel of the viewport panel: a live map of what one camera shows, where the person can
/// pick objects, drag them, and fly the camera.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it shows.</b> The game sends one box per drawn object, in view space (see
/// <see cref="ViewportPayload"/>). <see cref="Fit"/> letterboxes that view into the canvas, and the
/// control draws the boxes. This class owns everything except the pixels: the mouse gestures, the
/// toolbar state, and the actions it sends.
/// </para>
/// <para>
/// <b>Mouse.</b> The control passes canvas pixels in, and this class turns them into view units
/// with <see cref="Fit"/>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// a left click on a box sends <c>PICK</c> at that point, and the game picks what is on top;
/// </description></item>
/// <item><description>
/// a left drag that starts on the selected box sends <c>MOVE</c> by the drag distance;
/// </description></item>
/// <item><description>
/// a left drag anywhere else, or a middle or right drag anywhere, sends <c>PAN</c> so the scene
/// follows the pointer, like sliding a map on a table;
/// </description></item>
/// <item><description>
/// the wheel sends <c>ZOOM</c> around the pointer, 1.25 per notch;
/// </description></item>
/// <item><description>
/// a double click on empty space sends <c>PICK</c> there, which clears the selection.
/// </description></item>
/// </list>
/// <para>
/// <b>Units.</b> View space is world space shifted by the camera scroll, with the zoom already
/// folded into the view size, so a distance in view units is the same distance in world units.
/// That is why a drag's distance can be sent as <c>MOVE</c> as is, and why the dragged box stays
/// under the pointer.
/// </para>
/// <para>
/// <b>Throttling.</b> A mouse reports movement far more often than the game needs it. Drag and
/// wheel input is added up and sent on the panel's refresh, about 30 times a second, and once
/// more when the button is let go. <c>MOVE</c> and <c>PAN</c> are distances, so adding them up
/// loses nothing. The dragged box is drawn under the pointer at once
/// (<see cref="Shown(ViewportBox)"/>), so the drag feels instant even though the game answers
/// later.
/// </para>
/// <para>
/// <b>Selection.</b> A pick here is the same as an Alt+click in the game, but it is made in the
/// debugger, so it is marked with <see cref="SelectionOrigin"/>: the inspector does not jump in
/// front of this tab. The game decides what is picked; the mark uses the box this panel sees on
/// top, which is the same one unless the scene moved in between.
/// </para>
/// </remarks>
public sealed partial class ViewportPanelViewModel : LivePanelViewModel
{
  /// <summary>How often the viewport refreshes and sends input: about 30 times a second.</summary>
  public static readonly TimeSpan ViewportInterval = TimeSpan.FromMilliseconds(33);

  /// <summary>The zoom factor of one wheel notch.</summary>
  public const double ZoomStep = 1.25;

  /// <summary>How far, in pixels, the pointer has to move before a press becomes a drag.</summary>
  public const double DragThreshold = 4;

  /// <summary>The empty space around the view, in pixels.</summary>
  public const double Margin = 8;

  /// <summary>The hint shown while no game is connected.</summary>
  public const string NoGameHint = "Connect a game to see what its camera shows.";

  /// <summary>The hint shown while the game has sent no viewport yet.</summary>
  public const string WaitingHint =
      "Waiting for the game's viewport. Games built before the viewport panel do not send one.";

  /// <summary>The hint shown when the game has no camera.</summary>
  public const string NoCameraHint = "The game has no camera.";

  /// <summary>The line of mouse help under the map.</summary>
  public const string HelpText =
      "Click to pick. Drag the selection to move it. Drag anywhere else, or right drag, to pan. " +
      "Wheel to zoom. Double click empty space to clear the selection.";

  // How many refreshes a camera pick waits for the game to confirm it, and how long a dropped box
  // stays where it was dropped while the game's answer is on its way.
  private const int PickTimeoutTicks = 30;
  private const int SettleTicks = 15;

  private readonly SelectionOrigin _origin;
  private readonly IUiScheduler _ui;
  private readonly AgentActivityMonitor _agent;
  private ViewportPayload? _last;
  private int _agentId = -1;
  private bool _agentPointer;
  private float _agentPointerX;
  private float _agentPointerY;
  private double _canvasWidth;
  private double _canvasHeight;
  private double _startX;
  private double _startY;
  private double _lastX;
  private double _lastY;
  private double _hoverX = double.NaN;
  private double _hoverY = double.NaN;
  private double _pendingZoom = 1;
  private float _pendingMoveX;
  private float _pendingMoveY;
  private float _pendingPanX;
  private float _pendingPanY;
  private float _zoomAnchorX;
  private float _zoomAnchorY;
  private float _grabX;
  private float _grabY;
  private int _selectedCameraIndex;
  private int _pendingCamera = -1;
  private int _cameraTicks;
  private int _settleTicks;
  private int _pressedId = -1;
  private int _hitId = -1;
  private Gesture _gesture;
  private bool _built;
  private bool _dragging;
  private bool _syncing;

  [ObservableProperty]
  private bool _hasViewport;

  [ObservableProperty]
  private string _hint = NoGameHint;

  [ObservableProperty]
  private bool _freeCam;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(PauseText))]
  [NotifyCanExecuteChangedFor(nameof(StepCommand))]
  private bool _paused;

  [ObservableProperty]
  private bool _hitboxes;

  [ObservableProperty]
  private string _readout = string.Empty;

  /// <summary>
  /// Creates the panel.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="scheduler">Provides the UI-thread timer.</param>
  /// <param name="origin">Marks picks made here, so the inspector does not take the tab.</param>
  /// <param name="agent">Tells which object an AI agent touched last, to outline it.</param>
  public ViewportPanelViewModel(
      IActiveSession active,
      IUiScheduler scheduler,
      SelectionOrigin origin,
      AgentActivityMonitor agent)
      : base(BuiltInPanels.Viewport, "Viewport", active, scheduler, ViewportInterval)
  {
    ArgumentNullException.ThrowIfNull(origin);
    ArgumentNullException.ThrowIfNull(agent);
    _origin = origin;
    _agent = agent;
    _ui = scheduler;
    Refresh();
  }

  /// <summary>Raised when the map should be drawn again.</summary>
  public event EventHandler? Invalidated;

  /// <summary>
  /// Gets the payload the map shows, or <see langword="null"/> before the first one.
  /// </summary>
  public ViewportPayload? Payload => _last;

  /// <summary>Gets where the view sits in the canvas.</summary>
  public ViewportFit Fit { get; private set; }

  /// <summary>
  /// Gets the cameras the person can choose from, as labels. Entry 0 is the main camera.
  /// </summary>
  public ObservableCollection<string> Cameras { get; } = [];

  /// <summary>
  /// Gets whether the game has more than one camera, so the picker is worth showing.
  /// </summary>
  public bool HasManyCameras => Cameras.Count > 1;

  /// <summary>
  /// Gets or sets the position in <see cref="Cameras"/> of the camera the map shows.
  /// </summary>
  /// <remarks>
  /// Setting another index asks the game to switch (<c>SET_CAMERA</c>). A negative or out-of-range
  /// value is what a ComboBox reports while its items change; it is ignored and the property change
  /// is raised again, so the control shows the real selection.
  /// </remarks>
  public int SelectedCameraIndex
  {
    get => _selectedCameraIndex;
    set
    {
      if (_syncing)
      {
        // The ComboBox reacting to its items changing. The sync sets the real index afterwards.
        return;
      }

      if (value < 0 || value >= Cameras.Count)
      {
        _ui.Post(() => OnPropertyChanged(nameof(SelectedCameraIndex)));
        return;
      }

      if (!SetProperty(ref _selectedCameraIndex, value))
      {
        return;
      }

      // Keep the pick on screen until the game confirms it.
      _pendingCamera = value;
      _cameraTicks = 0;
      if (Session is { } session)
      {
        _ = session.ViewportSetCameraAsync((byte)value);
      }
    }
  }

  /// <summary>Gets the text of the pause button.</summary>
  public string PauseText => Paused ? "Resume" : "Pause";

  /// <summary>
  /// Gets the id of the object an AI agent touched last, which is outlined in the agent color, or
  /// -1 when no agent is active on this game.
  /// </summary>
  public int AgentObjectId => _agentId;

  /// <summary>Gets whether an agent injected a pointer position a moment ago.</summary>
  public bool HasAgentPointer => _agentPointer;

  /// <summary>Gets the x of the agent's pointer in view units.</summary>
  public float AgentPointerX => _agentPointerX;

  /// <summary>Gets the y of the agent's pointer in view units.</summary>
  public float AgentPointerY => _agentPointerY;

  /// <summary>
  /// Gets the id of the box the person is dragging (or just dropped), or -1.
  /// </summary>
  public int DraggedId { get; private set; } = -1;

  /// <summary>
  /// Gets the id of the box under the pointer, or -1.
  /// </summary>
  public int HoveredId { get; private set; } = -1;

  /// <summary>
  /// Gets the label of the box under the pointer, for example <c>FlixelSprite #12</c>, or an empty
  /// string.
  /// </summary>
  public string HoverText { get; private set; } = string.Empty;

  /// <summary>Gets the pointer position the hover label belongs to, in canvas pixels.</summary>
  public double HoverX => _hoverX;

  /// <summary>Gets the pointer position the hover label belongs to, in canvas pixels.</summary>
  public double HoverY => _hoverY;

  /// <inheritdoc/>
  public override void Refresh()
  {
    var session = Session;
    SyncAgent(session);
    Flush(session);
    var payload = session?.Viewport;
    var changed = !_built || !ReferenceEquals(payload, _last);
    if (changed)
    {
      _built = true;
      Apply(session, payload);
    }
    else if (_pendingCamera >= 0 && ++_cameraTicks > PickTimeoutTicks)
    {
      // The game never confirmed the camera pick, so show the camera it says it shows.
      SyncCameras(payload);
    }

    if (_settleTicks > 0 && --_settleTicks == 0 && !(_gesture == Gesture.Move && _dragging))
    {
      EndOverride();
    }
  }

  /// <summary>
  /// Tells the panel the size of its canvas, which moves the view.
  /// </summary>
  /// <param name="width">The canvas width in pixels.</param>
  /// <param name="height">The canvas height in pixels.</param>
  public void Resize(double width, double height)
  {
    _canvasWidth = width;
    _canvasHeight = height;
    UpdateFit();
    Invalidate();
  }

  /// <summary>
  /// Returns a box where it should be drawn: under the pointer while the person drags it, else
  /// where the game said.
  /// </summary>
  /// <param name="box">A box of <see cref="Payload"/>.</param>
  /// <returns>The box to draw.</returns>
  public ViewportBox Shown(ViewportBox box) =>
      box.Id == DraggedId ? box with { X = _grabX, Y = _grabY } : box;

  /// <summary>
  /// Handles a mouse button going down.
  /// </summary>
  /// <param name="button">The button.</param>
  /// <param name="x">The pointer x position in canvas pixels.</param>
  /// <param name="y">The pointer y position in canvas pixels.</param>
  /// <param name="clickCount">2 for the second press of a double click, else 1.</param>
  public void Press(ViewportButton button, double x, double y, int clickCount = 1)
  {
    if (_gesture != Gesture.None || _last is null || Fit.IsEmpty || Session is null)
    {
      return;
    }

    _startX = _lastX = x;
    _startY = _lastY = y;
    _dragging = false;
    if (button != ViewportButton.Left)
    {
      _gesture = Gesture.Pan;
      return;
    }

    var viewX = (float)Fit.ToViewX(x);
    var viewY = (float)Fit.ToViewY(y);
    var hit = TopmostAt(viewX, viewY);
    if (hit is null)
    {
      if (clickCount >= 2)
      {
        // A double click on empty space clears the selection, the same way the game does.
        Pick(viewX, viewY, -1);
        return;
      }

      _gesture = Gesture.Empty;
      return;
    }

    _hitId = hit.Value.Id;
    if (SelectedAt(viewX, viewY) is { } selected)
    {
      _gesture = Gesture.Move;
      _pressedId = selected.Id;
      return;
    }

    _gesture = Gesture.Box;
  }

  /// <summary>
  /// Handles the pointer moving, with or without a button held.
  /// </summary>
  /// <param name="x">The pointer x position in canvas pixels.</param>
  /// <param name="y">The pointer y position in canvas pixels.</param>
  public void Move(double x, double y)
  {
    if (_gesture == Gesture.None || Fit.IsEmpty)
    {
      Hover(x, y);
      return;
    }

    if (!_dragging)
    {
      var dx = x - _startX;
      var dy = y - _startY;
      if ((dx * dx) + (dy * dy) < DragThreshold * DragThreshold)
      {
        return;
      }

      _dragging = true;
      if (_gesture == Gesture.Move)
      {
        BeginOverride();
      }
    }

    // A pixel is 1 / Scale view units, and a view unit is a world unit.
    var moveX = (float)((x - _lastX) / Fit.Scale);
    var moveY = (float)((y - _lastY) / Fit.Scale);
    _lastX = x;
    _lastY = y;
    if (_gesture == Gesture.Move)
    {
      _pendingMoveX += moveX;
      _pendingMoveY += moveY;
      _grabX += moveX;
      _grabY += moveY;
      Hover(x, y);
      Invalidate();
    }
    else
    {
      // Grabbing the map: moving the pointer right slides the world right, so the camera goes left.
      _pendingPanX -= moveX;
      _pendingPanY -= moveY;
    }
  }

  /// <summary>
  /// Handles a mouse button going up, which ends the gesture.
  /// </summary>
  public void Release()
  {
    if (_gesture == Gesture.None)
    {
      return;
    }

    if (!_dragging && _gesture is Gesture.Box or Gesture.Move)
    {
      Pick((float)Fit.ToViewX(_startX), (float)Fit.ToViewY(_startY), _hitId);
    }

    End();
  }

  /// <summary>
  /// Ends a gesture without a click, for example when the window loses the mouse. What was dragged
  /// so far is still sent.
  /// </summary>
  public void Cancel()
  {
    if (_gesture != Gesture.None)
    {
      End();
    }
  }

  /// <summary>
  /// Handles the mouse wheel: zooms around the pointer.
  /// </summary>
  /// <param name="notches">How far the wheel turned; positive zooms in.</param>
  /// <param name="x">The pointer x position in canvas pixels.</param>
  /// <param name="y">The pointer y position in canvas pixels.</param>
  public void Wheel(double notches, double x, double y)
  {
    if (notches == 0 || !double.IsFinite(notches) || _last is null || Fit.IsEmpty)
    {
      return;
    }

    _pendingZoom *= Math.Pow(ZoomStep, notches);
    _zoomAnchorX = (float)Fit.ToViewX(x);
    _zoomAnchorY = (float)Fit.ToViewY(y);
  }

  /// <summary>
  /// Forgets the hovered box, for example when the pointer leaves the canvas.
  /// </summary>
  public void Leave()
  {
    _hoverX = double.NaN;
    _hoverY = double.NaN;
    SetHover(-1, string.Empty);
  }

  /// <inheritdoc/>
  protected override void OnSessionSwitched(IGameSession? oldSession, IGameSession? newSession)
  {
    _built = false;
    _last = null;
    _gesture = Gesture.None;
    _dragging = false;
    _pendingCamera = -1;
    _pendingZoom = 1;
    _pendingMoveX = _pendingMoveY = _pendingPanX = _pendingPanY = 0;
    _settleTicks = 0;
    DraggedId = -1;
  }

  partial void OnHitboxesChanged(bool value)
  {
    if (!_syncing && Session is { } session)
    {
      _ = session.SetHitboxesAsync(value);
    }
  }

  [RelayCommand]
  private async Task TogglePauseAsync()
  {
    if (Session is { } session)
    {
      await session.SetPausedAsync(!Paused);
    }
  }

  [RelayCommand(CanExecute = nameof(Paused))]
  private async Task StepAsync()
  {
    if (Session is { } session)
    {
      await session.StepAsync();
    }
  }

  [RelayCommand]
  private async Task ResetCameraAsync()
  {
    if (Session is { } session)
    {
      await session.ViewportResetCameraAsync();
    }
  }

  // Sends what the mouse did since the last refresh, as one action of each kind.
  private void Flush(IGameSession? session)
  {
    if (_pendingMoveX != 0 || _pendingMoveY != 0)
    {
      if (session is not null && DraggedId >= 0)
      {
        _ = session.ViewportMoveAsync(DraggedId, _pendingMoveX, _pendingMoveY);
      }

      _pendingMoveX = _pendingMoveY = 0;
    }

    if (_pendingPanX != 0 || _pendingPanY != 0)
    {
      if (session is not null)
      {
        _ = session.ViewportPanAsync(_pendingPanX, _pendingPanY);
      }

      _pendingPanX = _pendingPanY = 0;
    }

    if (_pendingZoom != 1)
    {
      if (session is not null)
      {
        _ = session.ViewportZoomAsync((float)_pendingZoom, _zoomAnchorX, _zoomAnchorY);
      }

      _pendingZoom = 1;
    }
  }

  private void End()
  {
    var moved = _gesture == Gesture.Move && _dragging;
    _gesture = Gesture.None;
    _dragging = false;
    _pressedId = -1;
    _hitId = -1;
    Flush(Session);
    if (moved)
    {
      // Keep the box where it was dropped until the game's answer arrives.
      _settleTicks = SettleTicks;
    }
  }

  private void Pick(float viewX, float viewY, int expectedId)
  {
    if (Session is not { } session)
    {
      return;
    }

    if (expectedId >= 0)
    {
      _origin.MarkLocal(expectedId);
    }

    _ = session.ViewportPickAsync(viewX, viewY);
  }

  private void BeginOverride()
  {
    _settleTicks = 0;
    if (DraggedId >= 0 && DraggedId == _pressedId)
    {
      // Grabbed again before the game answered the last drop: keep drawing it where it was left.
      return;
    }

    DraggedId = -1;
    if (BoxById(_pressedId) is { } box)
    {
      DraggedId = box.Id;
      _grabX = box.X;
      _grabY = box.Y;
    }
  }

  private void EndOverride()
  {
    _settleTicks = 0;
    if (DraggedId >= 0)
    {
      DraggedId = -1;
      Invalidate();
    }
  }

  private void Apply(IGameSession? session, ViewportPayload? payload)
  {
    _last = payload;
    HasViewport = payload is { CameraCount: > 0 };
    Hint = session is null ? NoGameHint
        : payload is null ? WaitingHint
        : payload.CameraCount == 0 ? NoCameraHint
        : string.Empty;

    _syncing = true;
    FreeCam = payload?.FreeCam ?? false;
    Paused = payload?.Paused ?? false;
    Hitboxes = payload?.Hitboxes ?? false;
    _syncing = false;
    Readout = payload is { CameraCount: > 0 }
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"Zoom {payload.Zoom:0.##}x   Scroll {payload.ScrollX:0.#}, {payload.ScrollY:0.#}")
        : string.Empty;

    SyncCameras(payload);

    // The game's answer to a drop has arrived once the box is where it was dropped.
    if (_settleTicks > 0 && BoxById(DraggedId) is { } dropped &&
        Math.Abs(dropped.X - _grabX) < 0.5f && Math.Abs(dropped.Y - _grabY) < 0.5f)
    {
      EndOverride();
    }

    UpdateFit();
    Hover(_hoverX, _hoverY);
    Invalidate();
  }

  // Copies the game's camera list and choice, without sending anything back.
  private void SyncCameras(ViewportPayload? payload)
  {
    var count = payload?.CameraCount ?? 0;
    var selected = count == 0 ? 0 : Math.Min((int)payload!.Camera, count - 1);
    if (_pendingCamera >= 0)
    {
      // Wait for the game to switch, unless it takes too long or the camera is gone.
      if (selected == _pendingCamera || _cameraTicks > PickTimeoutTicks ||
          _pendingCamera >= count)
      {
        _pendingCamera = -1;
      }
      else
      {
        selected = _pendingCamera;
      }
    }

    var hadMany = HasManyCameras;
    var resized = Cameras.Count != count;
    _syncing = true;
    try
    {
      // A camera's label never changes, so entries are only added or removed at the end. That
      // keeps the ComboBox from dropping its selection.
      while (Cameras.Count < count)
      {
        var index = Cameras.Count;
        Cameras.Add(index == 0
            ? "Camera 0 (main)"
            : string.Create(CultureInfo.InvariantCulture, $"Camera {index}"));
      }

      while (Cameras.Count > count)
      {
        Cameras.RemoveAt(Cameras.Count - 1);
      }

      var moved = selected != _selectedCameraIndex;
      _selectedCameraIndex = selected;

      // Also raised when only the list changed size: adding or removing entries may have cleared
      // the ComboBox's own selection.
      if (moved || resized)
      {
        OnPropertyChanged(nameof(SelectedCameraIndex));
      }
    }
    finally
    {
      _syncing = false;
    }

    if (hadMany != HasManyCameras)
    {
      OnPropertyChanged(nameof(HasManyCameras));
    }
  }

  private void UpdateFit() =>
      Fit = _last is { CameraCount: > 0 } payload
          ? ViewportFit.Compute(
              _canvasWidth, _canvasHeight, payload.ViewWidth, payload.ViewHeight, Margin)
          : default;

  private void Hover(double x, double y)
  {
    _hoverX = x;
    _hoverY = y;
    if (double.IsNaN(x) || Fit.IsEmpty || _last is not { } payload)
    {
      SetHover(-1, string.Empty);
      return;
    }

    if (TopmostAt((float)Fit.ToViewX(x), (float)Fit.ToViewY(y)) is not { } box)
    {
      SetHover(-1, string.Empty);
      return;
    }

    if (box.Id == HoveredId && HoverText.Length > 0)
    {
      Invalidate();
      return;
    }

    var text = string.Create(CultureInfo.InvariantCulture, $"{payload.TypeOf(box)} #{box.Id}");
    if (box.IsGroup)
    {
      text += " (group)";
    }

    if (box.IsInactive)
    {
      text += " (inactive)";
    }

    SetHover(box.Id, text);
  }

  private void SetHover(int id, string text)
  {
    var changed = id != HoveredId || text != HoverText;
    HoveredId = id;
    HoverText = text;

    // The label follows the pointer, so draw again even when the box stayed the same.
    if (changed || id >= 0)
    {
      Invalidate();
    }
  }

  // The box drawn on top at a point: the last one in draw order that contains it.
  private ViewportBox? TopmostAt(float viewX, float viewY)
  {
    if (_last is not { } payload)
    {
      return null;
    }

    for (var i = payload.Boxes.Count - 1; i >= 0; i--)
    {
      var box = Shown(payload.Boxes[i]);
      if (box.Contains(viewX, viewY))
      {
        return box;
      }
    }

    return null;
  }

  // The selected box when it contains the point, even if another box is drawn over it, so a
  // selected group can be dragged by any spot inside it.
  private ViewportBox? SelectedAt(float viewX, float viewY)
  {
    if (_last is not { SelectedId: >= 0 } payload ||
        BoxById(payload.SelectedId) is not { } selected)
    {
      return null;
    }

    var shown = Shown(selected);
    return shown.Contains(viewX, viewY) ? shown : null;
  }

  private ViewportBox? BoxById(int id)
  {
    if (id < 0 || _last is not { } payload)
    {
      return null;
    }

    foreach (var box in payload.Boxes)
    {
      if (box.Id == id)
      {
        return box;
      }
    }

    return null;
  }

  private void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);

  // Copies what the agent did into plain fields, so the control only reads them while drawing.
  private void SyncAgent(IGameSession? session)
  {
    var driving = _agent.IsDriving(session?.Id);
    var id = driving ? _agent.ObjectId : -1;
    var pointer = driving && _agent.HasPointer;
    var x = pointer ? _agent.PointerX : 0f;
    var y = pointer ? _agent.PointerY : 0f;
    if (id == _agentId && pointer == _agentPointer && x == _agentPointerX && y == _agentPointerY)
    {
      return;
    }

    _agentId = id;
    _agentPointer = pointer;
    _agentPointerX = x;
    _agentPointerY = y;
    Invalidate();
  }

  private enum Gesture
  {
    None,

    // A left press on a box: a click picks, a drag pans.
    Box,

    // A left press on the selected box: a click picks, a drag moves it.
    Move,

    // A left press on empty space: a drag pans, a click does nothing.
    Empty,

    // A middle or right press: a drag pans.
    Pan,
  }
}
