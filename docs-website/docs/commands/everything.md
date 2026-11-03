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

## Not supported yet

Everything functions such as `ext:`.

## Quote the query

`<`, `>`, `|` and `!` mean something to a shell. Always put the whole query in quotes.
