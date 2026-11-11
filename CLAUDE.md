@AGENTS.md

# CDE - Catalog Directory Entries

## Project Overview

CDE is a high-performance file system cataloging utility written in C# that creates searchable catalogs of directory structures. It's designed to handle billions of file entries efficiently with minimal memory footprint.

**Main Purpose**: Capture directory entries into catalog files (.cde) for later processing, searching, hashing, and duplicate detection without requiring access to the original file systems.

**Inspiration**: Built as a modern alternative to Cathy (an older utility limited to 65,535 entries), CDE can handle billions of entries efficiently.

## Project Structure

### Main Projects

- **cde** - Command-line interface (CLI) application
  - Target: .NET 10
  - Cross-platform: win-x64, linux-x64, osx-x64
  - Entry point for scan, find, everything (Everything-style query), hash, dupes, dump commands
  - Dependencies: Autofac, SlimMessageBus, CommandLineParser, Spectre.Console

- **cdeLib** - Core library containing business logic
  - Target: .NET 10
  - Contains all catalog operations, hashing, duplicate detection
  - Uses CQRS pattern with SlimMessageBus
  - Serialization: columnar `.cdex` (zero-copy, memory-mapped), plus MessagePack/FlatSharp for the legacy `.cde` tree format
  - Key dependencies: Autofac, SlimMessageBus, Serilog

- **cdeAppCore** - Frontend-agnostic application core
  - Target: .NET 10 (no WinForms, no ASP.NET; references only `cdeLib`)
  - Transport-agnostic services + serializable DTOs shared by every frontend: `ICatalogSession`
    (owned, disposable catalog lifetime over mmap `.cdex`; per-catalog rescan "replace", create "add" and the
    `CatalogDir` working folder, all under one session gate), `JobRegistry` (queued/running/finished scan jobs over
    the session, with a concurrency cap), streamed `ISearchService`
    (`IAsyncEnumerable<SearchResultRow>` + `IProgress<SearchProgress>`), `IShellActions`
    (open/explore/properties + custom commands), and the pure formatting/sorting/validation helpers
    lifted out of the WinForms presenter
  - Both `cdeWin` (in-process) and `cdeApi` (over HTTP) call these same services with the same DTOs

- **cdeWin** - Windows Forms GUI application
  - Target: .NET 10 (Windows)
  - Browse, search, and navigate catalogs visually
  - Consumes `cdeAppCore` in-process (zero-copy mmap path preserved; not routed through HTTP)
  - Configuration stored in Local AppData or current directory

- **cdeApi** - Localhost HTTP/JSON + SSE API over `cdeAppCore` (Tauri sidecar)
  - Target: .NET 10 (ASP.NET / Kestrel minimal API)
  - Binds `127.0.0.1` on an ephemeral port; gated by a startup-handshake token printed on stdout
  - The API process owns the catalog session. Endpoints: `/health`, `/catalogs`,
    `/entries/{ref}/children`, `/entries/{ref}/path`, `/search` (SSE; the final `progress` event carries `limitReached: true` only when the result limit stopped the search with more matches beyond it), `/session/reload` (SSE; 409 busy while any job is queued or running),
    `POST /jobs` (queue a rescan of a catalog or a create of a new `.cdex` in the working folder; 202 + the job), `GET /jobs`, `GET /jobs/events` (SSE: `snapshot`, then one `job` event per change), `POST /jobs/{id}/cancel`, `DELETE /jobs/{id}`, `DELETE /jobs/finished`, `POST /catalogs/preflight`, `PUT /catalogs/{id}/description` (rewrites only the `.cdex` metadata and swaps it in; legacy `.cde` refused), `POST /compare/summaries` (exact subtree counts for folder pairs),
    `/shell`, `/shell/commands`, `/ui-state`
  - Scan jobs run through `cdeAppCore`'s `JobRegistry`: at most `AppConfig:Jobs:MaxConcurrent` (default 3) at once, the
    rest queued; never two queued/running jobs on one catalog file; finished jobs listed until dismissed (200 kept)
  - Publishes as a self-contained single-file binary for sidecar bundling

