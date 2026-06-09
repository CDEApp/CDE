## 1. Config model

- [x] 1.1 Add a `CustomCommandOptions { Label, Command, Arguments }` model and a `CustomCommands` list binding; update `appsettings.json` shape.
- [x] 1.2 Replace `ExplorerAltOptions` usage; on startup, migrate an existing `ExplorerAlt` setting into a seeded `CustomCommands` entry (default label, e.g. "Explore Alt").

## 2. Substitution + launch

- [x] 2.1 Implement a token-map substituter for `{path}` (full path+name), `{dir}` (`Path.GetDirectoryName`), `{filename}` (`Path.GetFileName`) — literal, no auto-quoting, extensible for more tokens later.
- [x] 2.2 Generalize `WindowsExplorerUtilities.ExplorerAltExplore` into `RunCustomCommand(command, argsTemplate, fullPath)`; keep the `Win32Exception` log + message-box handling.

## 3. Dynamic context menus

- [x] 3.1 Extend `ContextMenuHelper` with a dynamic block: append one `ToolStripMenuItem` per configured command (definition in `.Tag`), all routed to a single click handler.
- [x] 3.2 Build the custom items into the search-result, directory-tree, and directory-list context menus in `CDEWinForm.cs`.
- [x] 3.3 Remove the fixed `_exploreAlt` item, its `Ctrl+T` exclusivity, and the `On*ExploreAltClick` events from the view interface and form.

## 4. Presenter wiring

- [x] 4.1 Add a single presenter handler that receives the clicked command (from `.Tag`) + the single selected entry, applies the `ExistsOnFileSystem` gate, substitutes `{path}`/`{dir}`/`{filename}`, and launches once (reuse the existing `ActionOnSelectedItem` + `ThatExists` single-selection path).
- [x] 4.2 Remove the now-dead `*ContextMenuExploreAltClick` presenter handlers.

## 5. Verify

- [x] 5.1 Manual: configure 2+ commands; confirm each appears and launches with correct `{path}`/`{dir}`/`{filename}` substitution (including a quoted-token path with spaces).
- [x] 5.2 Manual: migrated Explore Alt appears as a named command and behaves as before.
- [x] 5.3 Manual: single selection launches; absent-locally entry does not launch; multi-selection does not fan out; missing-exe failure is logged + surfaced, app stays up.
- [x] 5.4 Run `cdeWinTest`; update any tests referencing the removed Explore Alt events/handlers.

## 6. Cross-reference

- [x] 6.1 Confirm the forward-reference note is present in `extract-frontend-core` (the list moves to `cdeAppCore` `IShellActions`; `/shell` action becomes a command id; server owns definitions + substitution).
