---
sidebar_position: 2
title: Catalog files
---

# Catalog files

cde creates one catalog file for each file system it scans.

## File extension

| Extension | Meaning |
| --------- | ------- |
| `.cdex` | The current catalog format. It is laid out so it can be memory-mapped and searched without reading the whole file into objects. |
| `.cde` | The older MessagePack format. Convert these with [`cde migrate`](../commands/migrate.md). |
| `.cde.migrated` | An old `.cde` that `cde migrate` has converted and renamed. |

## File names

The name comes from where you scanned.

### Windows

The name is built from the drive letter, volume name and path, or from the UNC path:

```batch
cde scan c:\users\
```

makes `C-V3Win7-C__users.cdex`, and

```batch
cde scan \\unc\toothless\c$\users
```

makes `UNC-toothless_c__users_.cdex`.

### Linux and macOS

There is no drive letter, volume label or UNC share. The name is the scan path without its leading `/`, with each `/` replaced by `_`. The path `/` becomes `root`. Characters that are not valid in a file name (`\ : $ * ? " < > |` and control characters) also become `_`. Spaces and case are kept.

| Scan path | Catalog |
| --------- | ------- |
| `/` | `root.cdex` |
| `/home` | `home.cdex` |
| `/mnt/data` | `mnt_data.cdex` |
| `/media/jason/USB1` | `media_jason_USB1.cdex` |

A rescan of the same path replaces its own catalog. Two paths can give the same name, for example `/mnt/data_x` and `/mnt/data/x`. The later scan replaces the earlier one, so rename the earlier catalog first if you want to keep both.

## Where cde looks for catalogs

cde and cdeWin load all catalog files in the **current directory and one directory below it**. Loading one level down lets you use file system permissions on sub-folders to limit which catalogs a user can see.

cde does not check catalogs for duplicates. If you copy a catalog to a new name, both copies load.

## Rescans keep your hashes

When you scan a file system that already has a catalog, cde scans first. It then loads the old catalog and copies across any hash whose path, name and size still match. The hashing work is not lost.

Only the last modified time of each entry is stored.

## Hashes

A catalog can hold MD5 hashes, added by [`cde hash`](../commands/hash.md). To save time, cde:

- hashes only files that have the same size as another file;
- first hashes a fixed-size block at the start of large files (65,536 bytes by default);
- promotes a partial hash to a hash of the whole file only when another file has the same size and the same partial hash.

:::note Older catalogs
Partial hashes made by older versions hashed the wrong block of the file (issue #37). cde detects them by the missing `PartialHashV2` flag, treats them as not hashed, and recomputes them on the next `cde hash`. Old catalogs stay readable. If you go back to an older build, run `cde hash` again.
:::

## Size and memory

These figures are examples. Real sizes depend on the length of file and folder names.

| Files and folders | Memory at launch | File size |
| ----------------: | ---------------- | --------- |
| none | 18 MB | 0 |
| 8,000 in one catalog | 19 MB | 500 KB |
| 500,000,000 in one catalog | 96 MB | 22 MB |
| 1,500,000,000 in one catalog | 275 MB | 65 MB |
| 11,000,000,000 in 7 catalogs | 1.5 GB (32-bit) / 2.5 GB (64-bit) | 500 MB |

Catalogs are not compressed inside, because that is quicker to load, especially on an SSD. They compress well if you archive them.
