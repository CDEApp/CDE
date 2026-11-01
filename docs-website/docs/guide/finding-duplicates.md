---
sidebar_position: 4
title: Finding duplicate files
---

# Finding duplicate files

Finding duplicates takes two steps.

```batch
cde hash
cde dupes
```

## Step 1: hash

[`cde hash`](../commands/hash.md) reads files on disk and stores their hashes in the catalogs. It checks that each catalog's drive can be reached first, and skips any that cannot, with a warning.

Hashing can take a long time on large drives. These features keep the work down:

- Only files that share a size with another file in the same run are hashed.
- Files on different drives are hashed in parallel.
- Files are hashed from the largest to the smallest.

### Stop early with Ctrl-C

Hashing runs in two phases. Phase one hashes the start of each candidate file. Phase two promotes some of those to a hash of the whole file.

- Press **Ctrl-C** in phase one to move to phase two.
- Press **Ctrl-C** in phase two to stop.

This way you can hash only the largest files, and run `cde dupes` on that part of the drive.

## Step 2: list the duplicates

[`cde dupes`](../commands/dupes.md) compares the hashes in all loaded catalogs. It does not touch the drives, so it is fast.

:::caution Dupes reads the catalog, not the drive
If files changed after the last `cde hash`, the report can be wrong. Hash again before you delete anything.
:::

## Hash one catalog or all of them

| Command | Effect |
| ------- | ------ |
| `cde hash` | Hash every catalog in the current directory and one below. Finds duplicates across catalogs. |
| `cde hash C-V3Win7-C__users.cdex` | Hash one catalog. Only that drive must be connected. Files whose only same-size match is in another catalog are not hashed. |

A legacy `.cde` catalog cannot be hashed. Run [`cde migrate`](../commands/migrate.md) first.
