## Why

The GUI logic for CDE lives entirely inside the WinForms app (`cdeWin`). Its presenter
(`CDEWinFormPresenter`, ~1350 lines) mixes genuinely reusable behaviour — catalog loading,
search orchestration, filter validation, sorting, formatting — with WinForms view glue
(`TreeNode`, `ListViewItem`, `Color`, `BackgroundWorker`, virtual-mode index callbacks). The
domain leaks the other way too: `cdeLib`'s `FindOptions` reaches back into the UI via a
`BackgroundWorker Worker` field to poll `CancellationPending`.

We want a second frontend — a **Tauri (React/TypeScript) desktop app backed by a C# API** —
while keeping the existing WinForms `cdeWin` app fully working and feature-complete. A React
frontend cannot consume C# view-models; the only thing that can cross an HTTP/IPC boundary is
serialized data. That single constraint forces the right architecture: extract a
**frontend-agnostic application core** (transport-agnostic services + serializable DTOs) that
both `cdeWin` (in-process) and a thin localhost API (for Tauri) call identically.

Doing this also pays down the UI-in-domain coupling regardless of whether Tauri ships, and
gives the search engine a clean, testable, headless contract.

## What Changes

- **Decouple the search engine from the UI.** Remove `BackgroundWorker` from `cdeLib.FindOptions`;
  unify cancellation on `CancellationToken` and progress on `IProgress<SearchProgress>`. No UI
  type appears in `cdeLib`.
- **Introduce `cdeAppCore`** — a new `net10`, UI-free, ASP.NET-free library holding: catalog
  session management (`ICatalogSession`), streamed search (`ISearchService` returning
  `IAsyncEnumerable<SearchResultRow>`), shell actions (`IShellActions`), serializable DTOs, and
  the pure helpers currently inline in the presenter (size/date formatting, sort comparators,
  filter validation, regex check).
- **Rewire `cdeWin`** to consume `cdeAppCore` in-process. The presenter becomes a thin adapter
  (core `DirectoryNodeDto` → `TreeNode`, `SearchResultRow` → `ListViewItem`). WinForms keeps its
  own reflection-based event wiring and window-geometry config. Zero-copy mmap stays in-process —
  WinForms is **not** routed through HTTP.
- **Introduce `cdeApi`** — an ASP.NET (Kestrel) localhost-only shim over `cdeAppCore`, exposing
  HTTP/JSON endpoints and Server-Sent Events for streamed search/progress. Bound to loopback with
  a startup-handshake token. The API process owns the memory-mapped catalogs (it *is* the session).
- **Introduce the Tauri/React frontend** — a Rust shell hosting a React/TypeScript webview, with
  `cdeApi` bundled and supervised as a sidecar process. Delivers feature parity with `cdeWin`:
  catalog tree, directory list, search with advanced filters, context-menu actions
  (open/explore/properties/copy-path), catalog list, and status bar.
- **Delete** the dead `cdeWinForms` / `cdeWinFormsPresenter` .NET 4.0 / x86 stub projects to
  remove the naming trap (they are not the live app).

## Capabilities

### New Capabilities
- `app-core`: Frontend-agnostic application layer — catalog session, streamed search service,
  shell actions, serializable DTOs, validation and formatting helpers; plus the `cdeLib` find
  engine decoupling from UI types.
- `local-api`: Localhost-only HTTP/JSON + Server-Sent Events API (`cdeApi`) over `app-core`,
  designed to run as a Tauri sidecar that owns the catalog session.
- `tauri-shell`: Tauri (React/TypeScript) desktop frontend with WinForms feature parity, talking
  to `cdeApi` over loopback and bundling it as a supervised sidecar.

### Modified Capabilities
<!-- No existing OpenSpec specs to modify; cdeLib changes are captured as requirements under app-core. -->

## Impact

- **New projects:** `src/cdeAppCore` (lib), `src/cdeApi` (ASP.NET), `src/cde-tauri` (Tauri/React).
- **Modified:** `cdeLib/FindOptions.cs` (remove `BackgroundWorker`, add `CancellationToken` +
  `IProgress`); `cdeWin` presenter/view (consume core services, drop relocated logic);
  `cde.slnx` (add/remove projects).
- **Removed:** `src/cdeWinForms`, `src/cdeWinFormsPresenter`.
- **Behaviour:** `cdeWin` end-user behaviour unchanged through steps 1–3 (pure refactor); search
  cancellation/progress semantics preserved.
- **Hot path:** the `FindOptions` cancellation/progress refactor touches the search inner loop —
  must be benchmarked against the current baseline (no regression on large catalogs).
- **New runtime deps:** ASP.NET (Kestrel) for `cdeApi`; Rust toolchain + Tauri + Node/React for the
  new frontend. None affect `cdeWin` or `cde` (CLI).
