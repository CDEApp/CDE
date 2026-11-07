---
sidebar_position: 5
title: hash
---

# cde hash

Hash files so that [`cde dupes`](dupes.md) can find duplicates.

```text
cde hash [<catalog.cdex>] [--filter "<query>"] [--include-unique-sizes]
```

| Argument | Description |
| -------- | ----------- |
| `<catalog.cdex>` | Optional. One catalog to hash and save. If you leave it out, cde hashes every catalog in the current directory and one directory below. |
| `--filter "<query>"` | Optional. Hash only the files that match this query. The query uses the syntax of [`cde everything`](everything.md). |
| `--include-unique-sizes` | Optional. Also hash files whose size no other file has. Each selected file gets a full hash. |

```batch
cde hash
cde hash C-V3Win7-C__users.cdex
cde hash --filter "size:>2gb"
cde hash --filter "ext:iso;mkv" --include-unique-sizes
```

## With a filter

A filter makes a large catalog fast to hash when you only need some of its duplicates. For example, `--filter "size:>2gb"` hashes only files over 2 GB.

- The filter narrows the usual candidates. A file must match the filter and share its size with another file. Add `--include-unique-sizes` to hash matching files whose size is unique too.
- Hashes that the catalog already has are kept, also for files the filter does not match. A later run without a filter hashes only the files that have no hash yet.
- The filter is for this run only. It is not saved in the catalog or the settings.
- A malformed filter stops the command before it reads a catalog. The error is `error: ... (at position N)`, the same message as `cde everything`.
- Quote the filter, because `<`, `>`, `|` and `!` have a meaning in the shell.

## With no argument

Every catalog is hashed. Files that share a size with a file in **any** of these catalogs are candidates, so this finds duplicates across drives.

## With a catalog

Only that catalog is hashed and saved. Only its drive must be connected.

Sizes are compared only inside the catalogs being hashed. A file whose only same-size match is in another catalog is not hashed this way. Run `cde hash` with no argument to find duplicates across catalogs.

A legacy `.cde` catalog cannot be hashed. Convert it first with [`cde migrate`](migrate.md).

## How it works

- **Drive check.** Before hashing, cde checks that each catalog's drive or share can be reached. It skips the ones that cannot (a disconnected drive, an offline share, a moved folder) and warns for each. Each check has 10 seconds to answer, and the checks run in parallel. Offline shares add at most about 10 seconds in total.
- **Parallel.** Files on different file systems are hashed in parallel. The file system is found by the root volume or share name.
- **Largest first.** Files are hashed in order of size, largest first.
- **Candidates only.** Only files that share a size with another file are hashed.
- **Two phases.** Phase one hashes a block at the start of each candidate file. Phase two promotes a partial hash to a hash of the whole file when another file has the same size and partial hash.

## Stop early

- Press **Ctrl-C** in phase one to move on to phase two.
- Press **Ctrl-C** in phase two to stop.

You then have hashes for the largest files only, which is enough to run [`cde dupes`](dupes.md) on a subset of a drive.

## Settings

Set the block size and the number of parallel hashers per file system in `appsettings.json`:

- `AppConfig.Hashing.FirstPassSizeInBytes` (default 65536)
- `AppConfig.Hashing.DegreesOfParallelism` (default 2)

See [Configuration](../reference/configuration.md).
