# FlixelGDX Debugger (project instructions for AI assistants)

---

## Project context (FlixelGDX Debugger)

The FlixelGDX Debugger is a desktop app for inspecting a running FlixelGDX game: live performance stats, logs,
watches, the scene tree and inspector, a live map of the camera view, textures, a console, save data, and any
panel a game developer builds. The game builds every panel out of widgets and runs its logic; the app is a
generic renderer that draws the widgets and sends back what the person does.

It is written in **C# on .NET 10** with **Avalonia 12** and **Dock.Avalonia 12**. The debugger is a WebSocket
**server** (port 6510 by default), and a FlixelGDX game running in debug mode is the **client** that connects to it.

The [FlixelGDX framework](https://github.com/flixelgdx/flixelgdx) is a separate repository. The wire protocol is
specified in `DEBUG_PROTOCOL.md` in the framework repository, and **that document is the source of truth**. If the
debugger and the spec disagree, the debugger is wrong (or the spec needs a change in the framework first).

---

## Collaboration before implementation

Treat the interaction as teamwork, not robotic task execution. Prefer brainstorming when the user's direction
is ambiguous.

- Before implementing anything (planning or coding):

  1. Ask yourself whether the requested design or refactor is actually good for the debugger.
  2. If it hurts the app, breaks invariants, or there is clearly a better path, **stop before editing files or running commands**.
  3. Explain why (pros and cons), suggest better alternatives, and ask whether the user still wants to proceed.
  4. If they confirm after that discussion, proceed as requested.

- If you are unsure about something, whether that would be for a library, the protocol, a specific aspect of the
  codebase, etc., do **not** assume anything. Always verify first before making an assumption. If you are still
  unsure about something, ignore it and bring it up to the user when you're done completing a task.

---

## Explaining things for beginners and contributors

The project welcomes new contributors learning open source.

When explaining code or introducing patterns:

- Explain **why** before **how** (motivation before mechanics).
- Use analogies for complex systems; if the user gave no analogy topic, ask for one they like.
- End **complex** explanations with a short check-in question so you can verify understanding.
- Stay encouraging and professional. Assume intelligence but not deep familiarity with C#, Avalonia, or MVVM.

---

## Architecture and scope

The solution (`FlixelDebugger.slnx`) has three layers. Each one only depends on the layers below it:

1. **`FlixelDebugger.Protocol`**: reads and writes the binary wire format (messages, widget trees, feed payloads,
   actions, values). No dependencies on Core, the app, or any UI library.
2. **`FlixelDebugger.Core`**: the WebSocket server, game sessions, feed and panel state, stored data (logs, stats history,
   scene tree), settings, and the `adb` helper. It depends on Protocol only, and must stay **UI-free** so it can be
   tested without Avalonia.
3. **`FlixelDebugger.App`**: the Avalonia app. MVVM with CommunityToolkit.Mvvm: ViewModels in `ViewModels/`, views
   (`.axaml`) in `Views/`, docking and the panel registry in `Panels/`, the widget renderer in `Widgets/`, and
   platform services behind interfaces in `Services/`.

Rules that follow from this:

- Keep protocol details in Protocol, logic that does not need a UI in Core, and only presentation in App.
- ViewModels must not touch Avalonia controls directly. Go through the service interfaces (`IUiScheduler`,
  `IDialogService`, `INotificationService`, `ISettingsStore`, `IAdbClient`) so they stay testable with fakes.
- Unknown messages, panels, and actions must decode to their `Unknown*` types instead of throwing, so a newer game
  does not crash an older debugger.
- Keep changes minimal: avoid unrelated files unless needed for the stated task.

---

## Code quality (non-negotiables)

### Performance and allocations

- The debugger receives data many times per second. **Do not allocate needlessly in hot paths** such as message
  decoding, panel updates, graph drawing, and log ingestion. Prefer reuse, spans, and batched collection updates
  (for example `RangeObservableCollection`) over one change notification per item.
- Never block the UI thread. Network and `adb` work stays async, and results are marshalled back through
  `IUiScheduler`.
- **Avoid reflection** where a normal API works, and do not use deprecated APIs. If one *is* used, replace it with the
  modernized, recommended version instead.

### Language and style

The build enforces most of these (warnings are errors, analyzers at `latest-recommended`, and `.editorconfig`
style rules run during the build). See [CONTRIBUTING.md](CONTRIBUTING.md) for the full list.

- Target **C# latest on .NET 10**, with nullable reference types and implicit usings turned on. Prefer modern features
  (records, pattern matching, switch expressions, collection expressions, primary constructors where they read well).
- File-scoped namespaces (`namespace Foo;`), with `using` directives outside the namespace and `System` first.
- Naming: `PascalCase` for types, members, and constants; `I` prefix for interfaces; `_camelCase` for private fields;
  `camelCase` for locals and parameters.
- Use `var` everywhere the compiler allows it.
- Always write accessibility modifiers, and make fields `readonly` when they can be.
- Do **not** silence a warning without a short comment explaining why.
- Do not use fully qualified type names inline when a normal `using` would read cleanly.
- Prefer **short** member names. If shortening a name hides its meaning, use a concise name plus an XML doc comment
  instead of a long identifier.

### Formatting

- **Indentation:** 2 spaces in C#, XAML, XML, JSON, and YAML. A continuation line (wrapped arguments, a long
  condition, a chained call) is indented 4 spaces deeper than the line it continues.