- **cde-tauri** - Tauri (React/TypeScript) desktop frontend (`src/cde-tauri`)
  - Tauri v2 shell hosting a React/Vite webview; bundles `cdeApi` as a supervised sidecar
  - Talks to the sidecar over loopback HTTP/SSE with the handshake token; mmap catalogs live in the
    sidecar, the webview only ever sees DTO pages
  - Window geometry via the Tauri window-state plugin; app UI state via `/ui-state`
  - Not a .NET project: built with `pnpm`/`vite` + `cargo` (see `src/cde-tauri/README.md`)

- **cdeMemProbe** / **cdeBenchmarks** - Footprint probe and BenchmarkDotNet micro-benchmarks. `cdeMemProbe --report <N>` writes a versioned `perf.json` (scan time, `.cdex` size, cold/warm search, peak RSS on the mmap path); `--compare` gates only on `.cdex` size growth over 2% (exit 3), and `--readme` regenerates the table between the `perf-table` markers in `Readme.md`. The `Performance` workflow (`.github/workflows/perf.yml`) runs it per release tag, attaches `perf.json` to the release and opens a Readme PR. See `src/cdeBenchmarks/README.md`.

### Test Projects

- **cdeLibTest** - Unit tests for cdeLib
- **cdeAppCoreTest** - Unit tests for cdeAppCore (session, streamed search, validation, formatting)
- **cdeLibSpec** - Specification/BDD tests
- **cdeLibSpec2** - Additional specification tests
- **cdeWinTest** - Tests for Windows Forms application

### Two-frontend architecture

The GUI logic lives in `cdeAppCore` (transport-agnostic services + serializable DTOs), not in any
frontend. Dependency direction:

```
cdeLib  <-  cdeAppCore  <-  cdeWin   (in-process; zero-copy mmap, no HTTP)
                        <-  cdeApi   (HTTP/SSE)  <-  cde-tauri / React (loopback sidecar)
```

`cdeWin` consumes the core services in-process (preserving the zero-copy mmap path); `cdeApi` is a
thin localhost shim exposing the *same* services over HTTP/JSON + Server-Sent Events, bundled as a
sidecar by the Tauri frontend. The same `SearchQuery`/`SearchResultRow`/`DirectoryNodeDto` DTO
boundary is used in-proc and over the wire, so the API is a shim, not a second implementation. The
on-disk `.cdex`/`.cde` formats and the `cde` CLI are unchanged.

### Supporting Projects

- **Mono.Terminal** - Terminal/console utilities
- **Finder** - File finding utilities
- **Util** - General utilities
- **NUnit.Should** - Testing helpers

## Key Technologies & Patterns

### Architecture Patterns

- **CQRS (Command Query Responsibility Segregation)**: Commands and queries handled via SlimMessageBus (`IRequestHandler`, `IMessageBus`)
  - Commands: `CreateCacheCommand`, `HashCatalogCommand`, `FindDuplicatesCommand`, `UpdateCommand`
  - Handlers: Separate handlers for each command
  - Events: `ScanProgressEvent` for progress tracking

- **Dependency Injection**: Autofac container
  - Module registration in `CdelibModule.cs`
  - Scoped lifetimes for services

- **Object Pooling**: Heavy use of pooling for performance
  - `BufferPool` - Byte array pooling
  - `ObjectPool<T>` - Generic object pooling
  - `CollectionPool` - Collection pooling
  - Critical for reducing GC pressure with billions of entries

### Serialization

Two on-disk catalog formats:

