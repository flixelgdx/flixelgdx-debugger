# FlixelGDX Debugger

A desktop app for inspecting a running FlixelGDX game: live performance stats, logs, watches, the scene
tree and inspector, a live map of the camera view, textures, a console, save data, and custom panels
registered by game code.

> **Status: in progress.** The app window works: dockable panels, a list of connected games, and every panel the
> protocol describes (stats, performance graphs, controls, watches, trackers, scene, viewport, inspector,
> textures, save, the console, the log viewer, and the custom panels games register).

## How a game connects

The debugger is a WebSocket **server**. A FlixelGDX game running in debug mode is the **client** and
connects to it.

1. Start the debugger. It listens on port **6510** (`ws://127.0.0.1:6510/`).
2. Run the game in debug mode (`flixel.mode=debug`). On desktop that is the JVM option
   `-Dflixel.mode=debug`.
3. The game connects, sends `HELLO`, and the debugger answers with `HELLO_ACK`. If the game is not
   running yet, no problem: the game retries every 2 seconds until the debugger is there.

**Android devices** cannot reach your PC's `127.0.0.1` directly. Forward the port over USB first, and the
game can keep connecting to `127.0.0.1:6510`:

```sh
adb reverse tcp:6510 tcp:6510
```

The complete protocol (transport, encoding, every message and panel payload) is specified in
`DEBUG_PROTOCOL.md` in the FlixelGDX framework repository. That document is the source of truth.

## Building and running

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The repo pins it in `global.json`.

```sh
dotnet build   # Builds everything with warnings as errors.
dotnet test    # Runs the protocol, core, MCP, and app tests.
dotnet format --verify-no-changes     # Checks code style.
```

