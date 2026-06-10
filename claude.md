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
  - Entry point for scan, find, hash, dupes, dump commands
  - Dependencies: Autofac, SlimMessageBus, CommandLineParser, Spectre.Console

- **cdeLib** - Core library containing business logic
  - Target: .NET 10
  - Contains all catalog operations, hashing, duplicate detection
  - Uses CQRS pattern with SlimMessageBus
  - Serialization: columnar `.cdex` (zero-copy, memory-mapped), plus MessagePack/FlatSharp/protobuf-net for the legacy `.cde` tree format
  - Key dependencies: Autofac, SlimMessageBus, Serilog

- **cdeAppCore** - Frontend-agnostic application core
  - Target: .NET 10 (no WinForms, no ASP.NET; references only `cdeLib`)
  - Transport-agnostic services + serializable DTOs shared by every frontend: `ICatalogSession`
    (owned, disposable catalog lifetime over mmap `.cdex`), streamed `ISearchService`
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
    `/entries/{ref}/children`, `/entries/{ref}/path`, `/search` (SSE), `/session/reload` (SSE),
    `/shell`, `/shell/commands`, `/ui-state`
  - Publishes as a self-contained single-file binary for sidecar bundling

- **cde-tauri** - Tauri (React/TypeScript) desktop frontend (`src/cde-tauri`)
  - Tauri v2 shell hosting a React/Vite webview; bundles `cdeApi` as a supervised sidecar
  - Talks to the sidecar over loopback HTTP/SSE with the handshake token; mmap catalogs live in the
    sidecar, the webview only ever sees DTO pages
  - Window geometry via the Tauri window-state plugin; app UI state via `/ui-state`
  - Not a .NET project: built with `pnpm`/`vite` + `cargo` (see `src/cde-tauri/README.md`)

- **cdeWeb** - Web interface (unreleased/unfinished)
  - ASP.NET MVC with SignalR, Bootstrap, Angular.js
  - Not production-ready

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
  - **FlatSharp** (FlatBuffers) and **protobuf-net** - alternative protocols selectable via `SerializerProtocol`

### Hashing

- **MD5Hash** - File content hashing
- **MurmurHash3** - Fast non-cryptographic hashing
- **Partial hashing strategy**: Initially hashes first 64KB, promotes to full hash when duplicates detected

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

- **appsettings.json** - Application configuration
  - `AppConfig.Display.ProgressUpdateInterval` - Progress reporting frequency (default: 5000 entries)
  - `AppConfig.Hashing.FirstPassSizeInBytes` - Partial hash size (default: 65536 bytes)
  - `AppConfig.Hashing.DegreesOfParallelism` - Parallel hashing threads per file system (default: 2)
  - `Serilog` - Logging configuration (console, Seq)

### Build System

- **Fallout Build** - Build automation (replaced Nuke)
  - `build.cmd` / `build.ps1` / `build.sh` - Build scripts (bootstrap `build/_build.csproj`)
  - `build/Build.cs` - Build definition (uses `Fallout.Common`)
  - `.fallout/` - Fallout config, parameters, and temp/log output
  - Command: `build.cmd publish` - Creates artifacts in `./artifacts`

### Key Command Handlers

Located in `cdeLib/`:
- `Catalog/CreateCacheCommandHandler.cs` - Scans file systems, writes columnar `.cdex` catalogs (reusing hashes from an existing `.cdex` when present)
- `Hashing/HashCatalogCommandHandler.cs` - Adds MD5 hashes to catalogs
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
- **Content**: Directory tree (or columns) with optional MD5 hashes
- **Size**: Highly efficient - 500MB for 11 billion entries
- **Format**:
  - `.cdex` - custom columnar binary, memory-mapped for zero-copy loads (not compressed)
  - `.cde` - MessagePack binary serialization by default (not compressed); protobuf/FlatBuffers selectable in code

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
cde hash -minSize 1MB
```
Adds MD5 hashes to catalog files. Two-phase: partial hash first, then promotes to full hash for potential duplicates.

### Duplicate Detection

```bash
cde dupes
cde dupes -minSize 10MB -minHourAge 24
```
Identifies duplicate files based on hashes. Very fast (operates on catalogs, not file system).

## Performance Characteristics

### Memory Usage (from README)

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
- **protobuf-net** - Protobuf serialization (alternative `.cde` protocol)
- **Serilog** - Structured logging
- **CommandLineParser** - CLI argument parsing
- **Spectre.Console** - Rich console output
- **MurmurHash.Net** - Fast hashing
