---
sidebar_position: 4
title: everything
---

# cde everything

Search every loaded catalog with an Everything-style query. The syntax is the same as the **Everything** checkbox in cdeWin and cde-tauri. It follows [voidtools' search syntax](https://www.voidtools.com/support/everything/search_syntax/).

```text
cde everything "<query>" [--template <format>] [--filter <test>]...
```

The query decides everything about the match. So this command has **no** regex, path, files or folders options. It takes `--template` and `--filter` as [`find`](find.md) does, and nothing else.

```batch
cde everything "do win"
cde everything "folder:music <flac | mp3>"
cde everything "file: .mp3 !tmp"
cde everything "path:system32 hosts" --template "{catalog} {fullpath}"
cde everything "file: .iso" --filter "{size:>4GB}"
```

## Syntax

| Syntax | Meaning |
| ------ | ------- |
| `a b` | A space is AND: the name has both `a` and `b`. `do win` matches `Windows`. |
| `a \| b` | OR. A space binds tighter than `\|`. |
| `!a` | NOT. |
| `<a \| b> c` | A group. |
| `"a b"` | A literal phrase. Operators inside the quotes are plain text. |
| `path:term` | Match the full path. It goes directly in front of one word, phrase or group, with no space. |
| `no-path:term` | Match the name only. |
| `regex:term` | The term is a regular expression, matched anywhere in the text, ignoring case. Quote one that has a space or `\|`. Modifiers chain: `path:regex:^C:`. |
| `case:term` | Match case. Plain terms, wildcards and `regex:` ignore case unless `case:` is in front. It chains: `case:path:Docs`. |
| `diacritics:term` | Accepted so Everything queries can be pasted. Diacritics always count here (`e` and `é` are different), so it changes nothing. |
| `file:` / `folder:` | Files only or folders only. They are tests on the entry, so `folder:music` means "a folder, and `music`". The default is both. |

A plain term matches the **name**, as a case-insensitive substring. Only `path:` reaches the path.

## Order of work

1. The query matches.
2. `--filter` keeps the matches that pass.
3. `--template` writes each line.

## Errors

A query that cannot be read, such as `a ||` or an unbalanced `<`, stops the command before any catalog is read. It prints `error: ... (at position N)`, prints no results, and exits with code 1.

An empty query matches every entry.

## Wildcards

| Wildcard | Matches |
|---|---|
| `*` | Zero or more characters, but not a backslash (`\`). |
| `**` | Zero or more characters, including backslashes. |
| `?` | Exactly one character, but not a backslash. |

A term with a `*` or `?` must match the whole text it is tested against, so `*.txt` is a name that ends in `.txt` and `a*b` starts with `a` and ends with `b`. A term with no wildcard is still a substring. Wildcards are case-insensitive and also work inside quotes. The backslash rule only matters for a path, for example `path:C:\**\*.txt`.

## Functions

A function is a test on a property of the entry, like `file:` and `folder:`, so it joins the AND, OR, NOT and group tree: `file: size:>100mb dm:1week`. Dashes in a function name are optional.

| Function | Tests |
|---|---|
| `size:` | The size in bytes. |
| `date-modified:` or `dm:` | The modified time. |

A value starts with an optional operator:

| Form | Meaning |
|---|---|
| `size:1kb` | Equal within the granularity of the value: `size:1kb` is 1024 to 2047 bytes. |
| `size:==1kb`, `size:=1kb` | Exactly equal. |
| `size:!=1kb` | Not exactly equal. |
| `size:!1kb` | Not within the granularity. |
| `size:<10mb`, `size:<=10mb`, `size:>10mb`, `size:>=10mb` | Less than, at most, more than, at least. |
| `size:2mb..3mb`, `size:2mb-3mb` | In the range, ends included. |

Sizes take the units `b`, `kb`, `mb`, `gb` and `tb` (also `k`, `m`, `g`, `t` and `kib` and so on). They are binary, so `1kb` is 1024 bytes. A bare number is bytes, and `1.5kb` is allowed.

For dates, the granularity is the part you write, so `dm:2026` is the whole year and `dm:2026-07-02` is the whole day. `>` means after the whole granule and `<=` includes all of it. A range uses `..` (not `-`) and includes all of the last granule.

| Value | Meaning |
|---|---|
| `today`, `yesterday`, `tomorrow` | That day, in local time. |
| `YYYY[-MM[-DD[Thh[:mm[:ss[.sss]]]]]]` | An ISO 8601 date or time, such as `2026-07-02T10:30`. |
| `10secs`, `10mins`, `2hours`, `2days`, `3weeks`, `4months`, `1year` | In the last that long: `dm:3days` is "since 3 days ago", `dm:>3days` is newer than that and `dm:<3days` is older. |

The catalog stores the time in local time, and the value is read in local time too.

`size:` and `dm:` work together with the size and date controls of the GUIs: an entry has to pass both. `size:unknown` is not supported.

### ext:

`ext:mp3;flac` matches files whose whole extension is one of those. A semicolon separates extensions, a leading `.`, `*` or `*.` is ignored, and `*` and `?` work (`ext:mp*`, `ext:*.txt;*.doc*`). `ext:` with no value matches files with no extension. Folders have no extension, so `ext:mp3 | folder:` means mp3 files or folders. The extension is the text after the last dot, so `a.tar.gz` is `gz`, `.gitignore` has none and `a.` has an empty one. `case:ext:JPG` matches case.

### empty:, child-count:, descendant-count:

| Function | Matches folders with |
|---|---|
| `empty:` | No files and no subfolders. Same as `child-count:0`. |
| `child-count:`, `child-file-count:`, `child-folder-count:` | That many files and folders (or files only, or folders only) directly inside. |
| `descendant-count:`, `descendant-file-count:`, `descendant-folder-count:` | That many at any depth. |

The value takes the same forms as `size:`: `child-count:10`, `child-count:20..30`, `descendant-count:>1000`, `child-file-count:1 child-folder-count:0`. The counts come from the catalog, not from the disk. They test folders only, so `!empty:` also matches files: write `folder: !empty:` for non-empty folders.

### dupe:

`dupe:` matches entries that share a property with another entry in **any of the loaded catalogs**, so the same file on two drives is a duplicate. A semicolon combines properties: `dupe:size;name` needs both to be equal. With no property it compares the name.

| Property | Compares |
|---|---|
| `name` | The name (default). |
| `path` | The full path. |
| `ext` | The extension. |
| `size` | The size in bytes. |
| `date-modified` or `dm` | The modified time. |
| `hash` | The hash stored in the catalog. |

Text is compared ignoring case; `case:dupe:name` matches case. `dupe:hash` uses the hash that `cde hash` already stored and never reads a file. A file with no full hash (not hashed yet, or only a partial hash) is left out of `dupe:hash`, so run `cde hash` first. The other properties do not need hashes: `dupe:size` also finds files that were never hashed. The counting pass reads every loaded catalog before any result appears, and it keeps one small entry per distinct value in memory.

## Not supported yet

Other Everything functions, `size:unknown`, and anything that reads the disk, such as `child-count-from-disk:`.

## Quote the query

`<`, `>`, `|` and `!` mean something to a shell. Always put the whole query in quotes.
