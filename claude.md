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
  - Dependencies: Autofac, MediatR, CommandLineParser, Spectre.Console

- **cdeLib** - Core library containing business logic
  - Target: .NET 10
  - Contains all catalog operations, hashing, duplicate detection
  - Uses CQRS pattern with MediatR
  - Serialization: MessagePack, FlatSharp
  - Key dependencies: Autofac, MediatR, Serilog

- **cdeWin** - Windows Forms GUI application
  - Target: .NET 10 (Windows)
  - Browse, search, and navigate catalogs visually
  - Configuration stored in Local AppData or current directory

- **cdeWeb** - Web interface (unreleased/unfinished)
  - ASP.NET MVC with SignalR, Bootstrap, Angular.js
  - Not production-ready

### Test Projects

- **cdeLibTest** - Unit tests for cdeLib
- **cdeLibSpec** - Specification/BDD tests
- **cdeLibSpec2** - Additional specification tests
- **cdeWinTest** - Tests for Windows Forms application

### Supporting Projects

- **Mono.Terminal** - Terminal/console utilities
- **Finder** - File finding utilities
- **Util** - General utilities
- **NUnit.Should** - Testing helpers

## Key Technologies & Patterns

### Architecture Patterns

- **CQRS (Command Query Responsibility Segregation)**: Commands and queries handled via MediatR
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

Multiple serialization formats supported:
- **MessagePack** - Primary catalog file format (.cde files)
- **FlatSharp** - FlatBuffers support (alternative)

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
- `Catalog/CreateCacheCommandHandler.cs` - Scans file systems, creates .cde files
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

## Catalog File Format (.cde)

- **Extension**: `.cde`
- **Naming**: Derived from drive letter, volume name, and path
  - Example: `C-V3Win7-C__users.cde` for `C:\users\`
  - Example: `UNC-toothless_c__users_.cde` for `\\unc\toothless\c$\users`
- **Loading**: All .cde files in current directory or one level down are loaded
- **Content**: Directory tree with optional MD5 hashes
- **Size**: Highly efficient - 500MB for 11 billion entries
- **Format**: MessagePack binary serialization (not compressed)

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

2. **Catalog Compatibility**: Changes to serialization affect .cde file format
   - Hash size changes require catalog recreation
   - Document breaking changes

3. **Thread Safety**: Hashing and scanning use parallel processing
   - Use lock-free structures where possible
   - Be careful with shared state

4. **Testing**: Run tests before commits
   - Unit tests in cdeLibTest
   - Specification tests in cdeLibSpec/cdeLibSpec2

### Code Patterns

- **MediatR Commands**: Business operations are commands/queries
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
- **MediatR** - Command/query pattern
- **MessagePack** - Primary serialization
- **Serilog** - Structured logging
- **CommandLineParser** - CLI argument parsing
- **Spectre.Console** - Rich console output
- **MurmurHash.Net** - Fast hashing