- **Columnar `.cdex`** (`Entities/Columnar/ColumnarFormat.cs`) - **Current/primary format.** A struct-of-arrays layout designed for *zero-copy reads over a memory-mapped file*: "loading" a catalog is mmap-ing it, so no managed object graph is materialised and the working set is only the file pages a query touches (reclaimable OS page cache, not GC heap). Custom binary layout with a `"CDEX"` magic header and dense, homogeneous columns (names, sizes, timestamps, hashes, tree links) — a name-only search scans just the name columns and never pages in the rest. Written directly by `scan`; read via `ColumnarCatalogReader`.
- **Legacy `.cde` tree format** - The original materialised directory-tree format, serialized via a pluggable `SerializerProtocol` in `Catalog/CatalogRepository.cs`:
  - **MessagePack** - default protocol for `.cde` (`MessagePackConfig.Options`, custom `Hash16Formatter`/resolver)
  - **FlatSharp** (FlatBuffers) - alternative protocol selectable via `SerializerProtocol`
  - FlatSharp 7 has no runtime code generation: the FlatBuffers layout is `Catalog/FlatBuffers/Catalog.fbs`, compiled at build time into `Fb*` types that `FlatBufferCatalogSerializer` maps to/from `RootEntry`/`DirEntry`. Field ids are positional and match the old FlatSharp 6 attribute indexes (the `deprecated` fields fill the gaps), so add new fields at the end of a table. The schema compiler runs on the .NET 9 runtime, which CI installs alongside 10.

### Hashing

