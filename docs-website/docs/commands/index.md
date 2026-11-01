---
sidebar_position: 1
title: Command reference
---

# Command reference

```text
cde <command> [arguments] [options]
```

The command (also called a verb) must come first. Options come after it.

```batch
cde find afilename --template "{fullpath}"
```

Command names and option names are not case-sensitive. `cde --help` lists all commands, `cde <command> --help` shows the options of one command, and `cde --version` shows the version.

## Commands

| Command | Purpose |
| ------- | ------- |
| [`scan`](scan.md) | Scan a folder or drive and write a catalog. |
| [`find`](find.md) | Search by a piece of the file name. |
| [`findpath`](find.md) | Search by a piece of the full path. |
| [`grep`](find.md) | Search the file name with a regular expression. |
| [`greppath`](find.md) | Search the full path with a regular expression. |
| [`everything`](everything.md) | Search with an Everything-style query. |
| [`hash`](hash.md) | Hash files so duplicates can be found. |
| [`dupes`](dupes.md) | List duplicate files. |
| [`migrate`](migrate.md) | Convert old `.cde` catalogs to `.cdex`. |
| [`update`](other.md#update) | Change the description of a catalog. |
| [`PopulousFolders`](other.md#populousfolders) | List folders with many entries. |
| [`repl`, `replfind`, `replgrep`, `replgreppath`](other.md#interactive-commands) | Interactive modes. |
| [`treedump1`](other.md#treedump1) | Print the full tree of every catalog. |

## Options at a glance

| Option | Applies to | Description |
| ------ | ---------- | ----------- |
| `--desc <text>` | `scan`, `update` | Description stored in the catalog. |
| `--follow-junctions` | `scan` | Go into directory junctions and symbolic links. |
| `--exclude <path>` | `scan` | Skip a folder for this scan. Repeatable. |
| `-v`, `--verbose` | `scan` | List folders that could not be read. |
| `--template <format>` | `find`, `findpath`, `grep`, `greppath`, `everything` | Format of each match line. |
| `--filter <test>` | `find`, `findpath`, `grep`, `greppath`, `everything` | Keep only matches that pass. Repeatable. |

## Catalogs and the current directory

Every command except `scan` and `migrate <file>` works on the catalogs it finds in the **current directory and one directory below it**. Run cde from the folder that holds your `.cdex` files. See [Catalog files](../guide/catalog-files.md).

## Exit codes

| Code | Meaning |
| ---- | ------- |
| 0 | Success. |
| 1 | An error, such as a query that cannot be read. |
| 130 | You pressed Ctrl-C and the command was cancelled. |

See [Size units](../reference/value-formats.md) for size units, and [Configuration](../reference/configuration.md) for `appsettings.json`.
