# app-core Specification

## Purpose
TBD - created by archiving change extract-frontend-core. Update Purpose after archive.
## Requirements
### Requirement: UI-free search engine
The `cdeLib` find engine SHALL NOT depend on any UI or presentation type. Cancellation SHALL be
expressed via `CancellationToken` and progress via a non-UI progress abstraction. The
`BackgroundWorker` dependency in `FindOptions` SHALL be removed.

#### Scenario: Cancellation without a BackgroundWorker
- **WHEN** a search is run with a `CancellationToken` that is cancelled mid-traversal
- **THEN** the search stops promptly (within the existing entry-count-gated check interval)
- **AND** no `System.ComponentModel.BackgroundWorker` type is referenced by `cdeLib`

#### Scenario: Progress without UI coupling
- **WHEN** a long-running search reports progress
- **THEN** progress is delivered through `IProgress<SearchProgress>` (or an equivalent non-UI
  callback) on the existing throttled (~100ms) cadence
- **AND** the search hot-path throughput shows no regression versus the pre-change baseline on a
  representative large catalog

### Requirement: Frontend-agnostic application core library
A new `cdeAppCore` library SHALL contain the application-layer services, DTOs, and pure helpers
shared by all frontends. It SHALL depend only on `cdeLib` and MUST NOT reference WinForms, ASP.NET,
or any frontend project.

#### Scenario: No UI or web dependencies
- **WHEN** `cdeAppCore` is compiled
- **THEN** it references neither `System.Windows.Forms` nor any ASP.NET / Kestrel assembly
- **AND** every public type it exposes for cross-boundary use is serializable as plain data

### Requirement: Owned, disposable catalog session
`cdeAppCore` SHALL expose an `ICatalogSession` abstraction that loads, holds, and disposes catalog
sources (zero-copy memory-mapped `.cdex` readers, or in-memory stores when no `.cdex` exists). The
session SHALL own the catalog lifetime explicitly rather than relying on an ambient field.

#### Scenario: Load and dispose releases mmap
- **WHEN** a session loads `.cdex` catalogs and is then disposed
- **THEN** all memory-mapped sources are released so the mappings are closed
- **AND** reloading does not leak prior mappings

#### Scenario: Catalog data is not materialized to the boundary
- **WHEN** a frontend requests a directory listing or search results
- **THEN** only the requested rows are materialized as DTOs
- **AND** the underlying catalog entry graph is not copied or serialized in full

### Requirement: Streamed search service
`cdeAppCore` SHALL expose `ISearchService.SearchAsync(SearchQuery, IProgress<SearchProgress>,
CancellationToken)` returning `IAsyncEnumerable<SearchResultRow>`. Results SHALL stream as they are
found, progress SHALL be reported during the search, and cancellation SHALL stop it.

#### Scenario: Results stream incrementally
- **WHEN** a search matches many entries over a long traversal
- **THEN** matching `SearchResultRow` DTOs are yielded incrementally as found, not only at the end

#### Scenario: Cancellation stops the stream
- **WHEN** the caller cancels the supplied `CancellationToken` during enumeration
- **THEN** enumeration ends promptly and the underlying traversal stops

#### Scenario: Query maps from a serializable request
- **WHEN** a `SearchQuery` DTO carrying pattern, regex mode, path inclusion, file/folder inclusion,
  result limit, and size/date/hour/not-older-than filters is supplied
- **THEN** the service applies exactly those filters, equivalent to the current WinForms search

### Requirement: Everything-style search mode
`SearchQuery` SHALL carry an `EverythingMode` flag. When it is set, `Pattern` SHALL be parsed as an
Everything-style (voidtools) query and `RegexMode` SHALL be ignored. Iteration 1 supports only: space (AND),
`|` (OR), `!` (NOT, applying to the next term, group or phrase), `< >` (grouping) and `"..."` (a literal phrase;
operators inside quotes are plain text). Precedence from tightest is `!`, groups, space, `|`. Each term is a
case-insensitive substring of the entry name; only a `path:` term reaches the full path. An empty or
whitespace-only query matches everything. Backslash is an ordinary character. Wildcards, functions such as
`ext:` and regex are out of scope. The `path:` and `no-path:` modifiers and the `file:` and `folder:` filters are supported (below). Groups SHALL nest no deeper than 32 levels. The columnar and in-memory stores
SHALL return the same rows, and the plain and regex paths SHALL be unchanged when the flag is off. In Everything
mode `IncludePath` SHALL be ignored (treated as false) and `IncludeFiles` and `IncludeFolders` SHALL be ignored
(both treated as true): the query text alone chooses the path (`path:`) and files or folders (`file:`, `folder:`).

#### Scenario: Space is AND
- **WHEN** the query is `do win` in Everything mode
- **THEN** `Windows` matches, because it contains both `do` and `win`

#### Scenario: Precedence
- **WHEN** the query is `a b | c`
- **THEN** it means `(a AND b) OR c`

#### Scenario: path: and no-path: modifiers
- **WHEN** a word, phrase or group is directly preceded (no space) by `path:`
- **THEN** its terms match the full path, including the name; `no-path:` makes them match the name only
- **AND** a term with no modifier matches the name
- **AND** the modifier applies to that one word, phrase or group, the innermost modifier wins inside a group,
  `!` goes in front of the modifier (`!path:foo`), and modifier names are case-insensitive
- **AND** the full path is built for an entry only when evaluation reaches a term that needs it
- **AND** `"path:x"` in quotes is the literal text; the `\:` alias and the global `?path:` form are rejected as
  not supported yet

