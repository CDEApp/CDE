---
sidebar_position: 3
title: find, findpath, grep, greppath
---

# find, findpath, grep, greppath

Search every loaded catalog.

```text
cde find     <string> [--template <format>] [--filter <test>]...
cde findpath <string> [--template <format>] [--filter <test>]...
cde grep     <regex>  [--template <format>] [--filter <test>]...
cde greppath <regex>  [--template <format>] [--filter <test>]...
```

| Command | Matches | On |
| ------- | ------- | -- |
| `find` | A piece of text | The file or folder name |
| `findpath` | A piece of text | The full path |
| `grep` | A regular expression | The file or folder name |
| `greppath` | A regular expression | The full path |

The one argument is required. Put it in quotes if it has spaces or characters that your shell uses.

```batch
cde find system.dll
cde grep "\.log$"
cde greppath "\\Windows\\System32\\.*\.dll$"
```

For AND, OR, NOT and files-only or folders-only searches, see [`cde everything`](everything.md).

## Output templates

`--template` sets how each match line is written. By default a match prints as its full path.

| Placeholder | Value |
| ----------- | ----- |
| `{catalog}` | The catalog file name without its extension, for example `C-V3Win7-C__users` |
| `{fullpath}` | The full path of the match |
| `{filename}` | The last part of the path, with its extension |
| `{directory}` | The folder that holds the match, with no trailing separator |
| `{size}` | The size, in the unit that suits it (for example `1.5 MB`), unless you give a format |
| `{modified}` | The last modified time as `yyyy-MM-dd HH:mm:ss`, unless you give a format |

Placeholder names are not case-sensitive. Write `{{` and `}}` for a literal brace, and `\t` or `\n` for a tab or a newline. Each match ends with a newline. An unknown placeholder stops the command with an error before any catalog is read.

### Size format

Add a format after a colon, for example `{size:MB}`. Each step is 1024. The result has one decimal place.

| Format | Value |
| ------ | ----- |
| `auto` | The default. The most suitable unit, with its name: `531 B`, `1.5 KB`, `5.3 GB`. A size of 0 is written as `0`. |
| `B` | The exact number of bytes, with no thousands separators: `1234567 B` |
| `KB`, `MB`, `GB`, `TB` | The size in that unit, with the unit name: `4096 KB`, `1.5 MB`. A size too small for the unit is written as `0 MB`. |

Format names are not case-sensitive.

### Modified format

Add a format after a colon, for example `{modified:yyyy-MM-dd}`. These parts are replaced and every other character is copied. They are case-sensitive: `MM` is the month and `mm` is the minutes.

| Part | Value |
| ---- | ----- |
| `yyyy` | Year, 4 digits |
| `MM` | Month, 2 digits |
| `dd` | Day, 2 digits |
| `HH` | Hour, 24-hour clock, 2 digits |
| `mm` | Minutes, 2 digits |
| `ss` | Seconds, 2 digits |

### Examples

```batch
cde grep "\.log$" --template "{catalog} {fullpath}"
cde greppath report --template "{filename}\t{directory}"
cde find .log --template "{modified:yyyy-MM-dd} {size} {fullpath}"
cde find .iso --template "{size:GB}\t{filename}"
```

## Filters

`--filter` keeps only some of the matches. For now, the only filter is `{size:...}`.

```batch
cde grep "\.iso$" --filter "{size:>4GB}"
cde find .log --filter "{size:>10KB}" --filter "{size:<4MB}"
```

Put each filter in quotes, because `<` and `>` mean redirection in a shell. Repeat `--filter` to require all of them, which is how you set a range. There is no "or".

| Part | Value |
| ---- | ----- |
| Operator | `<`, `<=`, `>`, `>=`, `=` or `!=`. A value with no operator, such as `{size:4MB}`, means `=`. |
| Value | A number, with decimals if you want (`1.5KB`), then an optional unit of `B`, `KB`, `MB`, `GB` or `TB`. With no unit it is bytes. Units are not case-sensitive and a space before the unit is allowed. |

- Each step of the units is 1024, the same as `{size:KB}` in `--template`. The size filter in cdeWin uses 1000 per step, so a limit such as 10 KB is slightly different there.
- A folder is compared by the total size of what is in it.
- An unknown filter, an unknown unit or text that is not a filter stops the command with an error before any catalog is read.
- Matches that are filtered out are not counted in the "Total found" figure.
