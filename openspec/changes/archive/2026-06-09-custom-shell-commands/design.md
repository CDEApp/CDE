## Context

`cdeWin` builds its context menus from a fixed, named set of handlers in `ContextMenuHelper` (a
hard-coded `_exploreAlt` item, etc.), each wired to a compile-time event on the view
(`OnSearchResultContextMenuExploreAltClick`, `OnDirectoryTreeContextMenuExploreAltClick`) via the
reflection-based passive-view convention. The single Explore Alt action reads
`ExplorerAltOptions { Path, Arguments = "/O /T /R={path}" }` from `appsettings.json` via the
`Program.Configuration` static and launches it with `{path}`-style substitution
(`Arguments.Replace("{path}", path)` today) under an `ExistsOnFileSystem` guard.

The goal is to let users configure an arbitrary number of such commands, each appearing as its own
context-menu item.

## Goals / Non-Goals

**Goals:**
- A user-configured list of commands (`Label`, `Command`, `Arguments`) surfaced in the file/folder
  context menus.
- `{path}` substitution into the arguments template, with the user controlling quoting.
- Generalize (migrate) the existing single Explore Alt into the list; one mechanism, not two.
- Preserve current launch semantics: per-selected-entry, existence-gated, fail-soft.

**Non-Goals:**
- No change to the larger `extract-frontend-core` refactor (this works on current WinForms); only a
  documented forward-reference at the shell seam.
- No remote/untrusted command execution; commands are user-configured local executables.
- No new tokens beyond `{path}` in this change (the substituter is built to add them later).
- No per-command menu-scoping UI (a command appears in all applicable menus); scoping can come later.

## Decisions

### CSC1. Generalize Explore Alt into a `CustomCommands` list (migrate, don't coexist)
The single `ExplorerAltOptions` is replaced by `CustomCommands: [{ Label, Command, Arguments }]`.
Explore Alt as a distinct, hard-coded action is removed. On first run, an existing `ExplorerAlt`
setting is read and seeded as one `CustomCommands` entry (default label e.g. "Explore Alt") so no user
loses their configured tool. Rationale: one mechanism is simpler to reason about and is the clean
model the frontend refactor will inherit; coexistence would leave two overlapping shell-launch paths.

### CSC2. Three substitution tokens via a small extensible map
The arguments template supports three tokens, replaced from the selected entry's resolved full path:

| Token | Value | Example (entry `C:\dir\sub\file.txt`) |
|---|---|---|
| `{path}` | full path + name | `C:\dir\sub\file.txt` |
| `{dir}` | containing directory | `C:\dir\sub` |
| `{filename}` | leaf name | `file.txt` |

For a folder entry the same `Path.GetDirectoryName` / `Path.GetFileName` split applies (`{path}` = the
folder's full path, `{dir}` = its parent, `{filename}` = the folder name). Replacement is literal and
case-sensitive via a token→value map, so further tokens can be added later without a format change.
Brace style is chosen over `%PATH%` because it matches the existing config convention and does not
visually collide with the Windows `PATH` environment variable. (Launch uses `Process.Start(Command,
args)` with `UseShellExecute=false`, so no cmd-level `%VAR%` expansion occurs regardless.)

### CSC3. The user's template controls quoting
Substitution is literal; the app does not auto-quote. A template targeting a path with spaces must
quote the token itself (`"{path}"`). This matches current Explore Alt behaviour and avoids guessing
quoting rules per target tool. The default seeded/example config demonstrates a quoted token.

### CSC4. Dynamic context-menu items via a single routed handler
`ContextMenuHelper` gains a dynamic block: given the `CustomCommands` list it appends one
`ToolStripMenuItem` per command, each carrying its definition in `.Tag`, all wired to a single click
handler. This is a deliberate, contained exception to the one-event-per-item reflection convention —
N user-defined commands cannot be compile-time events. The fixed `_exploreAlt` item and the
`On*ExploreAltClick` events are removed.

### CSC5. Single-selection launch semantics
A custom command operates on a **single** selected entry only (no multi-select fan-out). It is gated
by `ExistsOnFileSystem` (offered only for locally-present files/folders — catalogs are portable). When
no single entry is selected (or it does not exist locally), the command does not launch.
`Win32Exception` on launch is logged and surfaced via a message box without crashing, exactly as
`ExplorerAltExplore` does today. Mirrors the existing Explore Alt, which already acts on a single
existing entry (`ActionOnSelectedItem` + `ThatExists`).

### CSC6. Config location follows the host
In current WinForms the list lives in `cdeWin`'s `appsettings.json` (read where Explore Alt is read
today). This is intentionally aligned with `extract-frontend-core` D12: when the refactor lands, the
list moves to the shared user-scoped store (`%APPDATA%\cde\shell.json`) owned by `cdeAppCore`/the
sidecar. This change does not block on that move.

### CSC7. Forward-reference to `extract-frontend-core`
At the shell seam, the generalized list is the model the refactor absorbs: `IShellActions` exposes the
command list, and the API `/shell` action becomes a *command id* (not `ExploreAlt`). The server owns
the command definitions and performs `{path}` substitution; the client supplies only a command id +
entry reference, preserving the D11 capability guard. Captured as a note in that change's design.

## Risks / Trade-offs

- **Arbitrary local execution:** by design users configure executables that the app launches. Same
  trust model as today's Explore Alt; no remote input. Document clearly.
- **Quoting footgun:** literal substitution means an unquoted `{path}`/`{dir}` with spaces breaks for
  some tools. Mitigation: quoted token in the default/example, and a note in config docs.
- **Shortcut keys:** the fixed Explore Alt `Ctrl+T` is freed. Per-command shortcut assignment is out
  of scope; a migrated command does not automatically claim `Ctrl+T`.
- **Rework at the seam:** implementing in current WinForms then again in `cdeAppCore` is minor
  duplication; CSC6/CSC7 keep the model identical so the refactor is a move, not a redesign.

## Open Questions

- **Per-command menu scope:** should a command be limited to files vs folders, or to specific panes
  (results/tree/list)? Default for this change: appears in all applicable menus. Scoping deferred.
