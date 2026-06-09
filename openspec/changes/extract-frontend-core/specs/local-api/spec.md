## ADDED Requirements

### Requirement: Localhost-only HTTP API over the application core
A new `cdeApi` (ASP.NET / Kestrel) project SHALL expose `cdeAppCore` services over HTTP/JSON. It
SHALL bind only to the loopback interface (`127.0.0.1`) on an ephemeral port and MUST NOT be
reachable from other hosts.

#### Scenario: Bound to loopback only
- **WHEN** `cdeApi` starts
- **THEN** it listens on `127.0.0.1` on an ephemeral port
- **AND** it does not bind to any externally routable address

#### Scenario: Thin shim over shared services
- **WHEN** an API endpoint handles a request
- **THEN** it delegates to the same `cdeAppCore` service the desktop app uses
- **AND** contains no search/catalog business logic of its own

### Requirement: Startup-handshake authorization
`cdeApi` SHALL require a startup-handshake token on every request. The token SHALL be generated at
process start and provided to the launching host; requests without the valid token SHALL be rejected.

#### Scenario: Request without token is rejected
- **WHEN** a request arrives without the valid startup token
- **THEN** the API responds with an unauthorized status and performs no action

### Requirement: Server-Sent Events search streaming
`cdeApi` SHALL expose search as a Server-Sent Events stream that maps the core
`IAsyncEnumerable<SearchResultRow>` to `result` events, with periodic `progress` events and a final
`done` event. Client disconnect SHALL cancel the underlying search.

#### Scenario: Streamed results and progress
- **WHEN** a client opens the search SSE endpoint with a query
- **THEN** it receives `result` events as rows are found and `progress` events during the search
- **AND** a terminal `done` event when the search completes

#### Scenario: Disconnect cancels the search
- **WHEN** the client closes the SSE connection mid-search
- **THEN** the server cancels the `CancellationToken` and the underlying traversal stops

### Requirement: Catalog session and query endpoints
`cdeApi` SHALL expose endpoints sufficient for frontend feature parity: load/refresh catalogs, fetch
the directory tree, list a directory's entries, run a search (SSE), list loaded catalogs, and invoke
a shell action. The API process SHALL own the catalog session (the memory-mapped catalogs live in the
API process).

#### Scenario: Session lives in the API process
- **WHEN** catalogs are loaded via the API
- **THEN** the memory-mapped `.cdex` sources are held by the API process
- **AND** clients receive only DTO pages, never the full entry graph

#### Scenario: JSON via source-generated serializers
- **WHEN** the API serializes DTOs
- **THEN** it uses `System.Text.Json` source-generated serialization for the `cdeAppCore` DTOs

### Requirement: Capability-guarded shell action endpoint
The shell action endpoint SHALL accept a session entry reference (catalog + entry indices) and an
action name, never a client-supplied filesystem path. The server SHALL resolve the full path from the
session and verify the entry exists on the local filesystem before invoking `IShellActions`. The
action SHALL execute in the API (sidecar) process.

#### Scenario: Action identified by entry reference
- **WHEN** a client requests a shell action
- **THEN** the request identifies the target by session entry reference, not by a raw path
- **AND** the server resolves the full path from the loaded session

#### Scenario: Non-existent or unresolved entry is rejected
- **WHEN** the referenced entry does not resolve in the session or does not exist on the local
  filesystem
- **THEN** the endpoint rejects the request and invokes no shell operation

#### Scenario: Action runs in the sidecar process
- **WHEN** a valid `Open`/`Explore`/`ShowProperties` action, or a custom command referenced by id, is
  requested
- **THEN** `IShellActions` executes in the API process against the resolved local path
- **AND** for a custom command the server resolves the command definition (the client never sends a
  command line) and applies `{path}`/`{dir}`/`{filename}` substitution before launch

### Requirement: Minimal parity endpoint set
The API SHALL provide a minimal endpoint set sufficient for frontend feature parity: `GET /health`,
`POST /session/reload` (streamed progress), `GET /catalogs`, `GET /entries/{ref}/children`,
`GET /entries/{ref}/path`, `POST /search` (streamed), `POST /shell`, and `GET`/`PUT /ui-state`.
Entries SHALL be referenced by a single `ref` type (catalog id + entry index) resolved by the session.

#### Scenario: One children endpoint serves tree, list, and drill
- **WHEN** the tree expands a node (`?foldersOnly=true`), the directory list pages a folder
  (`?skip&take&sort`), or a directory is activated
- **THEN** all three are served by `GET /entries/{ref}/children` with parameters, not by separate
  endpoints

#### Scenario: Path chain supports view-in-tree and parent navigation
- **WHEN** a frontend needs to reveal an entry in the tree or navigate to its parent
- **THEN** `GET /entries/{ref}/path` returns the root→entry chain of refs

#### Scenario: Validation is part of the search endpoint
- **WHEN** `POST /search` receives an invalid query (inconsistent range or bad regex)
- **THEN** it returns `400` with the offending field/regex message before streaming any result

#### Scenario: Reload reuses the streamed progress mechanism
- **WHEN** `POST /session/reload` runs
- **THEN** it streams progress events and a terminal `done` event using the same mechanism as search

### Requirement: API-served UI state
The API SHALL serve and persist the frontend's UI state via `GET`/`PUT /ui-state`, persisted to a
user-scoped, update-stable file (`%APPDATA%\cde\ui-state.json`). `GET` SHALL return the user's stored
state merged over core-provided defaults. The state is per-frontend and is not shared with `cdeWin`.

#### Scenario: Defaults merged on read
- **WHEN** `GET /ui-state` is called with no stored overrides for some fields
- **THEN** the response provides core defaults (e.g. default column sets) for those fields

#### Scenario: Persisted server-side and durable
- **WHEN** the frontend `PUT`s updated UI state
- **THEN** the API writes it to `%APPDATA%\cde\ui-state.json`
- **AND** the state survives clearing the webview cache

#### Scenario: Window geometry is not part of UI state
- **WHEN** UI state is read or written
- **THEN** it does not carry the OS window size/position (managed natively by the Tauri shell)
