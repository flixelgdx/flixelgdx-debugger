# FlixelGDX Debugger

A desktop app for inspecting a running FlixelGDX game: live performance stats, logs, watches, the scene
tree and inspector, textures, a console, save data, and custom panels registered by game code.

> **Status: in progress.** The app itself (a C# and Avalonia desktop application using MVVM) is not built
> yet. What exists today is the repository layout, `FlixelDebugger.Protocol` (the C# implementation of the
> wire protocol the app and the game talk to), and `FlixelDebugger.Core` (the WebSocket server, game
> sessions, and the data models and logic behind every panel, with no UI).

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
dotnet test    # Runs the protocol and core tests.
dotnet format --verify-no-changes     # Checks code style.
```

Builds use a single worker (`Directory.Build.rsp`) so they stay friendly to older machines. Tests run on
[xUnit v3](https://xunit.net/) through Microsoft.Testing.Platform, which `global.json` turns on for
`dotnet test`.

There is nothing to run yet: the app's UI project has not been added. `FlixelDebugger.Core` can already be
used on its own, for example in a small console program:

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
  It will fail until the app project (`src/FlixelDebugger.App`) is added.

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
tests/
  FlixelDebugger.Protocol.Tests/         xUnit tests and the protocol fixtures.
    Fixtures/                            Binary test vectors copied from the framework repository.
  FlixelDebugger.Core.Tests/             xUnit tests for Core, including end-to-end tests with a fake game
                                         that connects to a real DebugServer over a WebSocket.
```

### Protocol fixtures

`tests/FlixelDebugger.Protocol.Tests/Fixtures/` holds binary test vectors and a `README.md` listing the
exact values each file encodes. They are copied from the framework repository
(`flixelgdx-test/src/test/resources/org/flixelgdx/debug/remote/fixtures/`). **When the protocol changes,
re-copy the whole folder** and update the tests. See the tests project's `README.md` for details.

## License

[MIT](LICENSE). Copyright (c) 2026 stringdotjar.
