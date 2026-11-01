---
sidebar_position: 5
title: Applications
---

# Applications

## cde

The command line program. It is described in the [Command reference](../commands/index.md). The `cde` executable is a single file that you can copy to any folder. It reads its settings from an `appsettings.json` file that it finds, or creates, for you. See [Configuration](../reference/configuration.md).

## cdeWin

A Windows Forms application for searching, sorting and browsing catalogs. It needs .NET 10.

cdeWin saves the layout of its window and controls (window position, search patterns, column sizes, search fields) to `cdeWinView.cfg`.

- Normally the file is in `C:\Users\<name>\AppData\Local\cde`.
- If a `cdeWinView.cfg` exists in the current directory when cdeWin starts, cdeWin uses that one instead.

### Custom shell commands

You can add your own commands to the right-click menus of the search results, directory tree and directory list. Use them to open a file or folder in another tool, such as a file manager or editor.

Add a `CustomCommands` array to the `appsettings.json` next to `cdeWin.exe`:

```json
{
  "CustomCommands": [
    {
      "Label": "Total Commander",
      "Command": "c:\\Program Files\\Totalcmd\\totalcmd64.exe",
      "Arguments": "/O /T /R=\"{path}\""
    },
    {
      "Label": "Open in VS Code",
      "Command": "code",
      "Arguments": "\"{path}\""
    }
  ]
}
```

These tokens in `Arguments` are replaced before the command starts:

| Token | Replaced with | Example |
| ----- | ------------- | ------- |
| `{path}` | The full path of the entry, with its name | `C:\dir\sub\file.txt` |
| `{dir}` | The directory that holds the entry | `C:\dir\sub` |
| `{filename}` | The name of the entry only | `file.txt` |

- cde does **not** add quotes. If a path can hold spaces, write `"{path}"` in the template.
- A command works on one selected entry, and only if that entry exists on this machine. Catalogs are portable, so they can describe files that are not here. If you select several entries, the command does not run once for each.
- Each command is its own item in the menus.
- Older versions had one fixed "Explore Alt" command in an `ExplorerAlt` section. If it is still there and no `CustomCommands` exist, cdeWin converts it to a custom command named "Explore Alt".

## cde-tauri

A second desktop application, built with Tauri (a Rust shell) around a React and TypeScript web view. It talks to a small local API (`cdeApi`) that it starts and supervises. The catalog data stays in that process, and the web view only receives small pages of results.

`cdeWin` and `cde-tauri` are both thin front ends over the same core, so searches give the same results. The `.cdex` format and the `cde` command line are the same for all.

### Settings files

cde-tauri keeps its settings in `%APPDATA%\cde` (on Linux and macOS, `~/.config/cde/` or `$XDG_CONFIG_HOME/cde/`). Set `CDE_CONFIG_DIR` to use another folder on any system.

| File | Contents | Written by |
| ---- | -------- | ---------- |
| `ui-state.json` | Panel layout, sort order, columns and other view settings | The app |
| `colour-rules.txt` | Your colour rules, as you typed them | The app |
| `shell.json` | Custom shell commands (optional) | You |
| `appsettings.json` | `Cde.ConfigPath` (the catalog folder) and `CustomCommands`. The `cde` command line reads the same file. | The app on first run, then you |
| `%APPDATA%\com.cde.tauri\.window-state.json` | Window size and position | The Tauri window-state plugin |

- To reset the view or the colour rules, close the app and delete `ui-state.json` or `colour-rules.txt`.
- `shell.json` uses the same `CustomCommands` array as cdeWin. cdeWin does not read this file. cde-tauri reads it at start, so restart after a change.
- Catalogs are not kept in the settings folder. cde-tauri loads the catalogs in its catalog folder and one level below. Use **File ▸ Open Folder…** to choose it. cde-tauri remembers the choice.
- cde-tauri does not load legacy `.cde` catalogs. If the catalog folder has `.cde` files with no `.cdex` beside them, it offers to convert them, in the same way as [`cde migrate`](../commands/migrate.md). If you choose **Not now**, it does not ask again for that folder, and the status bar keeps a **Migrate…** button.

### Everything search

Both applications have an **Everything** checkbox next to Regex. It turns on the same query syntax as [`cde everything`](../commands/everything.md). **Help ▸ Search Syntax** shows the operators.
