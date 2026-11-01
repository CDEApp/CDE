---
sidebar_position: 1
title: Quick start
---

# Quick start

This walks through the main use of cde: catalog a drive, search it, and find duplicates.

## 1. Scan a drive

```batch
cde scan C:\
```

This writes a catalog file such as `C-V3Win7-C__.cdex` in the current directory. See [Catalog files](catalog-files.md) for how the name is made.

Add a description if you want to remember what the catalog is:

```batch
cde scan E:\ --desc "Photo backup drive"
```

## 2. Search

cde loads every catalog in the current directory, and one folder below it.

```batch
cde find system.dll
```

For a regular expression, use `grep`:

```batch
cde grep "\.log$"
```

For more complex queries, use [`cde everything`](../commands/everything.md):

```batch
cde everything "folder:music <flac | mp3>"
```

## 3. Find duplicates

Hash the files, then list the duplicates:

```batch
cde hash
cde dupes
```

`cde dupes` only reads catalogs, so it is fast. It finds nothing until `cde hash` has run. See [Finding duplicate files](finding-duplicates.md).

## 4. Browse

Use [cdeWin or cde-tauri](applications.md) to sort and browse the same catalogs.

## Where next

- [Command reference](../commands/index.md) for every option.
- [Configuration](../reference/configuration.md) to change defaults such as folders to skip.