#### Scenario: file: and folder: filters
- **WHEN** the query contains `file:` or `folder:`
- **THEN** it is a stand-alone yes/no test on the entry kind that joins the AND, OR, NOT and group tree like any term
  (`!file:` is folders, `<file: a> | <folder: b>` works); the text after the colon, with or without a space, is its
  own term (`folder:music` means `folder: music`)
- **AND** the default with no filter is files and folders
- **AND** the names are case-insensitive, `file:` or `folder:` alone is valid and lists every entry of that kind,
  `file: folder:` matches nothing without an error, and `"file:"` in quotes is the literal text
- **AND** `path:` or `no-path:` directly before a filter is an error, because a filter is not a term to attach to

#### Scenario: Malformed query is rejected with a position
- **WHEN** the query has an unbalanced `<` or `"`, a dangling `|`, a lone `!`, an empty `<>`, adjacent operators
  (`a || b`, `!!a`), an empty phrase, a modifier not directly followed by a word, phrase or group (`path:`,
  `path: a`, `path:!a`), or nesting deeper than 32
- **THEN** `SearchFilterValidator` reports `SearchFilterField.Everything` with a message ending in
  `(at position N)` and the 0-based `Position`

### Requirement: Narrow shell-action abstraction
`cdeAppCore` SHALL expose `IShellActions` covering the fixed OS shell verbs `Open`, `Explore`, and
`ShowProperties` (each taking a resolved local filesystem path), plus a custom-command facility: the
configured `CustomCommandOptions` list and a run operation that performs `{path}`/`{dir}`/`{filename}`
token substitution before launching. The implementation SHALL be the Windows logic lifted from
`WindowsExplorerUtilities` and `CommandTokens`, with the custom-command configuration and logger
injected (no static `Program.Configuration` coupling). Clipboard, select-all, view-in-tree, and
parent-navigation SHALL NOT be part of `IShellActions`; they are frontend-native concerns. Core SHALL
supply only the path strings those frontend actions need.

#### Scenario: Only the OS verbs and custom commands are exposed
- **WHEN** a frontend uses `IShellActions`
- **THEN** the operations available are `Open`, `Explore`, `ShowProperties`, and running a configured
  custom command (plus reading the configured custom-command list)
- **AND** clipboard copy, select-all, view-in-tree, and go-to-parent are not present on the interface

#### Scenario: Custom command runs with token substitution
- **WHEN** a configured custom command is run against a resolved local path
- **THEN** its `Arguments` template has `{path}`/`{dir}`/`{filename}` substituted before launch
- **AND** a launch failure is logged via the injected logger without crashing the host

### Requirement: Custom-command configuration is owned by core but sourced by the host
The `CustomCommandOptions` type (and `CommandTokens`) SHALL live in `cdeAppCore`, but `cdeAppCore`
SHALL NOT read configuration itself — it SHALL receive the command list via injection. Hosts SHALL
source the list from a shared, user-scoped, update-stable location so all frontends honour the same
commands.

#### Scenario: Core does not read configuration sources
- **WHEN** `cdeAppCore` is compiled
- **THEN** it does not reference `Microsoft.Extensions.Configuration`, `appsettings.json`, or any
  ambient configuration static; the custom-command list reaches it only by injection

#### Scenario: Hosts share one user-scoped custom-command list
- **WHEN** the user configures custom commands
- **THEN** both `cdeWin` and the `cdeApi` sidecar read them from the same user-scoped file
  (`%APPDATA%\cde\shell.json`) layered over shipped `appsettings.json` defaults
- **AND** the commands survive app publish/update (they are not stored only in a bundled
  `appsettings.json`)

#### Scenario: Non-Windows host degrades safely
- **WHEN** `IShellActions` is resolved on a non-Windows host
- **THEN** a no-op / `NotSupported` implementation is provided rather than a Windows P/Invoke failure

### Requirement: Filter and pattern validation in core
`cdeAppCore` SHALL provide the search filter validation rules (From/To size, date, and hour
consistency; not-older-than; regex validity) as pure, frontend-agnostic functions returning
structured validation results.

#### Scenario: Inconsistent range is rejected with a message
- **WHEN** a `SearchQuery` has From-size greater than To-size (or the equivalent date/hour cases)
- **THEN** validation returns a failure with a human-readable message identifying the offending field

#### Scenario: Invalid regex is rejected with a message
- **WHEN** regex mode is enabled and the pattern is not a valid regular expression
- **THEN** validation returns a failure carrying the regex error message

### Requirement: Shared formatting and sorting helpers
`cdeAppCore` SHALL provide the size/date formatting and column sort-comparison logic currently inline
in the WinForms presenter, as pure functions independent of any UI framework.

#### Scenario: Deterministic formatting and sorting
- **WHEN** a row's size, date, or column sort order is formatted/compared via core helpers
- **THEN** the output matches the current WinForms rendering and sort behaviour for the same input

### Requirement: Directory tree and listing as DTOs
`cdeAppCore` SHALL expose the catalog directory tree and per-directory listings as serializable DTOs
(`DirectoryNodeDto`, `SearchResultRow`/listing rows, `CatalogInfoDto`, `ColumnDef`) rather than as
WinForms `TreeNode`/`ListViewItem`.

#### Scenario: Tree expressed as plain nodes
- **WHEN** a frontend requests the directory tree for loaded catalogs
- **THEN** it receives a tree of `DirectoryNodeDto` (name, path, child links, has-children)
- **AND** no WinForms type is required to represent it