- **XxHash128** (`System.IO.Hashing`, seed 0) - the file content hash since issue #222, for both the partial and the full hash. All 128 bits are kept in `Hash16` (`HashA` = high 64 bits, `HashB` = low 64 bits, so the `HashA:x16 + HashB:x16` hex that `EntryRecords` prints equals `xxh128sum` output). `HashHelper` is the only producer: one-shot over a single read for the partial hash, incremental over pooled 1 MB reads to end of file for the full hash.
  - **Breaking change (issue #222)**: earlier builds hashed with MurmurHash3-32. A `.cdex` hashed by this build carries header flag bit2 (`ColumnarFormat.FlagHashXxHash128`). A hash column without that bit (`ColumnarCatalogReader.Hashes == CatalogHashes.Legacy`) and every hash in a `.cde` are never usable: the reader masks their hash flags so they read as unhashed (`BitFields` is the masked `EntryFlagsColumn`; read hash state only through the reader), `CatalogRepository.Read` clears them on load, `cde hash` recomputes them, a rescan reuses none, `cde migrate` writes no hash column, and `cde dupes` ignores them. `cde hash` and `cde dupes` log one line per such catalog. `cde update --description` re-saves a `.cde` without its old hashes. The `.cdex` version is not bumped and old catalogs stay readable. Downgrade caveat: an older build ignores the bit and may compare its MurmurHash3 values with XxHash128 ones (missed duplicates, no false ones), and a `cde hash` by an older build drops the bit, so the next new build recomputes everything; run the new build's `cde hash` after any downgrade round trip.
- **Partial hashing strategy**: Initially hashes first 64KB, promotes to full hash when duplicates detected
  - **Breaking change (issue #37)**: before the fix the partial hash covered the wrong block (the second block, or a mix) and read 2x the data. It is now exactly the first N bytes (whole file if shorter), read once, correct under short reads. New partial hashes carry `Flags.PartialHashV2` (bit 7). A partial hash WITHOUT that bit (an old catalog) is treated as "not hashed": `IsHashDone` is false for it, it is never compared or copied on rescan, and the next `cde hash` recomputes it. The `.cdex` version is not bumped and old catalogs stay readable. Full hashes are unchanged. Downgrade caveat: an older build does not know the bit, so it reads new-style partial hashes as ordinary partials and may compare them with old-algorithm partials (missed duplicates); do not run an older build over catalogs hashed by a newer one, or run `cde hash` again afterwards.

### Performance Optimizations

- **Work-Stealing Tree Traversal**: Custom parallel directory traversal
  - `WorkStealingQueue<T>` - Lock-free queue implementation
  - `WorkStealingTreeTraversal` - Parallel tree walking
  - `TraversalWorkItem` - Work unit for traversal

- **Lock-Free Concurrency**: `LockFreeCounter` for thread-safe counting

- **Parallel Processing**:
  - Configurable `DegreesOfParallelism` for hashing
  - File systems hashed in parallel
  - Files processed in descending size order

## Core Domain Entities

### Entity Hierarchy

```
Entry (base class)
├── RootEntry - Represents a file system root (drive, UNC path)
├── DirEntry - Directory entry
└── CommonEntry - File or directory with metadata
```

### Key Entity Files

- **Entry.cs** - Base class for all file system entries
- **RootEntry.cs** - File system root (drive letter, UNC share, volume info)
- **DirEntry.cs** - Directory with children collection
- **DirEntry.EqualityComparer.cs** - Custom equality comparison
- **EntryHelper.cs** - Helper methods for entry manipulation
- **Hash16.cs** - 16-byte hash representation
- **Flags.cs** - Entry flags (Hidden, System, Directory, etc.)

## Important Files & Directories

### Configuration

- **appsettings.json** - Application configuration, shared by `cde` and `cdeApi`. Looked up in the config folder (`~/.config/cde`, `%APPDATA%\cde`, or `$CDE_CONFIG_DIR`), then the current directory, then the executable's folder; the first file found is used alone. A default is written to the config folder when none exists (`AppSettingsLocator`, #158)
  - `AppConfig.Display.ProgressUpdateInterval` - Progress reporting frequency (default: 5000 entries)
  - `AppConfig.Hashing.FirstPassSizeInBytes` - Partial hash size (default: 65536 bytes)
  - `AppConfig.Hashing.DegreesOfParallelism` - Parallel hashing threads per file system (default: 2)
  - `AppConfig.Scan.ExcludePaths` - Absolute folder paths (or bare folder names) a scan records as empty entries but does not descend into (default: none; not an error, not stored in the catalog). Matched by `ScanExcludeMatcher`: covers the folder and everything below, a bare name with no separator (`node_modules`, `.git`) matches a folder of that name at any depth below the scan root, never the root itself (blank, `.`, `..` and all-`*` names are rejected); `*` matches within one segment (no `**`; a trailing `/*` excludes the children but not the folder itself, so `/tmp/*` leaves `/tmp` scanned), case-sensitive except on Windows (case-insensitive, `/` = `\`); default macOS volumes are case-insensitive, so write patterns with the folder's real case. The scan root and its ancestors are never excluded; paths below it still apply. Wins over `FollowJunctions`
  - `AppConfig.Scan.UseDefaultExcludes` - On Linux also skip `/proc /sys /dev /run /tmp /var/tmp /var/cache /var/spool /var/lock /lost+found /home/*/.cache` (default: true; user paths add to it; on macOS it skips `/dev /.Spotlight-V100 /.fseventsd` (the two dot folders are anchored at `/`, so only when scanning from `/`; on another volume add e.g. `/Volumes/<name>/.Spotlight-V100`); no effect on Windows). The list lives in code
  - `AppConfig.Scan.StaleAfterDays` - Days after which cde-tauri marks a catalog stale (default: 30; 0 = off). `GET /catalogs` returns the current value as `staleAfterDays`, read from live config per request
  - `AppConfig.Scan.RescanStaleOnStartup` - cde-tauri rescans stale catalogs once after the first load of a session (default: false); returned as `rescanStaleOnStartup`
  - `AppConfig.Scan.Threads` - Folders a scan reads at the same time (default: 4; 1 = one at a time). `RootEntry.RecurseTree` workers share one stack of folders; each folder is read whole by one worker in OS order, so the tree and the sorted `.cdex` are the same for any value (#235). Used by `cde scan` and by `cdeApi` create/rescan (read per scan)
  - `cde scan --exclude <path>` (repeatable) adds patterns for that run only, on top of config and defaults: option -> `CreateCacheCommand.ExcludePaths` -> `ScanExcludeMatcher.ForScanConfig(scan, extraPatterns)`. Not stored in the catalog or config
  - Scan excludes apply in `cde scan`, and in `cdeApi` create/rescan (`CatalogSession` takes a `Func<ScanSection>` provider, read on every scan, so a rescan uses the CURRENT config after an appsettings reload). `cdeWin` has no scan UI and passes no provider
  - `Serilog` - Logging configuration (console, Seq)

### Build System

- **Fallout Build** - Build automation (replaced Nuke)
  - `build.cmd` / `build.ps1` / `build.sh` - Build scripts (bootstrap `build/_build.csproj`)
  - `build/Build.cs` - Build definition (uses `Fallout.Common`)
  - `.fallout/` - Fallout config, parameters, and temp/log output
  - Command: `build.cmd publish` - Creates artifacts in `./artifacts`
  - Releases: tag-driven GitHub Actions pipeline that zips `cde`, `cdewin` and `cde-tauri`; see `docs/release.md`

### Key Command Handlers

Located in `cdeLib/`:
- `Catalog/CreateCacheCommandHandler.cs` - Scans file systems, writes columnar `.cdex` catalogs (reusing hashes from an existing `.cdex` when present)
- `Hashing/HashCatalogCommandHandler.cs` - Adds XxHash128 hashes to catalogs
- `Duplicates/FindDuplicateCommandHandler.cs` - Identifies duplicate files
- `FindService.cs` - File search functionality

### Infrastructure

Located in `cdeLib/Infrastructure/`:
- `FileSystemHelper.cs` - File system operations
- `FileStreamManager.cs` - Stream handling with pooling
- `BufferPool.cs`, `ObjectPool.cs`, `CollectionPool.cs` - Resource pooling
- `WorkStealingTreeTraversal.cs` - Parallel directory traversal
- `Config/` - Configuration classes

## Catalog File Formats (.cdex / .cde)

- **Extensions**: `.cdex` (current columnar format) and `.cde` (legacy tree format)
- **Naming**: Derived from drive letter, volume name, and path
  - Example: `C-V3Win7-C__users.cdex` for `C:\users\`
  - Example: `UNC-toothless_c__users_.cdex` for `\\unc\toothless\c$\users`
- **Loading**: All catalog files in the current directory or one level down are loaded (`GetColumnarFileList` for `.cdex`, `GetCacheFileList` for `.cde`)
- **Content**: Directory tree (or columns) with optional XxHash128 hashes
- **Size**: Highly efficient - 500MB for 11 billion entries
- **Format**:
  - `.cdex` - custom columnar binary, memory-mapped for zero-copy loads (not compressed)
  - `.cdex` header flags (int32 at offset 12): bit0 = hasHashes, bit1 = followJunctions (issue #66, the scan option, exposed as `ColumnarCatalogReader.FollowJunctions` / `CatalogInfoDto.FollowJunctions`), bit2 = the hash column is XxHash128 (issue #222; `ColumnarFormat.ComposeFlags` sets bit0 and bit2 together, `ParseHashKind` reads them back as `CatalogHashes.None`, `Legacy` or `XxHash128`). Absent bit = false, so older catalogs load unchanged. Version stays 1; older builds ignore the unknown bits. `.cde` cannot carry them (reads as false).
  - `.cde` - MessagePack binary serialization by default (not compressed); FlatBuffers selectable in code

## Common Operations

### Scanning

```bash
cde scan C:\users\
```
Creates catalog file for specified path. Reuses hashes from old catalogs if file metadata matches.

### Finding

```bash
cde find system.dll
cde find pattern -path -grep
```
Searches loaded catalogs. Options: `-path`, `-grep`, `-repl`, size/date filters.

### Hashing

```bash
cde hash
cde hash --filter "size:>2gb"
cde hash --filter "ext:iso;mkv" --include-unique-sizes
```
Adds XxHash128 hashes to catalog files. Two-phase: partial hash first, then promotes to full hash for potential duplicates.
`--filter` (issue #230) takes an Everything-style query (`EverythingQuery.Parse`, the search bar's parser and error
messages) and hashes only the repeated-size files it matches; `--include-unique-sizes` drops the repeated-size rule and
gives every selected file a full hash. The selection is a `HashSelection` per run, never saved. A filter only adds or
upgrades hashes; it never clears one. `ColumnarCatalogHashing.Preview` counts what a selection will read, through the
same `HashCandidateSelection` as the run, and `HashAndSave` takes a `CancellationToken` that stops before any save.

### Duplicate Detection

```bash
cde dupes
cde dupes -minSize 10MB -minHourAge 24
```
Identifies duplicate files based on hashes. Very fast (operates on catalogs, not file system).

## Performance Characteristics

### Memory Usage (legacy `.cde` figures; the Readme table is now generated per release)

| File/Folder Count | Architecture | Memory Usage | File Size |
|-------------------|--------------|--------------|-----------|
| 8,000 entries | 32-bit | 19MB | 500KB |
| 500M entries | 32-bit | 96MB | 22MB |
| 1.5B entries | 32-bit | 275MB | 65MB |
| 11B entries (7 catalogs) | 64-bit | 2.5GB | 500MB |

### Optimization Notes

- Work-stealing algorithm for parallel directory traversal
- Object pooling to minimize GC pressure
- Lock-free data structures where possible
- Partial hashing strategy reduces I/O
- File systems processed in parallel during hashing

## Current Branch: spike/refix-perf

This branch focuses on performance improvements and refactoring. Recent commits indicate:
- Microoptimizations for `DirEntry`
- Removal of pooled pair feature (not beneficial)
- Find operation improvements with parallel options
- Allocation reduction in Find operations

## Development Guidelines

### When Working on This Codebase

1. **Performance First**: This is a performance-critical application dealing with billions of entries
   - Consider memory allocations carefully
   - Use object pooling for frequently allocated objects
   - Benchmark changes that affect hot paths

2. **Catalog Compatibility**: Changes to serialization affect the `.cdex`/`.cde` file formats
   - Hash size changes require catalog recreation
   - Document breaking changes

3. **Thread Safety**: Hashing and scanning use parallel processing
   - Use lock-free structures where possible
   - Be careful with shared state

4. **Testing**: Run tests before commits
   - Unit tests in cdeLibTest
   - Specification tests in cdeLibSpec/cdeLibSpec2

### Shell & Tooling

This is a Windows environment with both PowerShell and Bash available. The two shells have **incompatible** here-string / quoting syntax — never mix them.

- **PowerShell here-string** is `@'` ... `'@` (closing `'@` must be at column 0). Only valid in the PowerShell tool.
- **Bash here-doc** is `<<'EOF'` ... `EOF`. Only valid in the Bash tool.
- Passing `@'...'@` to the Bash tool does **not** create a here-string — Bash treats the `@` characters as literal text, which (for example) prepends a stray `@` to git commit messages.
- For multi-line text (commit messages, file content) prefer the matching syntax for the tool you're calling, or write the text to a file and pass it with `-F <file>`.

### Code Patterns

- **SlimMessageBus Commands**: Business operations are commands/queries (`IRequestHandler<T>.OnHandle`)
- **Dependency Injection**: Constructor injection via Autofac
- **Logging**: Serilog with structured logging
- **Configuration**: Microsoft.Extensions.Configuration with appsettings.json

## Build & Test

### Building

```bash
# Windows
build.cmd publish

# Linux/macOS
./build.sh publish
```

Artifacts output to `./artifacts/`

### Testing

Standard .NET test runners (tests use NUnit, xUnit)

## Dependencies to Note

- **Autofac** - Dependency injection
- **SlimMessageBus** - Command/query and pub/sub messaging (in-memory)
- **MessagePack** - Serialization for the legacy `.cde` tree format (current `.cdex` format uses a custom columnar layout)
- **FlatSharp** - FlatBuffers serialization (alternative `.cde` protocol)
- **protobuf-net** - cdeWin user config file only (no longer a `.cde` protocol)
- **Serilog** - Structured logging
- **CommandLineParser** - CLI argument parsing
- **Spectre.Console** - Rich console output
- **System.IO.Hashing** - XxHash128 file hashing
- **Superpower** - Parser combinators; parses the `--template` text in `MatchTemplate` and the `--filter` text in `MatchFilter`
