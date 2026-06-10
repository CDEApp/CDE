# cde-tauri

Tauri (React/TypeScript) desktop frontend for CDE. It talks to the **cdeApi** sidecar over
loopback HTTP/SSE and bundles that sidecar as a supervised child process.

## Architecture

```
cde-tauri (Rust shell)
  └─ webview (React/TS)  ──HTTP/SSE+token──▶  cdeApi sidecar  ──▶  cdeAppCore  ──▶  cdeLib (mmap .cdex)
```

- The webview spawns the bundled `cdeApi` sidecar via the Tauri shell plugin
  (`src/sidecar.ts`), reads its stdout handshake (`{ "url", "token" }`), and uses that
  base URL + token for every request.
- Catalog data never crosses the wire as a graph — only DTO pages (tree nodes, listing rows,
  search results) do. The mmap catalogs live in the sidecar process.
- Window geometry is persisted by the Tauri **window-state** plugin (Rust side). App-level UI
  state (column sets, search history, filter prefs) round-trips through the sidecar's
  `/ui-state` endpoint.

## Prerequisites

- Node 18+ and pnpm
- Rust toolchain (stable) + the platform's Tauri prerequisites
  (WebView2 on Windows, `webkit2gtk` on Linux, Xcode CLT on macOS)
- The .NET SDK (to publish the `cdeApi` sidecar)

## Develop / run

```bash
pnpm install
pnpm bundle-sidecar   # publishes cdeApi and stages it under src-tauri/binaries/
pnpm tauri dev        # runs vite + the Rust shell
```

## Build a distributable

```bash
pnpm bundle-sidecar
pnpm tauri build
```

## Notes

- Icons under `src-tauri/icons/` are generated with `pnpm dlx tauri icon <source.png>`.
- `src-tauri/binaries/` (the staged sidecar) is git-ignored; run `pnpm bundle-sidecar`
  to regenerate it for your host target triple.
