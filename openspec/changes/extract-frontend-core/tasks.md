## 1. Clear the ground

- [x] 1.1 Delete dead `src/cdeWinForms` and `src/cdeWinFormsPresenter` (.NET 4.0 / x86 stubs); remove from `cde.slnx` and any references.
- [x] 1.2 Capture a search performance baseline on a representative large catalog (entries/sec, allocations) to compare against after the `FindOptions` refactor.

## 2. Decouple the search engine from the UI (cdeLib)

- [x] 2.1 Add `SearchProgress` (count/total/elapsed) and an `IProgress<SearchProgress>`-based progress path to `FindOptions`/`Find`.
- [x] 2.2 Replace `Worker.CancellationPending` checks with `CancellationToken`; unify the sync and async cancellation paths; remove the `BackgroundWorker Worker` field.
- [x] 2.3 Update `cdeWin` (and any other callers) to pass a `CancellationToken` + progress instead of a `BackgroundWorker`.
- [x] 2.4 Benchmark vs. the 1.2 baseline; confirm no hot-path regression. Keep entry-count-gated cancel checks and ~100ms throttled progress.
- [x] 2.5 Verify `cdeLib` references no UI types; run cdeLib tests.

## 3. Create cdeAppCore and move pure logic in

- [x] 3.1 Add `src/cdeAppCore` (net10, no WinForms, no ASP.NET); reference `cdeLib`; add to `cde.slnx`.
- [x] 3.2 Move `LoadCatalogService`/`ILoadCatalogService` into `cdeAppCore` (decouple from `LoaderForm`).
- [x] 3.3 Move size/date formatting and column sort comparators out of the presenter into pure core helpers.
- [x] 3.4 Move filter validation (From/To size·date·hour, not-older-than) and regex validity check into core validators returning structured results.
- [x] 3.5 Point `cdeWin` at the relocated helpers/services; delete the now-duplicated presenter code. Build and smoke-test `cdeWin` — behaviour unchanged.

## 4. Define the shared service + DTO boundary

- [ ] 4.1 Define DTOs: `SearchQuery`, `SearchResultRow`, `SearchProgress`, `DirectoryNodeDto`, `CatalogInfoDto`, `ColumnDef`; add `System.Text.Json` source-gen context.
- [ ] 4.2 Define and implement `ICatalogSession` (load → tree → directory listing → dispose) over mmap `.cdex` sources; own catalog lifetime explicitly.
- [ ] 4.3 Define and implement `ISearchService.SearchAsync(...) : IAsyncEnumerable<SearchResultRow>` with `IProgress<SearchProgress>` + `CancellationToken`.
- [ ] 4.4 Define `IShellActions` with `Open`/`Explore`/`ShowProperties` (fixed OS verbs) + run-custom-command + expose the configured command list; lift the Windows logic from `WindowsExplorerUtilities` and `CommandTokens`, inject the custom-command config + logger (drop the `Program.Configuration` static); add a non-Windows no-op impl. (Copy-path/select-all/view-in-tree/parent stay frontend-native.)
- [ ] 4.4a Move `CustomCommandOptions` + `CommandTokens` from `cdeWin` into `cdeAppCore`; keep core config-source-agnostic (no `Microsoft.Extensions.Configuration` reference — list injected). Both `cdeWin` and `cdeApi` load the shared user-scoped file (`%APPDATA%\cde\shell.json`) layered over shipped `appsettings.json` defaults, and bind+inject the `CustomCommands` list. (The `ExplorerAlt`→`CustomCommands` migration already shipped in `custom-shell-commands`.)
- [ ] 4.5 Unit-test the services against a small fixture catalog (load, tree, list, search stream, cancel, validation).

## 5. Rewire cdeWin onto the service boundary

- [ ] 5.1 Replace the presenter's implicit catalog field with `ICatalogSession`; map `DirectoryNodeDto` → `TreeNode`.
- [ ] 5.2 Replace the `BackgroundWorker` search with `await foreach` over `ISearchService`; map `SearchResultRow` → `ListViewItem`; wire cancel + progress.
- [ ] 5.3 Route the four OS shell context actions through `IShellActions`; keep copy-path (`Clipboard`), select-all, view-in-tree, and parent as native WinForms handlers.
- [ ] 5.4 Full regression pass of `cdeWin` (tree, dir list, advanced search, context menus, catalog list, status bar); run `cdeWinTest`.

## 6. Stand up the localhost API (cdeApi)

- [ ] 6.1 Add `src/cdeApi` (ASP.NET / Kestrel minimal API); reference `cdeAppCore`; bind to `127.0.0.1` on an ephemeral port; add to `cde.slnx`.
- [ ] 6.2 Implement startup-handshake token generation + middleware; reject requests without the token.
- [ ] 6.3 Endpoints (D13): `GET /health`, `POST /session/reload` (streamed progress), `GET /catalogs`, `GET /entries/{ref}/children` (foldersOnly | skip/take/sort), `GET /entries/{ref}/path`. API process owns the session; single `ref` = catalog id + entry index.
- [ ] 6.3a Shell action endpoint (`POST /shell`): accept an entry reference (not a path), resolve `FullPath` from the session, re-check `ExistsOnFileSystem`, then invoke `IShellActions` in the sidecar process.
- [ ] 6.3b UI-state endpoints (`GET`/`PUT /ui-state`): persist to `%APPDATA%\cde\ui-state.json`; `GET` merges user overrides over core defaults; exclude window geometry.
- [ ] 6.4 Implement `POST /search` (streamed) mapping `IAsyncEnumerable<SearchResultRow>` → `result`/`progress`/`done`; validate first (`400` + message); cancel on client disconnect.
- [ ] 6.5 Verify with `curl` (token rejection, reload, children/path, streamed search + validation 400, cancel-on-disconnect, ui-state round-trip).
- [ ] 6.6 Publish `cdeApi` as a self-contained single-file binary suitable for sidecar bundling.

## 7. Tauri / React frontend

- [ ] 7.1 Scaffold `src/cde-tauri` (Tauri + React/TypeScript); configure `cdeApi` as a bundled sidecar.
- [ ] 7.2 Spawn the sidecar on launch (capture port + token), terminate on exit; pass token to the webview.
- [ ] 7.3 Build an API client (HTTP + SSE) with the handshake token; load UI state from `GET /ui-state` on startup and persist via `PUT /ui-state` (debounced + flush on close). Use the Tauri window-state plugin for window geometry.
- [ ] 7.4 UI: catalog tree pane + directory listing.
- [ ] 7.5 UI: search box + advanced filters; live streaming results with progress and cancel.
- [ ] 7.6 UI: catalog list + status bar (results, entries loaded, catalogs loaded, memory, timing).
- [ ] 7.7 UI: context-menu actions — open/explore/properties and any configured custom commands (by id) via the sidecar shell endpoint (hidden/disabled for non-local entries); copy-path/select-all/view-in-tree/parent implemented frontend-natively.
- [ ] 7.8 Parity pass: same queries produce the same results as `cdeWin`; resolve the open questions in design.md (shell-action location, frontend state persistence, endpoint set).

## 8. Wrap-up

- [ ] 8.1 Update `CLAUDE.md` and `README` with the new project layout (`cdeAppCore`, `cdeApi`, `cde-tauri`) and the two-frontend architecture.
- [ ] 8.2 Build automation (Fallout) updated to build/publish the new projects; CI covers `cdeAppCore`/`cdeApi` tests.
- [ ] 8.3 `openspec validate extract-frontend-core` passes; archive the change once implemented.
