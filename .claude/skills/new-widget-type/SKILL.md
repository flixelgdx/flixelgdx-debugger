---
name: new-widget-type
description: Add support for a new panel widget type, widget property, or feed to the debugger, end to end - protocol decoding, Core state, and the App renderer. Use when the framework's DEBUG_PROTOCOL.md gains a widget type or feed the debugger cannot draw yet.
argument-hint: "<widget type or feed name>"
---

# New widget type or feed

Panels are built by the game. The debugger never decides what a panel contains: it decodes the widget tree the game sends, draws each widget generically, and sends back what the person does as panel events. So supporting a new widget type means teaching every layer to carry and draw it, and supporting a new feed means teaching every layer to decode and show it.

## Steps

1. **Read the spec.** Find the type in the framework's `DEBUG_PROTOCOL.md` (under "Widgets" or "Feeds"): its type ID, properties, encoding, and the events it can send. Check that the fixtures already contain it; if not, run the `protocol-sync` skill first.
2. **Protocol layer** (`src/FlixelDebugger.Protocol`): add the type ID and decoding in `Ui/` (widgets) or `Feeds/` (feeds). Unknown types must still decode to `Unknown*`. Add round-trip tests against the fixture.
3. **Core layer** (`src/FlixelDebugger.Core`): store the state and apply `PANEL_PATCH` updates to it without UI types. Batch updates and avoid allocating per message, since data arrives many times a second.
4. **App layer** (`src/FlixelDebugger.App`): add a view or control in `Widgets/` and register it in `WidgetFactory`. Apply the common style properties through `WidgetStyle`. Send events back through the existing panel event path. ViewModels go through the service interfaces, never Avalonia controls directly.
5. **Document it** in the README if it lists supported widgets or feeds.
6. **Run checks:** `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes`.

Report the files added or changed per layer and anything the spec left unclear.
