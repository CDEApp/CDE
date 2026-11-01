---
sidebar_position: 5
title: hash
---

# cde hash

Hash files so that [`cde dupes`](dupes.md) can find duplicates.

```text
cde hash [<catalog.cdex>]
```

| Argument | Description |
| -------- | ----------- |
| `<catalog.cdex>` | Optional. One catalog to hash and save. If you leave it out, cde hashes every catalog in the current directory and one directory below. |

```batch
cde hash
cde hash C-V3Win7-C__users.cdex
```

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
