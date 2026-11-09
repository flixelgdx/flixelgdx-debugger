# FlixelGDX Debugger

A desktop app for inspecting a running FlixelGDX game: live performance stats, logs, watches, the scene
tree and inspector, a live map of the camera view, textures, a console, save data, and any panel a game
developer builds.

> **Status: in progress.** The app window works: dockable panels, a list of connected games, and a generic
> renderer that draws every panel the game builds.

The app has no panels of its own. The game builds every panel, the built-in ones included, out of widgets
(rows, columns, grids, tabs, labels, buttons, checkboxes, dropdowns, text boxes, number fields, sliders,
tables, trees, property grids, graphs, canvases, and more) and runs all of their logic. The app draws the
widgets, keeps them up to date, and tells the game what the person clicked or typed. A game can replace or
redesign any built-in panel, or add new ones, without a new version of the app.

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

The complete protocol (transport, encoding, every message, widget, and feed) is specified in
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
- **Debug menu**: show or hide each panel, reset the layout, and open the settings (port, listening on all
  interfaces for Wi-Fi debugging, and the MCP server for AI agents). A check mark means you can see the
  panel right now: a tab behind another tab in the same group is unchecked, and one click on it brings it to
  the front. Closing a tab with its X button unchecks it, and one click brings it back. Panels can be
  dragged, tabbed, and resized, and a tab's **Dock as Tabbed Document** option moves that panel into the
  middle group; the layout is saved when you close the app.
- **Default layout**: each panel tells the app where it belongs (left column, middle tabs, right column, or
  bottom) and whether it starts open. The first game that connects fills the empty window that way, and
  **Reset layout** puts every panel back there. A saved layout keeps its tabs even before a game connects.
- **Game menu**: show hitboxes (F2), pause (F3), free camera (F4), step one frame, and clear the log. The
  shortcuts work anywhere in the window except while you type in a text box.
- **Panels**: the game only builds and sends a panel while its tab is visible, so hidden panels cost the game
  nothing. A few things happen in the app without asking the game, so they feel instant: sorting a table by
  a column, filtering a table or tree by a text box, keeping the newest log line in view, copying a row,
  remembering which tree nodes you opened, Tab completion and history in a command line, and asking "are
  you sure?" before a dangerous button.

FlixelGDX ships these panels: **Stats**, **Performance** (graphs), **Logs**, **Scene**, **Viewport** (a map of
what a camera shows, where you click to select, drag to move, right-drag to pan, and scroll to zoom),
**Inspector**, **Textures**, **Save** (a save editor), **Watches**, **Controls**, **Trackers**, and
**Console**. Their behavior is described in the framework's documentation, because the framework is where
they live.

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
  git tag v1.0.0
  git push origin v1.0.0
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
| Reading | `get_stats` (latest, min, max, and average), `get_logs` (unfiltered, with a `sinceSeq` cursor), `get_watches`, `list_commands`, `get_save`, `list_textures` |
| Scene | `get_scene` (paths, ids, types), `inspect`, `set_property` |
| Viewport and camera | `get_viewport`, `viewport_pick`, `move_object`, `pan_camera`, `zoom_camera`, `reset_camera`, `set_camera` |
| Controls | `set_paused`, `step`, `set_hitboxes`, `set_freecam`, `run_command` |
| Save | `set_save_value`, `save_now` |
| Any panel | `list_panels`, `read_panel`, `close_panel`, `panel_click`, `panel_set`, `panel_submit`, `panel_row`, `panel_edit`, `panel_event` |
| Pictures | `screenshot`, `get_thumbnail` |
| Input | `press_key`, `key_down`, `key_up`, `click`, `pointer`, `scroll`, `click_object`, `type_text` |

The typed tools read the game's *feeds*: data the game sends only while the MCP server runs. The panel tools
work with any panel the game built, custom ones included: `read_panel` returns the panel's widgets with their
names, ids, and values, and the other panel tools click, set, submit, select, or edit a widget by its name,
the same way a person would in the app. Each one answers with what the game changed in the panel. A panel
that `read_panel` opened stays open for agents until `close_panel` or until the MCP server stops.

List tools take a `limit` and say when they cut the list. Positions in the viewport and input tools are in
the camera's view space (0, 0 is the top left of what the camera shows), the same space as the boxes of
`get_viewport`. `click_object` clicks the center of an object's box, which is the easiest way to press a UI
button. Keys are given by name (`SPACE`, `A`, `LEFT`, `F1`, ...) or as a raw FlixelGDX key code.

