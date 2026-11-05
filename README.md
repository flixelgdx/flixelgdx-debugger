# FlixelGDX Debugger

A desktop app for inspecting a running FlixelGDX game: live performance stats, logs, watches, the scene
tree and inspector, textures, a console, save data, and custom panels registered by game code.

> **Status: in progress.** The app window works: dockable panels, a list of connected games, the console, and
> the log viewer. The other panels (stats, performance, controls, watches, trackers, scene, inspector,
> textures, save, and the custom panels games register) show a placeholder until their content is built.

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
dotnet test    # Runs the protocol, core, and app tests.
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
  interfaces for Wi-Fi debugging). Panels can be dragged, tabbed, and resized; the layout is saved when you
  close the app.
- **Game menu**: show hitboxes (F2), pause (F3), free camera (F4), step one frame, and clear the log. The
  shortcuts work anywhere in the window except while you type in a text box.
- **Console**: Tab completes a command, Up and Down walk the history, Enter runs it. The debugger's own
  commands are `clear`, `show <panel>`, `close <panel>`, and `layout.reset`.
- **Logs**: filter by level or text, click a line to see its stack trace below, and turn auto-scroll off to
  read older lines while new ones arrive.

`FlixelDebugger.Core` can also be used on its own, for example in a small console program:

```csharp
await using var server = new DebugServer();   // ws://127.0.0.1:6510/
server.SessionConnected += (_, e) => Console.WriteLine($"{e.Session.DisplayName} connected");
await server.StartAsync();
```

### Continuous integration and releases

Two GitHub Actions workflows live in `.github/workflows/`:

- **CI** (`ci.yml`) runs on every pull request and every push to `master`. It restores, builds in Release
  mode, runs the tests, and checks formatting with `dotnet format --verify-no-changes`.
- **Release** (`release.yml`) builds self-contained single-file packages for `linux-x64`, `linux-arm64`,
  `win-x64`, `win-arm64`, and `osx-arm64`. To cut a release, push a version tag:

  ```sh
  git tag v0.1.0
  git push origin v0.1.0
  ```

  The workflow zips each package as `flixelgdx-debugger-<rid>.zip` and attaches them to a new GitHub
  Release. It can also be started by hand from the Actions tab, which builds the zips without releasing.

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
    Data/                                StatsHistory (120-sample rings) and LogStore (500-line ring with filters).
    Completion/                          CommandCompleter: console suggestions, Tab cycling, history, submit.
    Graphs/                              GraphScale and GraphScaler for the performance graphs.
    Adb/                                 AdbHelper: finds adb, lists devices, runs "adb reverse".
    Settings/                            AppSettings: the JSON settings file in the per-user app data folder.
    ServiceCollectionExtensions.cs       AddFlixelDebuggerCore() for dependency injection.
  FlixelDebugger.App/                    The Avalonia desktop app (Views and ViewModels on top of Core).
    Program.cs, App.axaml, AppServices.cs  Entry point, theme and view templates, dependency injection.
    Sessions/                            IGameSession, IDebugHost, IActiveSession: small interfaces over Core's
                                         server and sessions, so ViewModels can be tested without a socket.
    Services/                            Interfaces and Avalonia implementations for the UI thread, toasts,
                                         dialogs, adb, and the settings file.
    Panels/                              The panel registry (the extension point for panels), the base
                                         PanelViewModel, and the placeholder panel.
      Docking/                           The Dock.Avalonia layout: default arrangement, save, and restore.
    ViewModels/                          MainWindow, Console, Logs, and dialog ViewModels.
    Views/                               MainWindow, the console and log views, and the dialogs.
tests/
  FlixelDebugger.Protocol.Tests/         xUnit tests and the protocol fixtures.
    Fixtures/                            Binary test vectors copied from the framework repository.
  FlixelDebugger.Core.Tests/             xUnit tests for Core, including end-to-end tests with a fake game
                                         that connects to a real DebugServer over a WebSocket.
  FlixelDebugger.App.Tests/              xUnit tests for the ViewModels (with fake server, sessions, and adb),
                                         the panel registry, the dock layout, and a headless smoke test that
                                         opens the real main window.
```

### Adding a panel

Panels are registered by id in `Panels/DefaultPanels.cs`. To replace a placeholder with real content:

1. Derive a ViewModel from `PanelViewModel` and add a view for it.
2. Add a `DataTemplate` for the ViewModel in `App.axaml`.
3. Change the panel's line in `DefaultPanels.CreateDefinitions()` to create the new ViewModel.

The dock, the Debug menu, the `show` and `close` console commands, and the saved visibility settings all read
the registry, so nothing else needs to change. Panels that games register at run time (ids starting with
`custom.`) are added to the registry by the main ViewModel and appear in the middle tab group.

### Protocol fixtures

`tests/FlixelDebugger.Protocol.Tests/Fixtures/` holds binary test vectors and a `README.md` listing the
exact values each file encodes. They are copied from the framework repository
(`flixelgdx-test/src/test/resources/org/flixelgdx/debug/remote/fixtures/`). **When the protocol changes,
re-copy the whole folder** and update the tests. See the tests project's `README.md` for details.

## License

[MIT](LICENSE). Copyright (c) 2026 stringdotjar.
