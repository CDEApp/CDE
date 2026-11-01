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
| `file:` / `folder:` | Files only or folders only. They are tests on the entry, so `folder:music` means "a folder, and `music`". The default is both. |

A plain term matches the **name**, as a case-insensitive substring. Only `path:` reaches the path.

## Order of work

1. The query matches.
2. `--filter` keeps the matches that pass.
3. `--template` writes each line.

## Errors

A query that cannot be read, such as `a ||` or an unbalanced `<`, stops the command before any catalog is read. It prints `error: ... (at position N)`, prints no results, and exits with code 1.

An empty query matches every entry.

## Not supported yet

Wildcards, and Everything functions such as `ext:`.

## Quote the query

`<`, `>`, `|` and `!` mean something to a shell. Always put the whole query in quotes.