Object ids and scene paths can be written as a number or a string, and `get_stats` `fields` as an array or a
comma separated string. A wrong argument type answers with an error that names the argument and the type
expected. Screenshots of one game run one after another (the game keeps one open request), and the PNGs are
recompressed in the debugger, so they are several times smaller than what the game sends. User interface
widgets usually live on their own camera: `get_viewport` shows one camera at a time (`set_camera` switches),
`click` and `pointer` use that camera's view space, and `click_object` finds the object on its own camera.

Injected input is queued by the game and applied one key or button change per frame, so a tap takes two
frames. While the game is paused the input waits until you `step` or resume.

`screenshot` and the input tools need a game whose framework has the `capture` and `input` feeds. A game on
an older framework still works with every other tool, and these tools answer with a clear
error instead of hanging.

### What you see while an agent works

While an agent calls tools, the window shows it: a purple frame around the window, an "Agent: client - tool"
badge in the status bar (hover it for the last calls), and an "Agent" marker on the game it drives in the game
list. It all fades about 3 seconds after the last call. The game's own Scene, Viewport, and Inspector panels
also mark the object an agent touched last.

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
    Messages/                            Every message: the handshake, feeds, panels, pings, and app requests.
    Ui/                                  Widget trees: UiValue, WidgetNode, and the widget, property, and event
                                         numbers.
    Feeds/                               One payload model per feed.
    Actions/                             One model per feed action.
  FlixelDebugger.Core/                   The Model layer: no UI, uses Kestrel from the ASP.NET Core shared framework.
    Server/                              DebugServer (Kestrel + WebSocket), its options, and connection events.
    Sessions/                            GameSession (handshake, feeds, panels, typed actions), FeedState,
                                         UiPanelState (a live copy of one panel's widgets), SceneTree.
    Data/                                StatsHistory and PingHistory (120-sample rings), LogStore (500-line ring with
                                         filters), TextureSorter, SaveListing, and ConfirmArm (click twice to confirm).
    Formatting/                          ValueFormat (how values are shown and parsed) and StatsTable (the stats rows).
    Completion/                          CommandCompleter: command-line suggestions, Tab cycling, and history.
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
    Panels/                              The panel registry (filled from the game's panel list),
                                         GamePanelViewModel (one class for every panel), and PanelSync (which
                                         panels the game builds, and the game's app requests).
      Docking/                           The Dock.Avalonia layout: default arrangement, save, and restore.
    Widgets/                             The generic renderer: UiWidget and one Avalonia control per widget type.
    ViewModels/                          MainWindow, the dialogs, and the property rows the property grid uses.
    Views/                               MainWindow, the dialogs, and GamePanelView.
    Converters/                          PngImageConverter: picture bytes to a bitmap.
tests/
  FlixelDebugger.Protocol.Tests/         xUnit tests and the protocol fixtures.
    Fixtures/                            Binary test vectors copied from the framework repository.
  FlixelDebugger.Core.Tests/             xUnit tests for Core, including end-to-end tests with a fake game
                                         that connects to a real DebugServer over a WebSocket.
  FlixelDebugger.Mcp.Tests/              xUnit tests that start a real McpHost, connect with the MCP SDK's client,
                                         and answer the tools with a fake game.
  FlixelDebugger.App.Tests/              xUnit tests for the ViewModels (with fake server, sessions, and adb),
                                         the panel registry, the dock layout, and a headless smoke test that
                                         opens the real main window with a panel of every widget type.
```

### Adding a panel

Panels are not added here. Build them in the game with the widget toolkit in the framework's
`org.flixelgdx.debug.ui` package and register them with `Flixel.debug.panels`; the app draws them on its own.
Work in this repository is needed only for a new kind of widget: add it to `DEBUG_PROTOCOL.md` in the
framework first, then add its numbers in `Protocol/Ui/`, a builder in `Widgets/WidgetFactory.cs` (or its own
control next to it), and a node of that type to the smoke test's panel.

### Protocol fixtures

`tests/FlixelDebugger.Protocol.Tests/Fixtures/` holds binary test vectors and a `README.md` listing the
exact values each file encodes. They are copied from the framework repository
(`flixelgdx-test/src/test/resources/org/flixelgdx/debug/remote/fixtures/`). **When the protocol changes,
re-copy the whole folder** and update the tests. See the tests project's `README.md` for details.

## License

[MIT](LICENSE). Copyright (c) 2026 stringdotjar.
