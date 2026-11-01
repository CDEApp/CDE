---
sidebar_position: 3
title: Searching
---

# Searching

cde searches every catalog it loads. Choose the command by how you want to match.

| You want to | Command |
| ----------- | ------- |
| Match a piece of the file name | [`cde find`](../commands/find.md) |
| Match a piece of the full path | `cde findpath` |
| Match a regular expression on the name | `cde grep` |
| Match a regular expression on the full path | `cde greppath` |
| Combine terms with AND, OR and NOT, or restrict to files or folders | [`cde everything`](../commands/everything.md) |
| Search again and again without reloading the catalogs | [`cde replfind`, `cde replgrep`, `cde replgreppath`](../commands/other.md) |

## Shape the output

`find`, `findpath`, `grep`, `greppath` and `everything` all take two options:

- `--template` sets how each match line is written. See [Output templates](../commands/find.md#output-templates).
- `--filter` keeps only the matches that pass a test, such as a size range. See [Filters](../commands/find.md#filters).

```batch
cde grep "\.iso$" --filter "{size:>4GB}" --template "{size:GB}\t{catalog} {fullpath}"
```

## Quote your arguments

`<`, `>`, `|` and `!` mean something to a shell. Always put filters and Everything queries in quotes.

## Browse instead

To sort and browse results, use [cdeWin or cde-tauri](applications.md).
