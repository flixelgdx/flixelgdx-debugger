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

using FlixelDebugger.App.Services;
using FlixelDebugger.App.Sessions;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Ui;

namespace FlixelDebugger.App.Panels;

/// <summary>
/// Keeps the app's panels in step with the active game: which panels exist, which ones the game
/// should build, and what the game asks the app to do.
/// </summary>
/// <remarks>
/// <para>
/// The game only builds a panel while someone holds it open, so a hidden tab costs the game
/// nothing. This class holds a panel open exactly while its tab is visible (open and in front), and
/// lets go when the tab is hidden or another game becomes active. It also registers every panel of
/// the game's panel list, and carries out the game's app requests: showing or hiding a panel,
/// resetting the layout, and showing a short notice.
/// </para>
/// <para>
/// The window's status bar and Debug menu read the stats and controls feeds, so this class also
/// subscribes to those two feeds of the active game.
/// </para>
/// <para>Use this class from the UI thread only, apart from the session events it handles.</para>
/// </remarks>
public sealed class PanelSync
{
  /// <summary>The feeds the window itself reads.</summary>
  public static readonly IReadOnlyList<string> ChromeFeeds =
      [BuiltInFeeds.Stats, BuiltInFeeds.Controls];

  private readonly IPanelRegistry _registry;
  private readonly IPanelLayout _layout;
  private readonly IUiScheduler _ui;
  private readonly INotificationService _notifier;
  private IGameSession? _session;

  /// <summary>
  /// Creates the service and starts following the active game.
  /// </summary>
  /// <param name="active">Tells which game is active.</param>
  /// <param name="registry">The panels the app knows.</param>
  /// <param name="layout">The arrangement of the panels.</param>
  /// <param name="ui">Moves work to the UI thread.</param>
  /// <param name="notifier">Shows the notices the game asks for.</param>
  public PanelSync(
      IActiveSession active,
      IPanelRegistry registry,
      IPanelLayout layout,
      IUiScheduler ui,
      INotificationService notifier)
  {
    ArgumentNullException.ThrowIfNull(active);
    ArgumentNullException.ThrowIfNull(registry);
    ArgumentNullException.ThrowIfNull(layout);
    ArgumentNullException.ThrowIfNull(ui);
    ArgumentNullException.ThrowIfNull(notifier);
    _registry = registry;
    _layout = layout;
    _ui = ui;
    _notifier = notifier;
    active.Changed += (_, e) => Follow(e.NewSession);
    layout.VisibilityChanged += (_, e) => SetOpen(e.PanelId, e.Visible);
    layout.RootChanged += (_, _) => OpenVisible();
    registry.Changed += (_, _) => OpenVisible();
    Follow(active.Current);
  }

  private void Follow(IGameSession? session)
  {
    if (_session is { } old)
    {
      old.PanelListChanged -= OnPanelList;
      old.AppRequested -= OnAppRequest;
      foreach (var panel in old.Panels)
      {
        _ = old.SetPanelOpenAsync(panel.Id, GamePanelViewModel.Owner, false);
      }

      foreach (var feed in ChromeFeeds)
      {
        _ = old.SetFeedWantedAsync(feed, GamePanelViewModel.Owner, false);
      }
    }

    _session = session;
    if (session is null)
    {
      return;
    }

    session.PanelListChanged += OnPanelList;
    session.AppRequested += OnAppRequest;
    foreach (var feed in ChromeFeeds)
    {
      _ = session.SetFeedWantedAsync(feed, GamePanelViewModel.Owner, true);
    }

    RegisterPanels(session);
  }

  private void OnPanelList(object? sender, EventArgs e) => _ui.Post(() =>
  {
    if (ReferenceEquals(sender, _session) && sender is IGameSession session)
    {
      RegisterPanels(session);
    }
  });

  private void RegisterPanels(IGameSession session)
  {
    var panels = session.Panels;
    var definitions = new PanelDefinition[panels.Count];
    for (var i = 0; i < definitions.Length; i++)
    {
      var panel = panels[i];
      definitions[i] = new PanelDefinition(panel.Id, panel.Title, panel.Dock, panel.Shown);
    }

    _registry.RegisterAll(definitions);
    OpenVisible();
  }

  private void OpenVisible()
  {
    if (_session is null)
    {
      return;
    }

    foreach (var definition in _registry.Definitions)
    {
      SetOpen(definition.Id, _layout.IsVisible(definition.Id));
    }
  }

  private void SetOpen(string id, bool visible)
  {
    if (_session is not { } session)
    {
      return;
    }

    _ = session.SetPanelOpenAsync(id, GamePanelViewModel.Owner, visible);
    if (visible && _registry.TryGet(id, out _))
    {
      _registry.GetViewModel(id).Refresh();
    }
  }

  private void OnAppRequest(object? sender, AppRequestEventArgs e) => _ui.Post(() =>
  {
    if (!ReferenceEquals(sender, _session))
    {
      return;
    }

    var request = e.Request;
    switch (request.Request)
    {
      case AppRequestKind.ShowPanel when _registry.TryGet(request.PanelId, out _):
        _layout.Reveal(request.PanelId);
        break;
      case AppRequestKind.HidePanel when _registry.TryGet(request.PanelId, out _):
        _layout.SetVisible(request.PanelId, false);
        break;
      case AppRequestKind.ResetLayout:
        _layout.Reset();
        break;
      case AppRequestKind.Notify when request.Text.Length > 0:
        _notifier.Show(LevelOf(request.Tone), request.Text);
        break;
    }
  });

  private static NotificationLevel LevelOf(Tone tone) => tone switch
  {
    Tone.Success => NotificationLevel.Success,
    Tone.Warning => NotificationLevel.Warning,
    Tone.Error => NotificationLevel.Error,
    _ => NotificationLevel.Info,
  };
}
