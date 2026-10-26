## Why

The WinForms app (`cdeWin`) supports a single, fixed "Explore Alt" context-menu action backed by one
`ExplorerAltOptions { Path, Arguments }` setting. Users who want to launch more than one external tool
against a catalogued file/folder (e.g. Total Commander, an editor, a hashing tool) have no way to do
so — they get exactly one slot.

We want users to configure **any number of custom commands** that appear in the file/folder context
menus. Each command provides a display label, an executable, and an arguments template with a
`{path}` placeholder that is substituted with the selected entry's full path before the app launches
it. This generalizes the existing single-slot Explore Alt into an open-ended, user-driven list.

This is independent of the larger `extract-frontend-core` refactor, but the two meet at the shell
boundary; this change deliberately reuses the same path-resolution and existence-guard semantics so
the refactor can absorb the generalized model cleanly.

## What Changes

- **Generalize Explore Alt into a list.** Replace the single `ExplorerAltOptions` with a
  `CustomCommands` list, each entry `{ Label, Command, Arguments }`. The previous Explore Alt setting
  is migrated into the list on first run; Explore Alt as a separate, hard-coded mechanism is removed.
- **Dynamic context menus.** The file/folder context menus (search results, directory tree, and
  directory list where shell actions apply) list every configured custom command by its `Label`,
  built dynamically rather than from a fixed compile-time item. All custom items route through a
  single handler.
- **Token substitution.** The arguments template substitutes `{path}` (full path + name), `{dir}`
  (containing directory), and `{filename}` (leaf name) from the selected entry. The token map is
  extensible for further tokens later without a format change. The user's template controls quoting
  (e.g. `"{path}"` for paths with spaces).
- **Single-selection launch semantics.** A command operates on a single selected entry only (no
  multi-select fan-out), and only when that entry exists on the local file system (the existing
  `ExistsOnFileSystem` gate). Launch failures are logged and surfaced without crashing, matching
  current Explore Alt behaviour.

## Capabilities

### New Capabilities
- `custom-shell-commands`: A user-configurable list of external commands surfaced in the file/folder
  context menus, each launched with a `{path}`-substituted arguments template against the selected
  entry; supersedes the single Explore Alt slot.

### Modified Capabilities
<!-- No existing OpenSpec specs to modify. -->

## Impact

- **Modified:** `cdeWin/Cfg/ExplorerAltOptions.cs` (replaced by a `CustomCommandOptions` /
  `CustomCommands` model), `ContextMenuHelper.cs` (dynamic custom-command items), `CDEWinForm.cs`
  (build dynamic items into the three context menus), `CDEWinFormPresenter.cs` (single routed handler;
  remove the fixed `*ExploreAltClick` handlers), `WindowsExplorerUtilities.cs` (generalize
  `ExplorerAltExplore` into a `RunCustomCommand(command, args)`), `appsettings.json` (config shape).
- **Migration:** an existing `ExplorerAlt` setting is read once and seeded as a `CustomCommands` entry.
- **Removed:** the fixed `ExploreAlt` menu item, its `Ctrl+T` binding's exclusivity (a migrated
  command may reclaim it), and the `On*ExploreAltClick` events.
- **Behaviour:** users with no config see no custom items (today they see a non-functional Explore Alt
  when unconfigured); users with an Explore Alt configured see it as a named custom command.
- **Cross-reference:** `extract-frontend-core` — when that refactor lands, this list moves into
  `cdeAppCore`'s `IShellActions` and the API `/shell` action becomes a *command id*; the server owns
  the command definitions and performs substitution, so the client never supplies a command line.
- **Security note:** custom commands are arbitrary local executables configured by the user — by
  design, same trust model as the current Explore Alt. No command line is ever accepted from a remote
  or untrusted source.