- **Line length:** at most **100 characters** in `.cs` and `.axaml` files, comments and XML docs included. CI fails on
  longer lines, and `dotnet format` does **not** wrap them, so wrap them by hand:
  - C#: break after the opening parenthesis and put the arguments on the next line, or one per line when they still
    do not fit. Break a long condition after `&&` or `||`, put `?` and `:` at the start of their lines, and split
    long strings into `"..." +` pieces.
  - XAML: put each attribute of a long element on its own line, aligned under the first attribute.
  - XML docs: a long one-line `<summary>`, `<param>`, or `<returns>` becomes the three-line form.
- **Braces:** Allman style (opening brace on a new line). An empty body stays on its declaration's line and is written
  `{ }` with one space, for example `public Foo() { }`.

### Finishing work

Before finishing a coding task, run:

1. Build: `dotnet build`
2. Tests: `dotnet test`
3. Formatting: `dotnet format`, then `dotnet format --verify-no-changes`
4. Line length: make sure no `.cs` or `.axaml` line is longer than 100 characters (the same check CI runs):
   `git ls-files -z '*.cs' '*.axaml' | LC_ALL=C.UTF-8 xargs -0 grep -nP '^.{101,}$'` should print nothing.

Additionally, if you are currently on a branch for a pull request, always update the description of the PR to ensure
accuracy after completing a task.

---

## Tests

- Tests live in `tests/` and use **xUnit v3** on Microsoft.Testing.Platform. App tests use Avalonia Headless and the
  fakes in `tests/FlixelDebugger.App.Tests/Fakes/`.
- Add tests for every fix and feature. Prove the bug first, then the fix.
- The protocol fixtures in `tests/FlixelDebugger.Protocol.Tests/Fixtures/` are generated by the framework
  (`flixelgdx-test/src/test/resources/org/flixelgdx/debug/remote/fixtures/` in the framework repository). **Never edit
  them by hand.** When the protocol changes, re-copy the whole folder from the framework.

---

## Documentation, comments, and XML docs

Documentation should read like a **beginner-friendly handbook**, not an expert-only manual.

### Mechanics

- Every public API in `src/` needs an XML doc comment. A missing one is a build error (CS1591). Test projects are exempt.
- Use correct grammar and punctuation everywhere (comments, `<summary>`, `<param>`, `<returns>`, `<exception>`, and
  so on).
- Stick to **ASCII** in code comments and XML docs; avoid decorative punctuation like en dash, em dash, fancy quotes,
  fancy arrows, or emojis. Use a plain hyphen (-) only for compound adjectives; never use it as a sentence separator
  or stand-in for an em dash. This rule also applies to comments in build files and workflows. The Markdown docs are
  the only exception to this rule.
- Every doc comment starts with a **single summary sentence**, with detailed paragraphs following.
- Include the right XML doc tags wherever they apply, and keep `<see cref="..."/>` references valid. Use a `using`
  rather than a fully qualified name inside `cref` so the docs stay readable.
- Every `.cs` file carries the MIT license header from `.editorconfig` (`file_header_template`). `dotnet format` adds
  it for you.
- Use **American English** in docs (for example "behavior" instead of "behaviour").
- Comment code where the reason is not obvious. Do not restate what the code already says.
- Don't use section comments (like `// ---` or `#region`). The code should be easy to navigate simply by how it's
  organized.
- After code changes that affect behavior the user can see, **update [README.md](README.md)** (and
  [CONTRIBUTING.md](CONTRIBUTING.md) if a convention changed).

### Comments versus XML docs

- For single-line comments, do **not** use Markdown tricks (bold with `**`, backticks around snippets, etc.).
- When naming methods inside comments, include parentheses (`SomeMethod()`). If parameters exist, but you are not
  spelling them out, use `AnotherMethod(...)`. Example class-qualified form: `SomeClass.SomeMethod(...)`.

### Heavily used or critical APIs

For widely used types and members, or anything central to correctness, include a **small usage example**
(`<example>` with `<code>`) showing correct typical use.

---

## Project housekeeping

- `FlixelDebugger.slnx` must list every project. Add new ones with `dotnet sln FlixelDebugger.slnx add <project>`.
- Package versions live in `Directory.Packages.props` (central package management). Project files reference packages
  **without** a version.
- Builds use a single worker (`Directory.Build.rsp`) on purpose. Do not remove it.

---

## Working with Git and Pull Requests

- Prefer **small, focused commits** as you finish logical slices of work so history stays readable. For example, if the
  task involves a large refactor, **don't dump everything in one commit**; split each logical change into a separate
  commit.
- Use **one branch and one pull request** unless the user explicitly asks for more (for example, stacked features or
  dependent work).
- If the user renames a pull request, **do not rename it back**; respect their title.
- Your commit titles should be **short and descriptive**, not exceeding **72 characters**, and should not contain
  **keywords in front (e.g. `fix`, `feat`, `refactor`, etc.)**. They should also be **present** tense. Examples:
    - "Add property value encoding"
    - "Fix string truncation on surrogate pairs"
    - "Update README with connection steps"
- Always pull the latest changes before changing any code if on a branch outside of the `master` branch.
- If the current branch is set to `master` or something else, **create a new branch off of the latest changes from `master`**.
- When you're done with a task (and you haven't yet made one), **create a pull request**. Make sure it follows the
  [PR template](.github/PULL_REQUEST_TEMPLATE.md) exactly with all of your changes.
- Pull request titles should be read as **past tense**, in the format as if it was a new update to a game. Examples:
    - "Added a logs panel with level filtering"
    - "Reworked the connection handling to survive game restarts"
- All pull requests should target the **`master`** branch.
- If the user has changes present on the current branch, **do not undo, modify or touch them**. Leave them as-is.
