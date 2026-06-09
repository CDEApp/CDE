## Context

`cdeWin` is a WinForms app using a Passive-View MVP pattern: `CDEWinFormPresenter` drives an
`ICDEWinForm` view interface, with events auto-wired by reflection (`Presenter<TView>`). The seam
is conceptually right but the wrong *shape* — the view contract and presenter are saturated with
WinForms types (~221 references to `TreeNode`/`ListViewItem`/`SortOrder`/`Color`/`DialogResult`/
`BackgroundWorker`). The domain leaks too: `cdeLib.FindOptions` holds a `BackgroundWorker` and
polls `Worker.CancellationPending` inside the search hot loop.

Catalogs are loaded as zero-copy, memory-mapped `.cdex` columnar files via `ColumnarCatalogReader`
(exposed as `ICommonEntry`/`EntryRef`). "Loading" a catalog is mmap-ing it: the working set is OS
page cache, not managed heap. This is the app's core performance property and must be preserved.

The goal is a second frontend — **Tauri (React/TypeScript) + C# API** — alongside the existing,
fully-maintained `cdeWin`. There is no WinUI3 / shared-.NET-view-model path; the second frontend is
JavaScript, so the shared boundary must be **serializable data over a wire**.

## Goals / Non-Goals

**Goals:**
- One engine, two surfaces: `cdeWin` (in-process) and Tauri/React (via a localhost C# API) call the
  *same* application services with the *same* DTO boundary.
- Keep `cdeWin` working and feature-complete throughout; steps 1–3 are pure refactors.
- Preserve zero-copy mmap performance for the desktop app (no HTTP/serialization on `cdeWin`'s path).
- Remove UI coupling from `cdeLib` (headless, testable search engine).
- Feature parity for the Tauri frontend (tree, dir list, advanced search, context actions, catalog
  list, status bar).

**Non-Goals:**
- No WinUI3 frontend.
- No shared observable view-models (`INotifyPropertyChanged`/`ObservableCollection`) in core — a
  React frontend cannot use them; each frontend owns its own state/view-model layer.
- No multi-user / multi-tenant server. The API is a single-user localhost sidecar.
- No change to the `.cdex`/`.cde` on-disk formats or to the `cde` CLI.
- No reuse of WinForms window-geometry config across frontends (window placement is per-frontend).

## Decisions

### D1. Boundary = serializable DTOs + transport-agnostic application services (not view-models)
`cdeAppCore` exposes request/response services over plain DTOs. The same call works in-process
(`cdeWin`) and behind HTTP (`cdeApi`). This is the only boundary that survives contact with React,
and it is identical in-proc and over the wire — so `cdeApi` is a shim, not a second implementation.

### D2. `IAsyncEnumerable<SearchResultRow>` is the unifying streaming primitive
`ISearchService.SearchAsync(SearchQuery, IProgress<SearchProgress>, CancellationToken)` returns
`IAsyncEnumerable<SearchResultRow>`. `cdeWin` consumes it in-process with `await foreach`, updating
the ListView as rows arrive (matches today's live-updating search). `cdeApi` maps the same stream to
**Server-Sent Events** (`result`/`progress`/`done`). Client disconnect cancels the token. Streaming,
progress, and cancellation fall out of one contract on both sides; paged pull (`Skip`/`Take`) remains
available later without changing the signature.

### D3. The catalog *session* is an explicit, ownable, disposable resource
`ICatalogSession` (load → query tree/dir/search → dispose) replaces the presenter's implicit
`List<ICommonEntry>` field. In-process it is an app-lifetime singleton; in the API the **sidecar
process is the session** and holds the mmap. At the DTO boundary only the visible page of result
rows is materialized (a small copy that must happen anyway to serialize); the catalog never
serializes. Same abstraction serves desktop singleton and server session with no rework.

### D4. WinForms stays in-process (do NOT route `cdeWin` through the API)
Routing `cdeWin` through localhost HTTP/JSON would regress the zero-copy mmap advantage that is the
app's reason to exist. `cdeWin` references `cdeAppCore` directly. Both `cdeWin` and `cdeApi` call the
same services with the same DTO boundary — which is what proves the contract is transport-agnostic.

### D5. Transport = localhost HTTP/JSON + SSE (chosen over Tauri stdio IPC)
HTTP is debuggable with `curl`, language-agnostic, and SSE gives streaming for free. Bind Kestrel to
`127.0.0.1` on an ephemeral port; require a startup-handshake token (passed from the Tauri shell to
the webview) so nothing else on the box can call it. JSON via `System.Text.Json` source-generated
serializers (AOT-friendly, fast).

### D6. C# backend ships as a Tauri sidecar
The Tauri (Rust) shell bundles `cdeApi` as a self-contained binary, spawns it on launch (Kestrel on
loopback), and kills it on exit. React (webview) talks to it over HTTP/SSE. The mmap catalogs live in
the sidecar process; React only ever sees DTO pages.

### D7. `FindOptions` UI decoupling is thorough
Remove `BackgroundWorker Worker` from `cdeLib.FindOptions`. Unify the sync and async paths on
`CancellationToken` for cancellation and `IProgress<SearchProgress>` (or an internal progress
callback) for progress. This is the correct headless end state; the async path already takes a token.
The change touches the search inner loop and must be benchmarked.

### D8. Each frontend wires its own UI; reflection event-wiring stays WinForms-only
`Presenter<TView>` and the `On*` reflection convention remain in `cdeWin`. The Tauri frontend uses
React idioms. Core holds no event-wiring machinery.

### D9. Layering / dependency direction
```
cdeLib  ◀─  cdeAppCore  ◀─  cdeWin   (in-proc)
                        ◀─  cdeApi   (HTTP/SSE)  ◀─  cde-tauri/React (loopback)
```
`cdeAppCore` depends only on `cdeLib`. It must not reference WinForms, ASP.NET, or any frontend.

### D10. `IShellActions` is narrowed to four OS operations; the rest are frontend-native
The WinForms context menu mixes three categories; only the first is a shell action:

| Action | Today | Category | Home |
|---|---|---|---|
| Open | `ProcessStart` (`UseShellExecute`) | OS shell op | `IShellActions` (core) |
| Explore | `explorer.exe /select` | OS shell op | `IShellActions` (core) |
| Properties | `ShellExecuteEx` `"properties"` verb | OS shell op | `IShellActions` (core) |
| Custom commands (list) | user-configured exe + args template, `{path}`/`{dir}`/`{filename}` tokens | OS shell op | `IShellActions` (core) |
| Copy Full Path | `Clipboard.SetText` | client clipboard | frontend-native |
| Select All | `ListView.SelectAllItems` | pure view state | frontend-native |
| View in Tree | switch tab + expand to entry | in-app navigation | frontend-native |
| Parent | navigate up a directory | in-app navigation | frontend-native |

> Updated after the shipped `custom-shell-commands` change: the former single "Explore Alt" action
> is now a **dynamic list of user-configured custom commands** (`CustomCommandOptions` +
> `CommandTokens` + `WindowsExplorerUtilities.RunCustomCommand`, with `{path}`/`{dir}`/`{filename}`
> token substitution). This refactor *lifts that already-generalized model into core*, it does not
> re-generalize `ExplorerAlt`.

- **`IShellActions` = `Open`, `Explore`, `ShowProperties` (fixed OS verbs) + a custom-command
  facility** — the configured `CustomCommandOptions` list plus a `RunCustomCommand(command, path)`
  that performs `{path}`/`{dir}`/`{filename}` substitution. It lives in `cdeAppCore` as an interface
  plus a **Windows implementation** lifted from `WindowsExplorerUtilities`/`CommandTokens`,
  de-static-ified: the custom-command config and the logger become injected dependencies (removing
  the `Program.Configuration` static). The feature is Windows-specific; non-Windows hosts get a
  no-op / `NotSupported` implementation.
- **Clipboard, select-all, view-in-tree, and parent are NOT shell actions** and are explicitly out
  of `IShellActions`. Core supplies only the path strings; each frontend implements these natively
  (cdeWin → `Clipboard`/`ListView`/navigation; Tauri → webview clipboard / React state / routing).
- **Custom commands are addressed by id over the wire.** `IShellActions` exposes the configured
  command list so frontends can render menu items; the API `/shell` `action` for a custom command is
  a **command id** (not a command line). The server owns the command definitions and performs token
  substitution server-side, so the client supplies only a command id + entry reference — preserving
  the D11 capability guard.

### D11. Shell actions execute in the host that owns the local filesystem; the API is capability-guarded
Catalogs are portable and may describe files not present on this machine, so every shell action is
gated by `ExistsOnFileSystem`. The path resolution, existence check, and custom-command definitions
all live in the session/host.

- **Execution host:** for `cdeWin`, `IShellActions` runs in the app process; for Tauri, it runs in
  the **sidecar** (same machine, same interactive user session) — reusing the exact C# code and the
  session it already holds. It is **not** reimplemented in Rust/Tauri's shell plugin (which lacks a
  "show properties" verb and would drift from parity). `ShowProperties` already passes
  `hwnd = IntPtr.Zero` today, so running it headless in the sidecar is behaviourally identical.
- **Capability guard:** the shell endpoint takes a **session entry reference** (catalog + entry
  indices), never a client-supplied path. The server resolves `FullPath` from the session and
  re-checks `ExistsOnFileSystem` before invoking `IShellActions`. This prevents the loopback endpoint
  from becoming a "run `ShellExecute` on any path" gadget. `IShellActions` itself stays path-based;
  the resolve-and-guard lives at the API boundary.

### D12. Custom-command config: types in core, value in a shared user-scoped file
The shipped `custom-shell-commands` change put `CustomCommandOptions` (`Label` / `Command` /
`Arguments`) and the `CommandTokens` substituter in `cdeWin`, reading the `CustomCommands` list from
`cdeWin`'s `appsettings.json`. This refactor moves those types into `cdeAppCore` and relocates the
value to a shared store. Two layers are kept distinct:

- **Type / contract:** `CustomCommandOptions` (+ `CommandTokens`) live in `cdeAppCore`. Both hosts
  bind the `CustomCommands` list and inject it (as `IReadOnlyList<CustomCommandOptions>` /
  `IOptions<>`).
- **Source-agnostic core:** `cdeAppCore` MUST NOT read configuration. It never references
  `Microsoft.Extensions.Configuration`, `appsettings.json`, or a static like `Program.Configuration`.
  Each host sources the list and injects it.
- **Runtime value location:** a single **user-scoped, update-stable** file
  (`%APPDATA%\cde\shell.json`, or a shared `cde.user.json` with a `CustomCommands` section), loaded by
  **both** `cdeWin` and `cdeApi` via a layered `ConfigurationBuilder` — shipped `appsettings.json`
  provides defaults, the user file overrides. Single source of truth, configure-once across
  frontends. (The legacy `ExplorerAlt` → `CustomCommands` migration already shipped in
  `custom-shell-commands`; it can run once in whichever host loads config.)

Rationale: custom commands are per-user "preferred external tools" choices, not app-instance
settings — so they should be shared by all frontends and survive app updates. Both hosts'
`appsettings.json` are update-fragile (cdeWin's is `CopyToOutputDirectory=Always`; cdeApi's ships
inside the Tauri bundle, possibly read-only / replaced on update), so user-editable prefs must not
live there. They are also kept out of `cdeWin`'s `cdeWinView.cfg`, which holds per-frontend
window/column geometry (a non-goal for sharing).

### D13. Minimal API surface (9 endpoints, parity-complete)
The `cdeApi` surface is deliberately small; parity comes from a few well-chosen primitives plus
folding several WinForms actions into existing endpoints.

| Endpoint | Method | cdeWin feature it covers |
|---|---|---|
| `/health` | GET | sidecar readiness / handshake check (token-gated) |
| `/session/reload` | POST *(streamed)* | reload catalogs + loading progress (reuses the SSE progress/`done` mechanism) |
| `/catalogs` | GET | catalog list view **+** status bar (catalogs loaded, total entries, memory) |
| `/entries/{ref}/children` | GET | tree lazy-expand (`?foldersOnly`), directory list pane (`?skip&take&sort`), drill-into-dir on activate |
| `/entries/{ref}/path` | GET | view-in-tree **and** go-to-parent (root→entry ref chain, `GetListFromRoot`) |
| `/search` | POST *(streamed)* | search + advanced filters + live results + count + timing + cancel |
| `/shell` | POST | open / explore / properties / run-custom-command-by-id (entry-ref guarded, D11) |
| `/ui-state` | GET | column configs/widths, search history, filter prefs, panel ratios, last view |
| `/ui-state` | PUT | persist UI state |

Folded in (no separate endpoint):
- **Validation** → into `POST /search`: an invalid query returns `400` + the field/regex message
  (core validators) before any stream starts.
- **Search history** → into `/ui-state` (part of the state document; frontend appends, debounced PUT).
- **Directory drill / result-activate** → reuse `/children` and `/path`; activating a file is `/shell`.
- **Select-all, copy-path** → frontend-only, no server call (D10).

`ref` is a single type: catalog id + entry index, resolved by the session. Tree nodes, list rows, and
search results all reference entries the same way.

### D14. UI state is served and persisted by the API (sidecar), not the webview
The Tauri frontend retrieves and persists its UI state via `/ui-state` rather than webview
`localStorage`.

- **Single user-scoped store:** the sidecar persists to `%APPDATA%\cde\ui-state.json` (same pattern as
  `shell.json`, D12). Durable across webview cache clears, backup-able, one file.
- **Core supplies defaults:** `GET /ui-state` returns the user's overrides merged over core's known
  defaults (existing `Default*ColumnCount` / `ColumnConfig`).
- **Per-frontend, not shared:** this is the React app's own document. `cdeWin` keeps its
  `cdeWinView.cfg`; sharing window/column geometry across frontends remains a non-goal. The sidecar
  treats the document as a mostly-opaque per-frontend blob, merging only the column-set defaults it
  knows.
- **Window geometry stays Tauri-native (option b):** the Tauri window's size/position is managed by
  the Tauri window-state plugin (Rust side), not round-tripped through C#. `/ui-state` covers
  app-level state only. (Including geometry in `/ui-state` for a single store was the alternative;
  rejected to avoid fighting the Tauri window plugin for no real benefit.)
- **Write cadence:** PUT on meaningful changes (column-resize end, search committed to history, filter
  pref toggled), debounced, plus a flush on app close — not per keystroke.

## Risks / Trade-offs

- **Hot-path regression risk (D7):** rewriting cancellation/progress in the search loop could slow
  large-catalog scans. Mitigation: benchmark before/after on a representative catalog; keep the
  entry-count-gated cancellation check and ~100ms throttled progress semantics.
- **DTO materialization cost:** the API copies result rows instead of handing out `EntryRef`s into
  mmap. Bounded to the visible page; required for serialization anyway. Acceptable.
- **New toolchain surface:** Tauri adds Rust + Node/React build chains and ASP.NET adds Kestrel.
  Contained to the new projects; `cdeWin`/`cde` unaffected.
- **Localhost security:** an open loopback port is reachable by other local processes. Mitigation:
  ephemeral port + startup-handshake token; do not bind beyond `127.0.0.1`.
- **Two streaming consumers of one `IAsyncEnumerable`:** must ensure the stream is cold/restartable
  per request and that cancellation/disposal is honored on client disconnect (SSE) and on UI cancel
  (WinForms).
- **Scope creep:** feature parity for Tauri (context menus, advanced filters, shell integration) is
  the largest unknown. Sequencing keeps steps 1–3 valuable independently of the frontend landing.

## Open Questions

None outstanding. Earlier open questions are resolved: shell-action execution/guard (D10–D11),
custom-command config location (D12), API surface (D13), and Tauri UI-state persistence (D14).
