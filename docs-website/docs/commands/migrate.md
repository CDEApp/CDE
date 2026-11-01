---
sidebar_position: 7
title: migrate
---

# cde migrate

Convert the original MessagePack `.cde` catalogs to the newer `.cdex` format. The conversion is one-way.

```text
cde migrate [<file.cde>]
```

| Argument | Description |
| -------- | ----------- |
| `<file.cde>` | Optional. One `.cde` file to convert. If you leave it out, cde converts every catalog in the current directory and one directory below. |

```batch
cde migrate C-V3Win7-C__users.cde
cde migrate
```

The `.cdex` format is laid out so it can be memory-mapped and searched without reading the whole catalog into objects. It uses less memory and loads faster on large catalogs.

## What happens to each file

1. cde writes a `.cdex` file beside the source, with the same base name.
2. cde renames the source to `.cde.migrated`, for example `C-V3Win7-C__users.cde.migrated`, so it is not found as a catalog again.

Nothing is deleted.

- A `.cde` that already has a `.cdex` of the same base name is **skipped**. An existing `.cdex` may have newer hashes, so it is never overwritten.
- An existing `.cde.migrated` is never overwritten.
- If cde cannot rename the source, it keeps the `.cdex` and prints a warning.
- Files that cannot be loaded are skipped and reported.

For each file, cde prints the source and destination names, their sizes in bytes and the entry count. At the end it prints how many catalogs it migrated.

:::note Older versions
Older versions of cde and cdeWin cannot read `.cdex` files, and do not look for `.cde.migrated` files. To use a migrated catalog with an older version, rename its `.cde.migrated` file back to `.cde`.
:::
