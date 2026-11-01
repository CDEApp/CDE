---
sidebar_position: 8
title: Other commands
---

# Other commands

## update

Change the description of a catalog.

```text
cde update <catalog> [--desc <text>]
```

| Argument or option | Description |
| ------------------ | ----------- |
| `<catalog>` | The catalog to update. |
| `--desc <text>` | The new description. |

## PopulousFolders

List folders that hold many entries.

```text
cde PopulousFolders <count>
```

`<count>` is required. cde prints the folders that hold more than `<count>` entries. It is useful for finding cache and temp folders.

```batch
cde PopulousFolders 10000
```

## Interactive commands

These load the catalogs once and let you search many times.

| Command | Search type |
| ------- | ----------- |
| `cde replfind [<string>]` | Text in the name (like `find`) |
| `cde replgrep [<regex>]` | Regular expression on the name (like `grep`) |
| `cde replgreppath [<regex>]` | Regular expression on the full path (like `greppath`) |

After the first search, cde asks `Enter string to search <nothing exits>:`. Type a pattern to search again. An empty line exits.

Lines that start with `--` are commands:

| Command | Effect |
| ------- | ------ |
| `--includefiles` | Switch whether files are included in the results. |
| `--includefolders` | Switch whether folders are included in the results. |
| `--clear` | Clear the screen. |
| `--help` | List the commands. |

### repl

```text
cde repl
```

Starts a `shell>` prompt with line editing. Type `help` or `?` to list its commands, `history` or `!` to show the history, and `quit` to leave.

## treedump1

```text
cde treedump1
```

Prints the full tree of entries in every loaded catalog as text. A `#` marks an entry that has a hash and a `!` marks an entry with a path problem. It is a diagnostic tool and can print a lot.

