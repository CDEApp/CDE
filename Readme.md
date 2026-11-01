![CDE - Catalog Directory Entries](assets/logo.png)

# cde - Catalog Directory Entries

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=CDEApp_CDE&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=CDEApp_CDE)
[![codecov](https://codecov.io/gh/CDEApp/CDE/graph/badge.svg)](https://codecov.io/gh/CDEApp/CDE)

**Documentation: [cdeapp.github.io/CDE](https://cdeapp.github.io/CDE/)** - user guide and command reference.

## Contents

- [Introduction](#introduction)
  - [What is cde](#what-is-cde)
  - [Contact Author](#contact-author)
- [Applications](#applications)
  - [cde](#cde)
  - [cdeWin](#cdewin)
    - [Custom Shell Commands](#custom-shell-commands)
  - [cde-tauri](#cde-tauri)
    - [cde-tauri configuration](#cde-tauri-configuration)
  - [Linux](#linux)
- [Using cde](#using-cde)
  - [Catalog files](#catalog-files)
  - [cde Operation](#cde-operation)
  - [Examples](#examples)
  - [cde scan Path](#cde-scan-path)
  - [cde find String](#cde-find-string)
  - [cde everything Query](#cde-everything-query)
  - [cde hash](#cde-hash)
  - [cde dupes](#cde-dupes)
  - [cde dump](#cde-dump)
  - [cde migrate](#cde-migrate)
  - [Parameter Options](#parameter-options)
    - [Date Time Format for parameters](#date-time-format-for-parameters)
    - [Time Format for parameters](#time-format-for-parameters)
    - [Size format for parameters](#size-format-for-parameters)
- [Configuration and Performance](#configuration-and-performance)
  - [appsettings.json](#appsettingsjson)
  - [Catalog file sizes and memory usage](#catalog-file-sizes-and-memory-usage)

## Introduction

### What is cde

This is a utility to capture directory entries into catalog files for later processing and lookup. Processing includes capturing hashes, identifying duplicates and searching for file system entries with assorted criteria.

It is inspired by old and faithful utility called Cathy I have used in the past (Website now gone). One of the driving reasons to write cde was that Cathy is internally limited to 65535 directory entries.

> **Note: the executables are not signed.** `cde.exe`, `cdeWin.exe`, `cde-tauri.exe` and `cdeApi.exe` are not signed with a code signing certificate. When you first run a downloaded copy, Microsoft Defender SmartScreen shows "Windows protected your PC" with the publisher "Unknown publisher". To run it, click **More info**, then **Run anyway**.
>
> To stop the warning for every file at once, unblock the zip before you extract it: right-click the zip, select **Properties**, select **Unblock**, then click **OK**.
>
> If Smart App Control is on (Windows 11), Windows can block unsigned apps with no option to run them.
>
> If this is a concern, you can always check the [build pipeline](https://github.com/CDEApp/CDE/actions/workflows/release.yml) to see the source is compiled via CI.

### Contact Author

My name is Robin and you can contact me about cde on the github repository site.

This application will continue to evolve driven by my own and a few friends desires.

## Applications

### cde

This is a command line utility to scan, find, hash, and perform duplicate file identification.

The executable `cde.exe` can be copied around by itself. It reads its settings from an `appsettings.json` file, which it finds for you and creates when there is none (see [appsettings.json](#appsettingsjson)).

### cdeWin

cdeWin is a Windows Forms application for searching, sorting and browsing of catalogs.

This application reads and writes a configuration file `cdeWinView.cfg`.

- This file is normally located in the Local Application Data folder of the current user.
- On Windows the path would be
  - `C:\Users\username\AppData\Local\cde`
- If a file called cdeWinView.cfg exists in the current directory when cdeWin launches it will use the current directory for the configuration file and not the Local Application Data Folder.
- This file saves and restores information about cdeWin window and controls.
  - window location
  - search patterns
  - size of all the columns in list views
  - values of fields in the search parameters

The executable `cdeWin.exe` can be copied around by it self to be used anywhere that .NET 10 is available with the behaviour of the cdeWinView.cfg file as described just above.

#### Custom Shell Commands

cdeWin lets you add your own commands to the right-click context menu of the search results, directory tree and directory list. Use this to launch a catalogued file or folder in another tool - for example an alternate file manager, an editor, or a hashing utility.

Custom commands are configured in the `appsettings.json` file that sits next to `cdeWin.exe`. Add a `CustomCommands` array; each entry has a `Label` (the menu text), a `Command` (the executable to run) and an `Arguments` template.

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

The following tokens in the `Arguments` template are replaced with values from the selected entry before the command is launched.

| Token        | Replaced with                                  | Example                  |
| ------------ | ---------------------------------------------- | ------------------------ |
| `{path}`     | the full path of the entry, including its name | `C:\dir\sub\file.txt`    |
| `{dir}`      | the directory containing the entry             | `C:\dir\sub`             |
| `{filename}` | the entry's name only                          | `file.txt`               |

Notes:

- Substitution is literal and cde does **not** add quoting. If a path may contain spaces, quote the token yourself in the template, for example `"{path}"`.
- A command operates on a single selected entry, and only when that entry currently exists on the local file system (catalogs are portable and may describe files that are not present on this machine). Selecting multiple entries does not run the command once per entry.
- Each configured command appears as its own item in the context menus.
- Earlier versions of cdeWin had a single fixed "Explore Alt" command configured under an `ExplorerAlt` section. If that setting is still present and no `CustomCommands` are configured, it is migrated automatically into a single custom command labelled "Explore Alt", so an existing tool is not lost.

### cde-tauri

A second desktop frontend built with Tauri (a Rust shell) hosting a React/TypeScript WebView. It
talks to a small localhost API (`cdeApi`, ASP.NET / Kestrel) that it bundles and supervises as a
sidecar process, reaching it over loopback HTTP + Server-Sent Events with a startup handshake token.

Both `cdeWin` and `cde-tauri` are thin frontends over a shared, frontend-agnostic core
(`cdeAppCore`): catalog session, streamed search, shell actions and serializable DTOs. `cdeWin`
calls the core in-process (keeping the zero-copy memory-mapped read path); `cde-tauri` calls the
same services across the `cdeApi` boundary, so the catalog data stays in the sidecar and the WebView
only ever sees small pages of results. The `.cde`/`.cdex` formats and the `cde` CLI are unchanged.

See `src/cde-tauri/README.md` for how to build and run it.

#### cde-tauri configuration

cde-tauri keeps its settings in the Roaming Application Data folder of the current user (`%APPDATA%`, which is `C:\Users\username\AppData\Roaming` on Windows).

| File | Contents | Written by |
| ---- | -------- | ---------- |
| `%APPDATA%\cde\ui-state.json` | UI state: panel layout, sort order, columns and other view settings | the app, when you change the view |
| `%APPDATA%\cde\colour-rules.txt` | The colour rules, stored exactly as you entered them | the app, when you save the rules |
| `%APPDATA%\cde\shell.json` | Custom shell commands (optional) | you, by hand |
| `%APPDATA%\cde\appsettings.json` | `Cde.ConfigPath` (the catalog folder) and `CustomCommands`; cde reads the same file (see [appsettings.json](#appsettingsjson)) | created with defaults on the first run when there is none anywhere, then you, by hand |
| `%APPDATA%\com.cde.tauri\.window-state.json` | Window size and position | the Tauri window-state plugin |

- On Linux and macOS these files are in `~/.config/cde/` (or `$XDG_CONFIG_HOME/cde/`) instead of `%APPDATA%\cde\`. Set the `CDE_CONFIG_DIR` environment variable to use another folder on any system.
- To reset the view or the colour rules, close the app and delete `ui-state.json` or `colour-rules.txt`.
- `shell.json` uses the same `CustomCommands` array as cdeWin (see [Custom Shell Commands](#custom-shell-commands)), but cdeWin does not read this file. cde-tauri reads it only at startup, so restart the app after you change it.
- Catalog files are not kept in the configuration folder. cde-tauri loads the catalogs in its catalog folder and one level below. To choose the catalog folder, use **File ▸ Open Folder…**; cde-tauri remembers it (`catalogDir` in `ui-state.json`) and opens it again next time. Until you choose one, the catalog folder is the current directory: when you start `cde-tauri.exe` from Explorer, this is normally the folder that contains the exe.
- cde-tauri does not load legacy `.cde` catalogs: they load slowly and use a lot of memory. When the catalog folder has `.cde` files with no `.cdex` beside them, cde-tauri offers to convert them, in the same way as [cde migrate](#cde-migrate). If you choose **Not now**, it doesn't ask again for that folder (`legacyMigrateDismissed` in `ui-state.json`), but the status bar keeps a **Migrate…** button.

### Linux

A release also has `cde-<version>-linux-x64.tar.gz` (with a `.sha256`), for 64-bit Linux. It holds `cde/` (the `cde` command line program and its `appsettings.json`) and `cde-tauri/` (the `cde-tauri` app and its `cdeApi` sidecar). There is no `cdewin`, because it is a Windows Forms app. There are no installers: extract the archive and run the programs.

```bash
sha256sum -c cde-2.1.0-linux-x64.tar.gz.sha256
tar -xzf cde-2.1.0-linux-x64.tar.gz
cd cde-2.1.0-linux-x64/cde
./cde --help
```

- `cde` finds its settings itself, so you can run it from any folder. On the first run it writes a default `appsettings.json` to `~/.config/cde/` (or `$XDG_CONFIG_HOME/cde/`); set `CDE_CONFIG_DIR` to use another folder (see [appsettings.json](#appsettingsjson)).
- `cde-tauri` needs the system libraries for its web view. On Debian and Ubuntu: `sudo apt install libwebkit2gtk-4.1-0 libayatana-appindicator3-1`. Other distributions have packages with similar names (WebKitGTK 4.1).
- The archive is built on Ubuntu 22.04, so it needs a glibc at least as new as that one has.
- Catalog files on Linux are named after the scan path, for example `cde scan /mnt/data` makes `mnt_data.cdex` (see [Catalog files](#catalog-files)).
- The Linux builds are new, and one thing does not work yet: in `cde-tauri` on Linux, **Open, Explore and Properties do nothing**, and so do custom shell commands.

## Using cde

### Catalog files

- cde creates a separate catalog file for each file system it scans.

- each catalog file ends with extension ".cdex". Catalogs made by older versions end with ".cde"; `cde migrate` converts them (see [cde migrate](#cde-migrate)).
- each catalog file created has a file name derived from where it was scanned. On Windows that is the drive letter, volume name and path or UNC path to the file system. On Linux and macOS it is the scan path alone (see below).
  - These canonical names are not enforced, but are used when cde updates hashes on a file system that has an old catalog file available.
  - Renaming files is not a problem if you are not using hashes or don't care about hashes.
  - Duplicating a catalog to a different name will cause cde to load both original and duplicate as it does not check for duplicates catalog file contents.
- example catalog file names

  ```batch
  cde scan c:\users\
  ```

  Produces a cde file name of `C-V3Win7-C__users.cdex` on my machine.

  ```batch
  cde scan \\unc\toothless\c$\users
  ```

  Produces a cde file name of `UNC-toothless_c__users_.cdex` on my home network.

- On Linux and macOS there is no drive letter, volume label or UNC share, so the name comes from the scan path: the path without its leading `/`, with every `/` replaced by `_`. The path `/` is `root`. Characters that are invalid in a file name (`\`, `:`, `$`, `*`, `?`, `"`, `<`, `>`, `|` and control characters) also become `_`. Spaces are kept, and so is the case, because paths are case-sensitive.

  | Scan path | Catalog |
  |---|---|
  | `/` | `root.cdex` |
  | `/home` | `home.cdex` |
  | `/mnt/data` | `mnt_data.cdex` |
  | `/mnt/nas/share` | `mnt_nas_share.cdex` |
  | `/media/jason/USB1` | `media_jason_USB1.cdex` |
  | `/var/log` | `var_log.cdex` |

  There is no mount-point detection. A rescan of the same path replaces its own catalog. Two limits: `/mnt/data_x` and `/mnt/data/x` both give `mnt_data_x`, because `/` and `_` cannot be told apart, and scanning `/` and then `/root` both give `root.cdex`. Both are rare; the later scan replaces the earlier catalog, so rename the earlier catalog file first if you want to keep both. Windows names do not change.

- All catalog files in the current directory, or one directory below current directory will be loaded by cde or cdeWin when they are started.

  - The main reason for loading also from one directory down is to allow for file system permission to be applied to folders in the current directory to limit what catalog files are loaded by the current useres identity.

- Catalog files can contain MD5 Hashes for files as well if they have been added by using the "-hash" option.

- When hashing cde strives to only hash files that are of the same length as this means they could have the same content.

- When hashing cde tries to avoid work, by only hashing a fixed byte at the start of large files, to try and collect just enough information about a file to exclude it from being a duplicate of another file.

  - partial hashes are promoted to a hash of the entire file if another file is encountered with the same file length and a matching partial hash.
  - Compatibility note (issue #37): partial hashes made by older versions hashed the wrong block of the file. They are detected by the missing `PartialHashV2` flag, treated as not hashed, and recomputed by the next `cde hash`. Old catalogs stay readable and full hashes are unchanged. Downgrade caveat: an older build does not know the flag and may compare new partial hashes with old ones (missed duplicates), so re-run `cde hash` if you go back to an older build.

- When scanning a file system that you already have a catalog file for, cde will scan the file system and then load the old catalog file looking for any hashes in the old catalog file that still match on file paths, file names and file sizes to copy to the newly updated catalog file. So the work of hashing a file system with unchanged files should not be lost.

### cde Operation

Some options only apply to some modes, in those cases the mode parameter must occur before the other parameters for the command line to be valid.

Valid:

```batch
cde find afilename -path
```

Invalid:

```batch
cde path -find afilename
```

### Examples

To create a cde catalog for C:\ drive.

```batch
cde scan C:\
```

To find files containing word "system.dll" in catalogs in current directory.

```batch
cde find system.dll
```

### cde scan Path

#### Valid Options for this mode

[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)
[`--desc`](#parameter-options)
[`--follow-junctions`](#parameter-options)
[`--exclude`](#parameter-options)

This is the mode of operation that creates and updates catalog files.

When it creates new catalog files it will detect an old catalog file for the given scan target and copy any Hash values from the old file to the new file for matching file paths, file dates and sizes.

Only Last Modified Time of file system entries is captured into catalog files.

By default cde records directory junctions and symbolic links (reparse points) in the catalog but does **not** descend into them, which avoids scan cycles (for example a junction pointing back at an ancestor directory) and duplicated content. Pass `--follow-junctions` to descend into them.

```
  cde scan C:\ --follow-junctions
```

Pass `--exclude <path>` to skip a folder for this scan only. Repeat it for several folders. These add to `AppConfig.Scan.ExcludePaths` and the default excludes and follow the same rules (see `AppConfig.Scan.ExcludePaths` below). Nothing is saved in the catalog or the config.

```
  cde scan /data --exclude /data/cache --exclude "/data/*/tmp"
```

### cde find String

#### Valid Options for this mode

[`-basePath`](#parameter-options)
[`-path`](#parameter-options)
[`-grep`](#parameter-options)
[`-repl`](#parameter-options)
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)

This is the mode for finding files in your catalogs using assorted filter criteria. If you wish to sort and browse it is suggested you use use cdeWin rather than cde.

`find`, `findpath`, `grep` and `greppath` take `--template` to set how each match line is written. By default a match prints as its full path.

| Placeholder | Value |
|---|---|
| `{catalog}` | The catalog file name without its extension, for example `C-V3Win7-C__users` |
| `{fullpath}` | The full path of the match |
| `{filename}` | The last part of the path, with its extension |
| `{directory}` | The folder that holds the match, with no trailing separator |
| `{size}` | The size, in the unit that suits it (for example `1.5 MB`) unless you give a format |
| `{modified}` | The last modified time, as `yyyy-MM-dd HH:mm:ss` unless you give a format |

`{size}` takes a format after a colon, for example `{size:MB}`. Sizes use 1024 for each step and one decimal place, the same as the sizes in cdeWin and the Tauri app.

| Format | Value |
|---|---|
| `auto` | The default. The most suitable unit, with its name: `531 B`, `1.5 KB`, `5.3 GB`. A size of 0 is written as `0`. |
| `B` | The exact number of bytes, with no thousands separators: `1234567 B` |
| `KB`, `MB`, `GB`, `TB` | The size in that unit, followed by the unit name: `4096 KB`, `1.5 MB`. A size too small for the unit is written as `0 MB`. |

The format names are not case-sensitive.

`{modified}` takes a format after a colon, for example `{modified:yyyy-MM-dd}`. These parts of the format are replaced, and every other character is copied as it is. They are case-sensitive: `MM` is the month and `mm` is the minutes.

| Part | Value |
|---|---|
| `yyyy` | Year, 4 digits |
| `MM` | Month, 2 digits |
| `dd` | Day, 2 digits |
| `HH` | Hour, 24-hour clock, 2 digits |
| `mm` | Minutes, 2 digits |
| `ss` | Seconds, 2 digits |

Placeholder names are not case-sensitive. Write `{{` and `}}` for a literal brace, and `\t` or `\n` for a tab or a newline. Each match ends with a newline. An unknown placeholder stops the command with an error before any catalog is read.

```batch
cde grep "\.log$" --template "{catalog} {fullpath}"
cde greppath report --template "{filename}\t{directory}"
cde find .log --template "{modified:yyyy-MM-dd} {size} {fullpath}"
cde find .iso --template "{size:GB}\t{filename}"
```

`find`, `findpath`, `grep` and `greppath` also take `--filter` to keep only some of the matches. For now the only filter is `{size:...}`.

```batch
cde grep "\.iso$" --filter "{size:>4GB}"
cde find .log --filter "{size:>10KB}" --filter "{size:<4MB}"
```

Put the filter in quotes, because `<` and `>` mean redirection in a shell. Repeat `--filter` to require all of them, which is how you set a range. There is no "or".

| Part | Value |
|---|---|
| Operator | `<`, `<=`, `>`, `>=`, `=` or `!=`. A value with no operator, such as `{size:4MB}`, means `=`. |
| Value | A number, with decimals if you want (`1.5KB`), then an optional unit of `B`, `KB`, `MB`, `GB` or `TB`. With no unit it is bytes. Units are not case-sensitive and a space before the unit is allowed. |

Each step of the units is 1024, the same as `{size:KB}` in `--template`. The size filter in cdeWin uses 1000 per step, so a limit such as 10 KB is slightly different there. A folder is compared by the total size of what is in it. An unknown filter, an unknown unit or text that is not a filter stops the command with an error before any catalog is read. Matches that are filtered out are not counted in the "Total found" figure.

### cde everything Query

#### Valid Options for this mode

`--template` and `--filter`, as for `find`. Nothing else: the query itself decides everything below.

`cde everything` searches your catalogs with the same Everything-style query as the Everything checkbox in cdeWin and cde-tauri (see [voidtools' search syntax](https://www.voidtools.com/support/everything/search_syntax/)). `find`, `findpath`, `grep` and `greppath` do not change.

```batch
cde everything "do win"
cde everything "folder:music <flac | mp3>"
cde everything "file: .mp3 !tmp"
cde everything "path:system32 hosts" --template "{catalog} {fullpath}"
cde everything "file: .iso" --filter "{size:>4GB}"
```

| Syntax | Meaning |
|---|---|
| `a b` | A space is AND: the name has both `a` and `b`. `do win` matches `Windows`. |
| `a \| b` | OR (a space binds tighter than `\|`). |
| `!a` | NOT. |
| `<a \| b> c` | A group. |
| `"a b"` | A literal phrase. Operators inside the quotes are plain text. |
| `path:term` | Match the full path. It goes directly in front of one word, phrase or group, with no space. |
| `no-path:term` | Match the name only. |
| `file:` / `folder:` | Files only or folders only. They are tests on the entry, so `folder:music` means "a folder, and `music`". The default is both. |

A plain term matches the name, as a case-insensitive substring. Only `path:` reaches the path. There are no regex, path, files or folders options for this command: use `path:`, `file:` and `folder:` in the query.

Put the query in quotes, because `<`, `>`, `|` and `!` mean something to a shell. The query matches first, then `--filter` keeps the matches that pass, then `--template` writes each line.

A query that cannot be read, such as `a ||` or an unbalanced `<`, stops the command with `error: ... (at position N)` and exit code 1 before any catalog is read, and prints no results. An empty query matches every entry. Wildcards and Everything functions such as `ext:` are not supported yet.

### cde hash

#### Valid Options for this mode

[`-basePath`](#parameter-options)
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)
[`-minHourAge`](#parameter-options)

This mode exists to provide file hashes to the Dupes mode.

- With no argument, every catalog in the current directory and one directory below is hashed.
- With a path argument, only that one `.cdex` catalog is hashed and saved. Only its file system needs to be connected.

  ```batch
  cde hash C-V3Win7-C__users.cdex
  ```

  Hashing compares file sizes only within the catalogs being hashed. So a file whose only same-size match is in another catalog is not hashed this way: run `cde hash` with no argument to find duplicates across catalogs. A legacy `.cde` catalog can't be hashed; convert it first with [cde migrate](#cde-migrate).

  Failures: a file that cannot be read (locked, removed, access denied) is skipped, counted, and reported at the end as `Hashing finished with N errors`; the run still exits 0. An unexpected error that stops a hashing phase does not discard the work: the hashes completed so far are saved, then `cde hash` prints one summary line (`Hash FAILED: N files hashed, M files could not be read; ...`) and exits 1. A catalog that cannot be saved also exits 1.

Before hashing, cde checks that each catalog's drive or share can be reached, and skips the catalogs that can't (a disconnected drive, an offline share or a moved folder), with a warning for each one. Each check has 10 seconds to answer, and the checks run in parallel, so offline network shares add at most about 10 seconds instead of a network timeout for every file.

It hashes files on different file systems in parallel. File systems are determined in a naïve manner by a simple grouping of the root Volume or Share Name.

It will hash files in descending file size order.

If you have Hash running in Phase one and hit Ctrl-C it will actually move from Phase one of hashing to Phase two where files are checked if they need to be promoted to Full Hash to determine uniqueness.
This second phase can also be interrupted with Ctrl-C.
By doing this you can limit the hashing of files to try and run Dupes on a subset of the larger files in the cataloged file system.

You can also use the option -minSize to limit the size of Hashed files and Dupes reported files

### cde dupes

#### Valid Options for this mode

[`-basePath`](#parameter-options)
[`-minSize`](#parameter-options)
[`-maxSize`](#parameter-options)
[`-minDateTime`](#parameter-options)
[`-maxDateTime`](#parameter-options)
[`-minTime`](#parameter-options)
[`-maxTime`](#parameter-options)
[`-minHourAge`](#parameter-options)

This mode looks for duplicate files in all catalog files available.
It does no hashing itself and is very fast, however it does depend on hash being available in the catalog file and will not find any duplicates if no hashing has been done. So you will need to run Hash mode before using Dupes.

Dupes does not look at files on the file system at all, so if you run Dupes on a set of catlog files and it reports file duplicate and the file system has changed since the hashing of the catalogs was done you will not get useful results - Be Careful -

Consider using -minHourAge to limit Hash and Dupes work if you are cleaning up file systems.

### cde dump

#### Valid Options for this mode

`No options supported.`

Output the full tree of file entries in the catologs in text format.

### cde migrate

#### Valid Options for this mode

`No filter options supported.`

This mode performs a one-way conversion of the original MessagePack `.cde` catalogs into the newer zero-copy columnar `.cdex` format. The `.cdex` format is laid out so it can be memory-mapped and searched without first deserialising the whole catalog into objects, which lowers memory use and speeds up load on large catalogs.

- With a path argument, only that single `.cde` file is converted:

  ```batch
  cde migrate C-V3Win7-C__users.cde
  ```

- With no argument, every catalog discovered in the current directory and one directory below is converted (the same discovery rule used when loading catalogs):

  ```batch
  cde migrate
  ```

For each catalog a `.cdex` file is written beside the source `.cde`, keeping the same base name. Then the source is renamed to `.cde.migrated` (for example `C-V3Win7-C__users.cde.migrated`), so it is not found as a catalog again. Nothing is deleted, so the conversion is non-destructive.

- A `.cde` that already has a `.cdex` of the same base name is skipped, so an existing `.cdex` (which may hold newer hashes) is never overwritten.
- An existing `.cde.migrated` is never overwritten. If the source can't be renamed, the `.cdex` is kept and cde prints a warning.

For each file converted cde prints the source and destination file names, their byte sizes and the entry count, then a summary of how many catalogs were migrated. Files that cannot be loaded are skipped and reported.

> **Note:** older versions of cde and cdeWin can't read `.cdex` files and don't look for `.cde.migrated` files. To use a migrated catalog with an older version, rename its `.cde.migrated` file back to `.cde`.

### Parameter Options

|     | Parameter&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; | Description                                                                                                                                                                                                                                                                                                   |
| --: | :-------------------------------------------------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
|     | `-basePath {Path}`                                                                                              | Defines the set of base paths to search, hash or find entries from, if not specified it defaults to the root of each catalog loaded. May take multiple parameters or be used multiple times as a parameter. If the path specified appears in more than one catalog file you will get results for every match. |
|     | `-path`                                                                                                         | Include the path to a file when finding matching entry names.                                                                                                                                                                                                                                                 |
|     | `-grep`                                                                                                         | The search parameter is treated as a regular expression for finding matches.                                                                                                                                                                                                                                  |
|     | `-repl`                                                                                                         | The first search will occur and the cde will prompte for subsequent searches when search complete. An empty search exits the loop.                                                                                                                                                                            |
|     | `-minSize {Size}`                                                                                               | Minimum size of file in bytes to include in processing.                                                                                                                                                                                                                                                       |
|     | `-maxSize {Size}`                                                                                               | Maximum size of file in bytes to include in processing.                                                                                                                                                                                                                                                       |
|     | `-minHours {Int}`                                                                                               | Minimum age of file in hours to include in processing.                                                                                                                                                                                                                                                        |
|     | `-minDate {DateTime}`                                                                                           | Minimum date and time inclusive to include in processing.                                                                                                                                                                                                                                                     |
|     | `-maxDate {DateTime}`                                                                                           | Maximum date and time inclusive to include in processing.                                                                                                                                                                                                                                                    |
|     | `-minTime {T}`                                                                                                  | Minimum time inclusive to include in processing. This ignores the date part of file time.                                                                                                                                                                                                                     |
|     | `-maxTime {T}`                                                                                                  | Maximum time inclusive to include in processing. This ignores the date part of file time.                                                                                                                                                                                                                     |
|     | `-maxResults {Int}`                                                                                             | Maximum number of results returned by cde.                                                                                                                                                                                                                                                                    |
|     | `-exclude {Regex}`                                                                                              | A filter to exclude only entries that match these regexes for processing.                                                                                                                                                                                                                                     |
|     | `-include {Regex}`                                                                                              | A filter to include only entries that match these Regexes for processing.                                                                                                                                                                                                                                     |
|     | `--desc {Text}`                                                                                                 | (scan) Description text to store in the catalog file.                                                                                                                                                                                                                                                          |
|     | `--follow-junctions`                                                                                            | (scan) Descend into directory junctions and symbolic links (reparse points). Off by default; reparse points are recorded but not followed, avoiding scan cycles and duplicated content.                                                                                                                        |
|     | `--exclude {Path}`                                                                                              | (scan) Folder to skip for this scan only. Repeatable. Adds to `AppConfig.Scan.ExcludePaths` and the default excludes; same pattern rules.                                                                                                                                                                       |

##### Date Time Format for parameters

`<YYYY>-<MONTH>-<DD>T<HH>:<MM>:<SS>`

- Month field is an integer 1-12 or short Month Name
- There is no need to fill out all the fields of the date format to get a valid value.
- You may just specify from the beginning the parts you need.
- The `T` between date and time is required and must be upper case.

Valid Examples:

- "2014"
- "2014-12"
- "2014-Dec"
- "2014-Dec"
- "2014-12-02"
- "2014-12-02T"
- "2014-12-02T13"
- "2014-12-02T13:10"
- "2014-12-02T13:10:30"

##### Time Format for parameters

`<HH>:<MM>:<SS>`

- The time is 24 hour time format. 0 - 23
- There is no need to fill out all the fields of the time format to get a valid value.

Valid Examples:

- "9"
- "9:30"
- "9:30:15"

##### Size format for parameters

The following suffixes case independent on size fields modify there value.

| Suffix | Description | Multiplier |
| ------ | ----------- | ---------- |
| KB     | Kilobytes   | 1000       |
| MB     | Megabytes   | 1000^2     |
| GB     | Gigabytes   | 1000^3     |

Suffixes must follow number with no spaces.

## Configuration and Performance

### appsettings.json

cde and cde-tauri read their settings from one `appsettings.json` file. They look for it in this order and use the first one they find, on its own (the files are not merged):

1. the config folder: `~/.config/cde/` (or `$XDG_CONFIG_HOME/cde/`) on Linux and macOS, `%APPDATA%\cde\` on Windows, or the folder in the `CDE_CONFIG_DIR` environment variable when that is set;
2. the current directory;
3. the folder that holds the executable (the file shipped beside `cde.exe` and `cde` is found here).

If there is none, the program writes a default `appsettings.json` into the config folder (creating the folder) and uses it, so a first run needs no setup. A file that already exists is never overwritten. If the folder cannot be written (a read-only home folder, for example) the program carries on with the same defaults built in. To change a setting, edit the file in the config folder, or copy the one shipped beside the executable there.

cde logs which settings it uses at Information level, so it is quiet by default. To see `Using configuration from <path>`, set `Serilog.MinimumLevel` to `Information` in the file, or pass `--Serilog:MinimumLevel=Information`.

The file shipped beside the executables has a Seq sink for a log server on `localhost:5341`; delete that entry from `Serilog.WriteTo` if you do not run one. The default that cde writes has no Seq sink.

cde reads `AppConfig` and `Serilog`. The cde-tauri sidecar reads `Cde.ConfigPath` (the folder catalogs load from, default `.`) and `CustomCommands`, and `shell.json` in the same config folder is layered over it. cdeWin is unchanged: it reads the `appsettings.json` beside `cdeWin.exe`.

There are 5 values in `AppConfig` that can be modified to change cde behaviour.

- `AppConfig.Display.ProgressUpdateInterval` which defaults to 5000.

  - the number of file entries scanned between progress updates.

- `AppConfig.Hashing.FirstPassSizeInBytes` which defaults to 65536 bytes.

  - If you change this value after creating catalog files that contain collected hashes you need to recreate those hash files by deleting the catalog files, recreating with scan them and then hashing them again.

- `AppConfig.Hashing.DegreesOfParallelism` which defaults to 2.
  - hashing will automatically try to do hashing on different file systems in parallel.
  - This setting defines how many concurrent processes will be hashing files on one file system.

- `AppConfig.Scan.ExcludePaths` which defaults to empty.

  - A list of absolute folder paths, or bare folder names, that a scan records but does not go into. The folder is kept as an empty entry; nothing under it is scanned, and an excluded folder is not reported as an error.
  - A path covers that folder and everything under it. `*` matches inside one path segment (`/home/*/.cache`); there is no `**`.
  - A bare name with no path separator, such as `node_modules` or `.git`, excludes a folder of that name at any depth below the scan root (`*` works inside the name). It never excludes the scan root itself, even when the root has that name.
  - A trailing `/*` excludes what is inside a folder but not the folder itself: `/tmp/*` skips the children of `/tmp`, while `/tmp` is still recorded and entered.
  - Matching is case-sensitive, except on Windows where it ignores case and `/` and `\` are the same. Entries that are blank, or relative with a separator, are logged and ignored.
  - The folder you scan, and any folder above it, is never excluded: `cde scan /tmp/work` still scans `/tmp/work` when `/tmp` is excluded. Paths below the scan root still apply.
  - It applies to `cde scan`, and to creating and rescanning catalogs in cde-tauri. Nothing is stored in the catalog, and a rescan uses the settings at the time of the rescan, so edit the file and rescan to change what is skipped.

- `AppConfig.Scan.UseDefaultExcludes` which defaults to true.
  - On Linux, also skips `/proc`, `/sys`, `/dev`, `/run`, `/tmp`, `/var/tmp`, `/var/cache`, `/var/spool`, `/var/lock`, `/lost+found` and `/home/*/.cache`. Note `/var/spool` can hold user mail; scan it explicitly if you want it. `ExcludePaths` adds to this list.
  - Set it to false to scan those folders. It has no effect on Windows and macOS, which have no built-in list.

### Catalog file sizes and memory usage

These are examples and actual file sizes and memory footprints may vary depending
on the length of file and folder names.

|      Count Files and Folders | Description | Memory at Launch | File Size |
| ---------------------------: | ----------- | ---------------- | --------- |
|          no files or folders | 32 bit cde  | 18MB             | 0         |
|         8,000 in one catalog | 32 bit cde  | 19MB             | 500KB     |
|   500,000,000 in one catalog | 32 bit cde  | 96MB             | 22MB      |
| 1,500,000,000 in one catalog | 32 bit cde  | 275MB            | 65MB      |
|  2,000,000,000 in 3 catalogs | 32 bit cde  | 360MB            | 77 MB     |
| 11,000,000,000 in 7 catalogs | 32 bit cde  | 1.52 GB          | 500 MB    |
| 11,000,000,000 in 7 catalogs | 64 bit cde  | 2.5 GB           | 500 MB    |

The 64 or AnyCPU build on a 64 bit Windows uses a lot more memory than the 32 bit version.

The catalog files are not compressed internally as the use cases I have had till now makes them quicker without compression especially on SSD. They can be compressed quite well.
