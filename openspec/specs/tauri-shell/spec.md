# tauri-shell Specification

## Purpose
TBD - created by archiving change extract-frontend-core. Update Purpose after archive.
## Requirements
### Requirement: Tauri desktop frontend with a bundled C# sidecar
A new Tauri (Rust shell + React/TypeScript webview) desktop app SHALL bundle `cdeApi` as a sidecar
binary, spawn it on launch, and terminate it on exit. The webview SHALL communicate with the sidecar
over loopback HTTP/SSE using the startup-handshake token.

#### Scenario: Sidecar lifecycle bound to the app
- **WHEN** the Tauri app launches
- **THEN** it starts the `cdeApi` sidecar on loopback and obtains its port and handshake token
- **AND** when the Tauri app exits, the sidecar process is terminated

#### Scenario: Webview authorized via handshake token
- **WHEN** the React webview calls the sidecar
- **THEN** it includes the startup-handshake token provided by the Tauri shell

### Requirement: Feature parity with the WinForms app
The Tauri frontend SHALL provide functional parity with `cdeWin`: a catalog directory tree pane, a
directory listing view, a search box with advanced filters (regex; include path; files/folders;
result limit; From/To size; From/To date; From/To hour; not-older-than), a search results view, a
catalog list, and a status bar (results count, entries loaded, catalogs loaded, memory, timing).

#### Scenario: Advanced search parity
- **WHEN** a user runs a search with advanced filters in the Tauri frontend
- **THEN** results match what `cdeWin` returns for the same query and loaded catalogs

#### Scenario: Tree navigation and directory listing
- **WHEN** a user expands a catalog tree node and selects a directory
- **THEN** the directory's entries are listed, equivalent to the WinForms directory view

#### Scenario: Live streaming results
- **WHEN** a long search runs
- **THEN** results appear incrementally with progress feedback, and the user can cancel it

### Requirement: Context-menu actions
The Tauri frontend SHALL offer the context actions present in `cdeWin`. The OS shell operations —
open, explore, properties, and any configured custom commands — SHALL be invoked via the
capability-guarded sidecar shell endpoint (which runs core `IShellActions`). The remaining actions —
copy full path, select all, view-in-tree, and go-to-parent — SHALL be implemented frontend-natively
and SHALL NOT go through `IShellActions`.

#### Scenario: Open and explore via the sidecar
- **WHEN** a user invokes open or explore on a result row
- **THEN** the frontend calls the sidecar shell endpoint with the entry reference
- **AND** the operation runs against the resolved local path, equivalent to `cdeWin`

#### Scenario: Configured custom commands appear and run via the sidecar
- **WHEN** custom commands are configured
- **THEN** each appears as its own context-menu item
- **AND** invoking one calls the sidecar shell endpoint with the command id + entry reference

#### Scenario: Shell actions only offered for locally-present entries
- **WHEN** a result references an entry not present on the local filesystem (portable catalog)
- **THEN** the OS shell actions are hidden or disabled for that entry

#### Scenario: Copy full path is frontend-native
- **WHEN** a user invokes copy-full-path on one or more selected rows
- **THEN** the webview places the path string(s) on the clipboard without calling the shell endpoint

#### Scenario: Select-all, view-in-tree, and parent are frontend-native
- **WHEN** a user invokes select-all, view-in-tree, or go-to-parent
- **THEN** the action is handled within the frontend (selection/navigation state) with no shell call

### Requirement: UI state via the API; window geometry native
The Tauri frontend SHALL retrieve and persist its UI state (column configs/widths, search history,
filter preferences, panel ratios, last view) through the API's `/ui-state` endpoints rather than
webview local storage. The OS window size/position SHALL be managed natively by the Tauri shell, not
through the API.

#### Scenario: UI state round-trips through the API
- **WHEN** the frontend starts and when the user changes a persisted preference
- **THEN** it reads initial state from `GET /ui-state` and persists changes via `PUT /ui-state`
  (debounced, flushed on close)

#### Scenario: Window placement persists via the Tauri shell
- **WHEN** the user moves or resizes the window and relaunches
- **THEN** the window position/size is restored by the Tauri shell, independent of `/ui-state`

