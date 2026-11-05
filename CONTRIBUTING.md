# Contributing

Thanks for helping with the FlixelGDX Debugger! This page lists the conventions the project follows.
The build enforces most of them, so you will find out quickly if something is off.

## Setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Clone the repository and run `dotnet build` and `dotnet test` to check everything works.

Before you push, run all three and make sure they pass:

```sh
dotnet build
dotnet test
dotnet format --verify-no-changes
```

`dotnet format` (without `--verify-no-changes`) fixes most style problems for you.

## Automated checks

The **CI** workflow (`.github/workflows/ci.yml`) runs the same three commands on every pull request, so a
failing build, test, or style check shows up right on the pull request. If the format job fails, run
`dotnet format` locally and commit the result.

Releases are built by the **Release** workflow (`.github/workflows/release.yml`). Maintainers cut one by
pushing a tag such as `v0.1.0`; the workflow packages the app for Linux, Windows, and macOS and attaches
the zips to a GitHub Release.

## Code conventions

- **Language:** C# on .NET 10 with nullable reference types and implicit usings turned on.
- **Warnings are errors.** Analyzers run at `latest-recommended`, and style rules from `.editorconfig`
  run during the build. Do not silence a warning without a short comment explaining why.
- **Namespaces:** file-scoped (`namespace Foo;`).
- **Naming:** `PascalCase` for types, members, and constants; `I` prefix for interfaces; `_camelCase`
  for private fields; `camelCase` for locals and parameters.
- **`var`:** use `var` everywhere the compiler allows it.
- **Indentation:** 4 spaces in C#, 2 spaces in XML, JSON, and YAML. Files end with a newline and use
  `\n` line endings.
- **License header:** every `.cs` file starts with the MIT header from `.editorconfig`
  (`file_header_template`). `dotnet format` adds it for you.
- **Solution file:** `FlixelDebugger.slnx` must list every project. Use
  `dotnet sln FlixelDebugger.slnx add <project>`.
- **Packages:** versions live in `Directory.Packages.props` (central package management). Project files
  reference packages without a version.

## Documentation and comments

- Every public API in `src/` needs an XML doc comment. A missing one is a build error (CS1591).
- A doc comment starts with **one summary sentence**. Details and examples come after it.
- Write for beginners: explain *why* before *how*, and use a small example when an API is central.
- Use **American English** and plain **ASCII** in code comments and XML docs: no em or en dashes, fancy
  quotes, or emojis. Markdown files may use normal Markdown.
- Comment code where the reason is not obvious. Do not restate what the code already says.

## Tests

- Tests live in `tests/` and use xUnit v3. Test projects may skip XML docs (CS1591 is suppressed there).
- Add tests for every fix and feature. Prove the bug first, then the fix.
- The protocol fixtures in `tests/FlixelDebugger.Protocol.Tests/Fixtures/` are copied from the framework
  repository. Never edit them by hand: re-copy the folder when the protocol changes.

## Commits

- Keep commits **small and logical**: one idea per commit, so history reads like a story.
- Titles are in the **present tense**, under **72 characters**, with no `feat:` or `fix:` style prefixes.
  Examples:
  - `Add property value encoding`
  - `Fix string truncation on surrogate pairs`
  - `Update README with connection steps`

## Pull requests

- Open pull requests against `master`, from a branch of your own.
- Fill in the [pull request template](.github/PULL_REQUEST_TEMPLATE.md).
- PR titles are written in the **past tense**, like the notes of a game update:
  - `Added a logs panel with level filtering`
  - `Reworked the connection handling to survive game restarts`
- Keep the description accurate: update it if the work changes while the PR is open.
- Make sure `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` pass.
