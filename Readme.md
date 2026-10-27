![CDE - Catalog Directory Entries](assets/logo.png)

# cde - Catalog Directory Entries

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=CDEApp_CDE&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=CDEApp_CDE)
[![codecov](https://codecov.io/gh/CDEApp/CDE/graph/badge.svg)](https://codecov.io/gh/CDEApp/CDE)

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
- [Using cde](#using-cde)
  - [Catalog files](#catalog-files)
  - [cde Operation](#cde-operation)
  - [Examples](#examples)
  - [cde scan Path](#cde-scan-path)
  - [cde find String](#cde-find-string)
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

The executable `cde.exe` can be copied around by it self. The `appsettings.json` file has some customizations to operation (see [appsettings.json](#appsettingsjson)).

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
| `%APPDATA%\com.cde.tauri\.window-state.json` | Window size and position | the Tauri window-state plugin |

- To reset the view or the colour rules, close the app and delete `ui-state.json` or `colour-rules.txt`.
- `shell.json` uses the same `CustomCommands` array as cdeWin (see [Custom Shell Commands](#custom-shell-commands)), but cdeWin does not read this file. cde-tauri reads it only at startup, so restart the app after you change it.
- Catalog files are not kept in the configuration folder. cde-tauri loads the `.cdex` files in its current directory and one level below. When you start `cde-tauri.exe` from Explorer, this is normally the folder that contains the exe. To use a different catalog folder, start the app from that folder.

## Using cde

### Catalog files

- cde creates a separate catalog file for each file system it scans.

- each catalog file ends with extension ".cdex". Catalogs made by older versions end with ".cde"; `cde migrate` converts them (see [cde migrate](#cde-migrate)).
- each catalog file created has a file name derived from the drive letter, volume name and path or UNC path to the file system.
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

This is the mode of operation that creates and updates catalog files.

When it creates new catalog files it will detect an old catalog file for the given scan target and copy any Hash values from the old file to the new file for matching file paths, file dates and sizes.

Only Last Modified Time of file system entries is captured into catalog files.

By default cde records directory junctions and symbolic links (reparse points) in the catalog but does **not** descend into them, which avoids scan cycles (for example a junction pointing back at an ancestor directory) and duplicated content. Pass `--follow-junctions` to descend into them.

```
  cde scan C:\ --follow-junctions
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

For each catalog a `.cdex` file is written beside the source `.cde`, keeping the same base name. The original `.cde` file is left in place and is not deleted, so the conversion is non-destructive. Re-running migrate simply overwrites the `.cdex` output.

For each file converted cde prints the source and destination file names, their byte sizes and the entry count, then a summary of how many catalogs were migrated. Files that cannot be loaded are skipped and reported.

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

cde reads its settings from the `appsettings.json` file in the same folder as `cde.exe`. There are 3 values that can be modified to change cde behaviour.

- `AppConfig.Display.ProgressUpdateInterval` which defaults to 5000.

  - the number of file entries scanned between progress updates.

- `AppConfig.Hashing.FirstPassSizeInBytes` which defaults to 65536 bytes.

  - If you change this value after creating catalog files that contain collected hashes you need to recreate those hash files by deleting the catalog files, recreating with scan them and then hashing them again.

- `AppConfig.Hashing.DegreesOfParallelism` which defaults to 2.
  - hashing will automatically try to do hashing on different file systems in parallel.
  - This setting defines how many concurrent processes will be hashing files on one file system.

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
