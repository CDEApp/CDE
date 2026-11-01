---
sidebar_position: 2
title: Configuration
---

# Configuration

`cde` and `cde-tauri` read their settings from one `appsettings.json` file. `cdeWin` is different: it reads the `appsettings.json` next to `cdeWin.exe`.

## Where the file is

cde looks in this order and uses the **first** file it finds. It does not merge files.

1. The config folder:
   - Windows: `%APPDATA%\cde\`
   - Linux and macOS: `~/.config/cde/` (or `$XDG_CONFIG_HOME/cde/`)
   - Any system: the folder in the `CDE_CONFIG_DIR` environment variable, if it is set.
2. The current directory.
3. The folder that holds the executable. The file that ships beside `cde` is found here.

If there is no file, cde writes a default `appsettings.json` to the config folder, creating the folder, and uses it. It never overwrites a file that exists. If the folder cannot be written (a read-only home folder, for example), cde uses the same defaults built in.

To change a setting, edit the file in the config folder. Or copy the file that shipped beside the executable into the config folder.

Any setting can also be given on the command line in the form `--Section:Key=Value`:

```batch
cde scan C:\ --Serilog:MinimumLevel=Information
```

## Default file

```json
{
  "AppConfig": {
    "Display": {
      "ProgressUpdateInterval": 5000
    },
    "Hashing": {
      "FirstPassSizeInBytes": 65536,
      "DegreesOfParallelism": 2
    },
    "Scan": {
      "ExcludePaths": []
    }
  },
  "Serilog": {
    "MinimumLevel": "Warning",
    "WriteTo": [{ "Name": "Console" }]
  }
}
```

The file that ships beside the executable also has a Seq sink for a log server on `localhost:5341`. Delete that entry from `Serilog.WriteTo` if you do not run one. The default file that cde writes has no Seq sink.

## AppConfig settings

| Setting | Default | Meaning |
| ------- | ------- | ------- |
| `AppConfig.Display.ProgressUpdateInterval` | `5000` | The number of entries scanned between progress updates. |
| `AppConfig.Hashing.FirstPassSizeInBytes` | `65536` | How many bytes at the start of a file the first hashing pass reads. |
| `AppConfig.Hashing.DegreesOfParallelism` | `2` | How many hashers run at once on one file system. Different file systems are always hashed in parallel. |
| `AppConfig.Scan.ExcludePaths` | `[]` | Folders that a scan records but does not go into. See below. |
| `AppConfig.Scan.UseDefaultExcludes` | `true` | Skip the built-in list of system folders on Linux. See below. |

:::caution
If you change `FirstPassSizeInBytes` after you made catalogs with hashes, delete those catalogs, scan again and hash again.
:::

## Excluding folders

`AppConfig.Scan.ExcludePaths` is a list of absolute folder paths, or bare folder names. The scan records an excluded folder as an empty entry and does not go into it. An excluded folder is not reported as an error.

- A path covers that folder and everything under it. `*` matches inside one path segment (`/home/*/.cache`). There is no `**`.
- A bare name with no separator, such as `node_modules` or `.git`, excludes a folder of that name at any depth below the scan root. `*` works inside the name. It never excludes the scan root itself.
- A trailing `/*` excludes what is inside a folder but not the folder: `/tmp/*` skips the children of `/tmp`, while `/tmp` is still recorded and entered.
- Matching is case-sensitive, except on Windows, where it ignores case and `/` and `\` are the same.
- Entries that are blank, or relative with a separator, are logged and ignored.
- The folder you scan, and every folder above it, is never excluded. `cde scan /tmp/work` still scans `/tmp/work` when `/tmp` is excluded. Paths below the scan root still apply.
- The setting applies to `cde scan`, and to creating and rescanning catalogs in cde-tauri. Nothing is stored in the catalog. A rescan uses the settings at the time of the rescan, so edit the file and rescan to change what is skipped.

Use `cde scan --exclude <path>` to add folders for one scan only.

### Default excludes on Linux

When `AppConfig.Scan.UseDefaultExcludes` is `true`, a scan on Linux also skips:

`/proc`, `/sys`, `/dev`, `/run`, `/tmp`, `/var/tmp`, `/var/cache`, `/var/spool`, `/var/lock`, `/lost+found` and `/home/*/.cache`.

`/var/spool` can hold user mail. Scan it on purpose if you want it. `ExcludePaths` adds to this list. Set the option to `false` to scan these folders. It has no effect on Windows and macOS, which have no built-in list.

## Logging

cde writes to the console with [Serilog](https://serilog.net/). It is quiet by default (`Warning`). To see which settings file it uses (`Using configuration from <path>`), set `Serilog.MinimumLevel` to `Information` in the file, or pass `--Serilog:MinimumLevel=Information`.

## cde-tauri and cdeWin

- cde-tauri reads `Cde.ConfigPath` (the folder to load catalogs from, default `.`) and `CustomCommands`. It layers `shell.json` from the same config folder over them. See [Applications](../guide/applications.md).
- cdeWin reads `CustomCommands` from the `appsettings.json` beside `cdeWin.exe`.