Builds use a single worker (`Directory.Build.rsp`) so they stay friendly to older machines. Tests run on
[xUnit v3](https://xunit.net/) through Microsoft.Testing.Platform, which `global.json` turns on for
`dotnet test`.

### Running the app

```sh
dotnet run --project src/FlixelDebugger.App
```

The app is built with Avalonia 12 and Dock.Avalonia 12. It starts the server on launch, so a game running in debug mode connects by itself. Inside the window:

- **Game list** (top bar): every connected game with its platform, device, and ping. The first game that
  connects is selected automatically. If the selected game disconnects, its last data stays visible with a
  "Disconnected" note, and the same game reconnecting is selected again by itself.
- **Connect Android device** runs `adb reverse` for you (see "How a game connects"). If several devices are
  connected, you pick one.
- **Debug menu**: show or hide each panel, reset the layout, and open the settings (port, and listening on all
  interfaces for Wi-Fi debugging, and the MCP server for AI agents). A check mark means you can see the panel right now: a tab behind another tab
  in the same group is unchecked, and one click on it brings it to the front. Closing a tab with its X button
  unchecks it, and one click brings it back. Panels can be dragged, tabbed, and resized, and a tab's
  **Dock as Tabbed Document** option moves that panel into the middle group; the layout is saved when
  you close the app. A layout saved by an older version still loads, and panels it did not know about get a
  tab in the middle group.
- **Game menu**: show hitboxes (F2), pause (F3), free camera (F4), step one frame, and clear the log. The
  shortcuts work anywhere in the window except while you type in a text box.
- **Console**: Tab completes a command, Up and Down walk the history, Enter runs it. The debugger's own
  commands are `clear`, `show <panel>`, `close <panel>`, and `layout.reset`.
- **Logs**: filter by level or text, click a line to see its details and stack trace in a box below (the box
  only appears while a line is selected), and turn auto-scroll off to read older lines while new ones arrive.
- **Stats**: a key/value list of the newest sample (FPS, frame time, memory, draw calls, ping, RUNNING or
  PAUSED, game time, and the camera).
- **Performance**: collapsible sections of line graphs (Core, Render, Memory, Network) over the last 120
  samples. A graph exists only while its section is open, the scale grows at once and shrinks only when
  there is plenty of room, and hovering shows the value under the pointer.
- **Controls**: show hitboxes, pause, free camera, step one frame, the update rate (1 to 30 Hz), the game's log
  level, and a list of the game's keys and this window's shortcuts.
- **Watches** and **Trackers**: tables of the values the game registered. A value the game failed to read
  shows as red `error: message`.
- **Scene**: a tree of the game's objects. Clicking a node selects it in the game, and an Alt+click in the
  game reveals the node here. Expanded nodes stay open when the tree refreshes.
- **Viewport**: a live map of what one camera shows, drawn as one outline per object (the selection is
  highlighted, groups are dashed, inactive objects are muted, and hovering names the type and id). It works
  for any game, including one on a phone without a keyboard or mouse:
  - left click on an object selects what is drawn on top there, like Alt+click in the game (the Inspector tab
    does not jump in front of the map);
  - left drag on the selected object moves it, and the outline follows the pointer at once;
  - left drag anywhere else, or a middle or right drag anywhere, pans the camera like sliding a map;
  - the wheel zooms around the pointer (1.25 per notch);
  - a double click on empty space clears the selection.

  Panning and zooming turn the game's free camera on; **Reset camera** gives the camera back to the game.
  The toolbar also has a camera picker (only with several cameras), a "Free cam" badge, Pause or Resume,
  Step (while paused), a Hitboxes toggle, and the zoom and scroll. Moves and pans are sent about 30 times a
  second, added up in between.
- **Inspector**: the selected object's sections of properties, with editors for booleans, numbers, and text
  (Enter or leaving the box sends the value, and a row you are typing in is never overwritten), parent / go to /
  clear buttons, selecting by path, and a zoomable texture preview. The tab comes to the front when the game
  selects an object.
- **Textures**: totals, a table you can sort by any column (duplicates get a warning badge), and a zoomable
  preview with a pager.
- **Save**: the game's save data as a filterable table where unsaved values start with `* `. Edit a value, reset
  a key, save now, or reset everything (the button asks for a second click within three seconds). When the game
  keeps several saves (a settings file and a few profiles, for example), a picker at the top switches between
  them.
- **Custom panels**: any panel game code registers shows its rows with the same editors as the inspector, and
  its buttons.

`FlixelDebugger.Core` can also be used on its own, for example in a small console program:

```csharp
await using var server = new DebugServer();   // ws://127.0.0.1:6510/
server.SessionConnected += (_, e) => Console.WriteLine($"{e.Session.DisplayName} connected");
await server.StartAsync();
```

### Continuous integration and releases

Two GitHub Actions workflows live in `.github/workflows/`:

- **CI** (`ci.yml`) runs on every pull request and every push to `master`. It restores, builds in Release
  mode, runs the tests, checks formatting with `dotnet format --verify-no-changes`, and checks that no
  `.cs` or `.axaml` line is longer than 100 characters.
- **Release** (`release.yml`) builds self-contained single-file packages for `linux-x64`, `linux-arm64`,
  `win-x64`, `win-arm64`, and `osx-arm64`. To cut a release, push a version tag:

  ```sh
  git tag v0.1.0
  git push origin v0.1.0
  ```

  The workflow zips each package as `flixelgdx-debugger-<rid>.zip` and attaches them to a new GitHub
  Release. It can also be started by hand from the Actions tab, which builds the zips without releasing.

## Using with AI agents

The debugger has a built-in [MCP](https://modelcontextprotocol.io/) server, so an AI agent such as Claude
Code, Cursor, or Codex can debug a running game the way you do: read stats, logs, and watches, walk the scene,
read and edit properties, poke the viewport, run console commands, press keys, and look at a screenshot.

### Turn it on

The MCP server is **off by default**. Open **Debug > Settings...**, tick **Enable MCP server for AI agents**,
and press Save. It listens on port **6511**, only on this PC, at `http://127.0.0.1:6511/mcp` (Streamable
HTTP), and it is saved with your other settings. The settings dialog shows ready-to-copy connection texts
that follow the port you choose, and the status bar shows `MCP: 127.0.0.1:6511` while it runs.

### Connect an agent

Start a game in debug mode first (see "How a game connects"), then add the server to your agent:

- **Claude Code**: run this in a terminal (add `--scope project` to share it through `.mcp.json`):

  ```sh
  claude mcp add --transport http flixelgdx http://127.0.0.1:6511/mcp
  ```

  In a hand-written `.mcp.json`, the entry needs `"type": "http"` next to the `url`.

- **Cursor**: put this in `.cursor/mcp.json` (one project) or `~/.cursor/mcp.json` (all projects):

  ```json
  {
    "mcpServers": {
      "flixelgdx": { "url": "http://127.0.0.1:6511/mcp" }
    }
  }
  ```

- **Codex**: put this in `~/.codex/config.toml` (or a project's `.codex/config.toml`):

  ```toml
  [mcp_servers.flixelgdx]
  url = "http://127.0.0.1:6511/mcp"
  ```

Any other MCP client that speaks Streamable HTTP works with the same address. The client formats above were
checked against each tool's documentation when this was written; if a tool changes its format, its own MCP
documentation wins.

### Which game

Every tool takes an optional `session` argument: the game's id from `list_games` (a unique start of the id
works too). With **exactly one game connected** it can be left out. With several games it is required, and
the error lists every game as `id - title (platform, device)`, so an agent never changes the wrong game by
guessing.

### Tools

| Group | Tools |
|---|---|
| Games | `list_games` |
| Reading | `get_stats` (latest, min, max, and average), `get_logs` (unfiltered, with a `sinceSeq` cursor), `get_watches`, `list_commands`, `get_save`, `list_custom_panels`, `list_textures` |
| Scene | `get_scene` (paths, ids, types), `inspect`, `set_property` |
| Viewport and camera | `get_viewport`, `viewport_pick`, `move_object`, `pan_camera`, `zoom_camera`, `reset_camera`, `set_camera` |
| Controls | `set_paused`, `step`, `set_hitboxes`, `set_freecam`, `run_command` |
| Save and custom panels | `set_save_value`, `save_now`, `custom_press`, `custom_set_property` |
| Pictures | `screenshot`, `get_thumbnail` |
| Input | `press_key`, `key_down`, `key_up`, `click`, `pointer`, `scroll`, `click_object`, `type_text` |

List tools take a `limit` and say when they cut the list. Positions in the viewport and input tools are in
the camera's view space (0, 0 is the top left of what the camera shows), the same space as the boxes of
`get_viewport`. `click_object` clicks the center of an object's box, which is the easiest way to press a UI
button. Keys are given by name (`SPACE`, `A`, `LEFT`, `F1`, ...) or as a raw FlixelGDX key code.

Injected input is queued by the game and applied one key or button change per frame, so a tap takes two
frames. While the game is paused the input waits until you `step` or resume.

`screenshot` and the input tools need a game built with **debug protocol 1.1** (the `capture` and `input`
panels). A game on an older framework still works with every other tool, and these tools answer with a clear
error instead of hanging.

### What you see while an agent works

While an agent calls tools, the window shows it: a purple frame around the window, an "Agent: client - tool"
badge in the status bar (hover it for the last calls), an "Agent" marker on the game it drives in the game
list, a purple outline in the viewport around the object it touched last (plus a marker where it clicked),
and an "agent" marker on that object in the scene tree and the inspector. It all fades about 3 seconds after
the last call.

### Security notes

- The server only listens on `127.0.0.1`, even when the game server listens on all interfaces, and it refuses
  requests whose `Origin` or `Host` header is not a loopback name, so a web page cannot talk to it.
- There is no password. Any program running as you on this PC can use it, and an agent can edit values, press
  keys, and run console commands in your game. Turn it on only when you want an agent to have that power.
- `inspect`, `set_property`, and `viewport_pick` change the selection you see in the Inspector panel, because
  the selection lives in the game.

## Project layout

```
FlixelDebugger.slnx                      Solution (XML format).
Directory.Build.props                    Shared compiler settings, analyzers, metadata.
Directory.Packages.props                 Central package versions.
.editorconfig                            Code style and naming rules, enforced by the build.
src/
  FlixelDebugger.Protocol/               Wire protocol library (no UI, no networking).
    ProtocolConstants.cs                 Version, limits, subtypes, and action codes.
    MessageWriter.cs, MessageReader.cs   Little-endian binary encoding and decoding.
    PropertyValue.cs                     The shared value encoding (bool, int, long, float, ...).
    MessageDecoder.cs                    Turns received bytes into message models.
    Messages/                            HELLO, HELLO_ACK, PANEL_LIST, PANEL_DATA, PANEL_ACTION, PING, PONG.
    Panels/                              One payload model per built-in panel, plus custom panels.
    Actions/                             One model per panel action.
  FlixelDebugger.Core/                   The Model layer: no UI, uses Kestrel from the ASP.NET Core shared framework.
    Server/                              DebugServer (Kestrel + WebSocket), its options, and connection events.
    Sessions/                            GameSession (handshake, panel state, typed actions), PanelState, SceneTree.
    Data/                                StatsHistory and PingHistory (120-sample rings), LogStore (500-line ring with
                                         filters), TextureSorter, SaveListing, and ConfirmArm (click twice to confirm).
    Formatting/                          ValueFormat (how values are shown and parsed) and StatsTable (the stats rows).
    Completion/                          CommandCompleter: console suggestions, Tab cycling, history, submit.
    Graphs/                              GraphScale, GraphScaler, and GraphCatalog (the sections and graphs).
    Adb/                                 AdbHelper: finds adb, lists devices, runs "adb reverse".
    Settings/                            AppSettings: the JSON settings file in the per-user app data folder.
    ServiceCollectionExtensions.cs       AddFlixelDebuggerCore() for dependency injection.
  FlixelDebugger.Mcp/                    The built-in MCP server for AI agents (depends on Core only).
    McpHost.cs                           Loopback web host with the Origin and Host checks and tool registration.
    Tools/                               The tools, grouped by what they touch.
    Support/                             Session resolution, waiting for the game's answer, key names, JSON output.
  FlixelDebugger.App/                    The Avalonia desktop app (Views and ViewModels on top of Core).
    Program.cs, App.axaml, AppServices.cs  Entry point, theme and view templates, dependency injection.
    Sessions/                            IGameSession, IDebugHost, IActiveSession: small interfaces over Core's
                                         server and sessions, so ViewModels can be tested without a socket.
    Services/                            Interfaces and Avalonia implementations for the UI thread, toasts,
                                         dialogs, adb, the settings file, the MCP server, and the monitor that
                                         shows when an AI agent is at work.
    Panels/                              The panel registry (the extension point for panels), the base
                                         PanelViewModel, and the list of built-in panels.
      Docking/                           The Dock.Avalonia layout: default arrangement, save, and restore.
    ViewModels/                          MainWindow, dialog, and one ViewModel per panel. Panels that follow the
                                         game derive from LivePanelViewModel (10 Hz refresh, inspector and
                                         viewport 30 Hz).
    Views/                               MainWindow, the dialogs, and one thin view per panel.
    Controls/                            GraphControl (the performance graph) and ViewportControl (the
                                         viewport map), both custom drawn.
    Converters/                          PngImageConverter: thumbnail bytes to a bitmap.
tests/
  FlixelDebugger.Protocol.Tests/         xUnit tests and the protocol fixtures.
    Fixtures/                            Binary test vectors copied from the framework repository.
  FlixelDebugger.Core.Tests/             xUnit tests for Core, including end-to-end tests with a fake game
                                         that connects to a real DebugServer over a WebSocket.
  FlixelDebugger.Mcp.Tests/              xUnit tests that start a real McpHost, connect with the MCP SDK's client,
                                         and answer the tools with a fake game.
  FlixelDebugger.App.Tests/              xUnit tests for the ViewModels (with fake server, sessions, and adb),
                                         the panel registry, the dock layout, and a headless smoke test that
                                         opens the real main window.
```

### Adding a panel

Panels are registered by id in `Panels/DefaultPanels.cs`. To add a panel or change what one shows:

1. Derive a ViewModel from `PanelViewModel` (or from `LivePanelViewModel` if it follows the game's data on a
   timer) and add a view for it.
2. Add a `DataTemplate` for the ViewModel in `App.axaml`.
3. Change the panel's line in `DefaultPanels.CreateDefinitions()` to create the new ViewModel.

The dock, the Debug menu, the `show` and `close` console commands, and the saved visibility settings all read
the registry, so nothing else needs to change. Panels that games register at run time (ids starting with
`custom.`) are added to the registry by the main ViewModel, get a generic `CustomPanelViewModel`, and appear in the
middle tab group.

### Protocol fixtures

`tests/FlixelDebugger.Protocol.Tests/Fixtures/` holds binary test vectors and a `README.md` listing the
exact values each file encodes. They are copied from the framework repository
(`flixelgdx-test/src/test/resources/org/flixelgdx/debug/remote/fixtures/`). **When the protocol changes,
re-copy the whole folder** and update the tests. See the tests project's `README.md` for details.

## License

[MIT](LICENSE). Copyright (c) 2026 stringdotjar.
