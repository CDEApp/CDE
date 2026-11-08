# Changelog

## Unreleased

### Search

- feature (cdeWin, cde-tauri): New **Everything** checkbox next to Regex turns on Everything-style search syntax
  (https://www.voidtools.com/support/everything/search_syntax/). Iteration 1 supports the operators only: space is
  AND, `|` is OR, `!` is NOT, `<>` groups and `"..."` is a literal phrase. For example `do win` matches `Windows`.
  Terms are case-insensitive substrings of the name (or the full path with Include path). It turns Regex off while
  ticked, is off by default and is remembered. A malformed query keeps the old results and shows a message with the
  position. Wildcards and functions such as `ext:` are not supported yet. (#148)
- feature (cdeWin, cde-tauri): The Everything mode understands the `path:` and `no-path:` modifiers. `path:system32
  hosts` matches `hosts` in a folder path containing `system32`; `no-path:` matches the name only even when Include
  path is on. A modifier goes directly in front of one word, phrase or group (`path:<a | b>`), and `!path:foo` negates
  it. The `\:` alias and the global `?path:` form are not supported yet. (#148)
- feature (cdeWin, cde-tauri): The Everything mode understands the `file:` and `folder:` filters. `folder:music`
  matches folders only and `file: .mp3` matches files only; with neither it searches files and folders. `!file:`
  gives folders and `<file: a> | <folder: b>` works. While Everything is ticked the Files/Folders choice is ignored
  and greyed out (it shows both), and your choice comes back when you untick it. Include path is ignored and greyed
  out too in this mode: use `path:` to match the path, otherwise a term matches the name. (#150)
- feature (cdeWin, cde-tauri): The About menu is now **Help** (in cdeWin About moved out of File). **Help > Search Syntax**
  shows the Everything-search operators, modifiers and filters with examples. (#150)
- feature (cde): New `cde everything "<query>"` command runs the same Everything-style query as the GUIs: space is
  AND, `|` is OR, `!` is NOT, `<>` groups, `"..."` is a phrase, `path:` and `no-path:` choose the path or the name, and
  `file:` and `folder:` choose the kind. It takes `--template` and `--filter` and nothing else. A malformed query
  prints `error: ... (at position N)` and exits 1 before any catalog is read. Quote the query, since `<`, `>`, `|` and
  `!` mean something to a shell. `find`, `findpath`, `grep` and `greppath` are unchanged. (#152)

- feature (cdeWin, cde-tauri, cde): The Everything mode understands the voidtools wildcards. `*` matches any
  characters except `\`, `**` matches any characters including `\`, and `?` matches exactly one character except
  `\`. A term with a wildcard must match the whole name (or the whole path after `path:`), so `*.txt` finds names
  that end in `.txt` and `a*b` is anchored at both ends; a term with no wildcard is still a substring. Wildcards are
  case-insensitive and work inside quotes. Help > Search Syntax lists them. (#217)

- feature (cdeWin, cde-tauri, cde): The Everything mode understands the functions `size:` and `date-modified:`
  (short `dm:`), and the `regex:` modifier. `size:>10mb`, `size:1kb` (1024 to 2047 bytes), `size:2mb..3mb`,
  `size:!=1kb` and the other numeric forms work; `dm:today`, `dm:2026`, `dm:2026-07-02..2026-07-03`,
  `dm:2026-07-02T10:30` and spans such as `dm:3days` (newer than 3 days ago) work. `regex:^\d{4}` matches the name
  with a regular expression, `path:regex:...` the full path; quote a regular expression that has a space or `|`.
  `size:` and `dm:` work together with the Size and Date controls: an entry has to pass both. A `content` target
  and `size:unknown` are not supported. Help > Search Syntax lists them. (#219)

- feature (cdeWin, cde-tauri, cde): The Everything mode understands `ext:`, `empty:`, the child counts, the descendant
  counts and `dupe:`, and the `case:` and `diacritics:` modifiers. `ext:mp3;flac` and `ext:*.doc*` match the whole
  extension of a file, `ext:` alone files with no extension. `empty:`, `child-count:20..30`,
  `child-file-count:1 child-folder-count:0` and `descendant-count:>1000` test folders by how many files and folders
  they hold, counted in the catalog. `dupe:` finds entries that share a property (`name` by default, or `path`,
  `ext`, `size`, `date-modified`, `hash`, joined with `;`) with another entry in ANY loaded catalog. `dupe:hash` uses
  the hash already stored by `cde hash` and never reads a file: a file with no full hash is left out of it, and the
  other properties do not need hashes. `case:` makes terms, wildcards, `regex:`, `ext:` and `dupe:` match case;
  `diacritics:` is accepted and changes nothing, since diacritics always count. Help > Search Syntax lists them. (#221)
- refactor (cdeLib): `EverythingQuery` is split into smaller files for the grammar, the evaluation context, the
  syntax tree nodes, the terms and the functions. No change in behavior. (#221)

- feature (cdeWin, cde-tauri, cde): The Everything mode understands the `catalog:` modifier. It tests the next term,
  phrase or group against the file name of the catalog the entry is in (the name on disk, such as `photos.cdex`)
  instead of the entry, so `catalog:backup ext:mp3` finds mp3 files only in catalogs with `backup` in their file name.
  Wildcards, `case:catalog:`, `regex:catalog:` and groups work, and `!catalog:old` excludes catalogs. It cannot be
  combined with `path:` or `no-path:` on one term. A catalog that cannot match is not scanned, and `dupe:` still counts
  over all loaded catalogs, so `catalog:photos dupe:hash` lists files in the photos catalog that exist anywhere else.
  Help > Search Syntax lists it. (#224)
- fix (cdeWin, cde-tauri): An empty search box with Regex ticked no longer fails with the error `Bad Regex: Pattern
  is Null or Empty`. It searches the same as an empty box without Regex, so only the filters apply. (#250)

### cde-tauri

- fix: On Linux, the TreeMap tooltip joins a path with `/`, so it shows `/usr/sbin/NetworkManager`, not
  `/usr/sbin\NetworkManager`. (#248)
- fix: On Linux, the text in a closed dropdown such as the TreeMap Depth is readable. The page now tells the web view
  that it is dark, so WebKitGTK draws dark dropdowns instead of light ones. On Windows, scrollbars and the date
  picker icon also follow the dark theme. (#249)
- feature: The TreeMap Depth control is now − / + buttons around the value instead of a dropdown, and the mouse wheel
  over it changes the depth (up is deeper). The range is still 1 to 5. (#257)
- feature: The TreeMap lightens the tile under the mouse, or the one focused with the arrow keys, and gives it an inner
  shadow, so it is easy to see on light and dark tiles. The small white dot is still shown. (#253)
- feature: In Compare, the Diffs toggle shows the mode picked from its ▾ menu, such as `Diff | Left Orphans` or
  `Diff | Differences, No Orphans`, instead of always `Diffs`. The full name is still in the tooltip. (#252)
- feature: In Compare, an ↑ button beside each Base field moves that side up one folder (greyed out at the catalog
  root). The → and ← buttons between the two sides copy one side's Base path to the other, which keeps its own
  catalog, so the same folder in an old and a new scan is one click away. A side with no catalog takes the other's
  catalog too. (#255)

### Release

- feature (release): Each release now also has `cde-<version>-linux-x64.tar.gz` (with a `.sha256`) beside the
  Windows zip. It holds `cde` and `cde-tauri` with its `cdeApi` sidecar, built on Ubuntu 22.04, with no
  installers and no `cdewin`. The release is published only when both builds succeed. **Known limit on Linux:**
  Open, Explore and Properties do nothing in `cde-tauri`. A Linux smoke job now runs on every pull request. (#154)
- fix (cde): `cde scan` now works on Linux and macOS. It used to fail there, because the catalog file name was
  built from a drive letter and a volume label. On those systems the catalog is named after the scan path: the
  path without its leading `/`, with every `/` replaced by `_` (`/mnt/data` makes `mnt_data.cdex`, `/` makes
  `root.cdex`). Windows names do not change, and existing catalogs keep their names. (#155)

### CLI

- chore (cde): Removed the `PopulousFolders` verb. It listed folders with more than `<count>` entries from the legacy `.cde` catalogs; there is no direct replacement. (part of #190)
- chore (cde): Removed the `loadwait` verb. It loaded the legacy `.cde` catalogs and waited for Enter, only to measure load time by hand; use `cdeBenchmarks` or `cdeMemProbe` instead. `treedump1` stays for now. (part of #190)

### Configuration

- feature (cde, cde-tauri): `cde` and the `cdeApi` sidecar now find `appsettings.json` themselves. They look in the config
  folder first (`~/.config/cde` or `$XDG_CONFIG_HOME/cde` on Linux and macOS, `%APPDATA%\cde` on Windows, or the folder in
  the new `CDE_CONFIG_DIR` variable), then the current directory, then the folder that holds the executable, and use the
  first file they find on its own. When there is none, a default is written to the config folder, so `cde` runs from any
  folder with no setup; before, it stopped with an error without the file in the current directory. If the folder cannot
  be written the built-in defaults are used. The default has no Seq sink, and `HashingSection.FirstPassSizeInBytes` now
  defaults to 65536 like the shipped file (it was 1024 for a file that left the setting out; set `Hashing.FirstPassSizeInBytes`
  explicitly to keep a different size, because catalogs do not record the size their partial hashes used). `ui-state.json`, `colour-rules.txt` and `shell.json` follow `CDE_CONFIG_DIR` too.
  The Windows zip and the Linux archive still ship `appsettings.json` beside the executable. (#158)

### Hashing

- breaking (hashing): `cde hash` now hashes with XxHash128 instead of MurmurHash3 and stores all 128 bits. On 20 GB of
  duplicate files the hash phase went from 10.2 s to 7.3 s and is now limited by the disk. A printed hash equals
  `xxh128sum` output. Catalogs hashed by an older version read as "not hashed": `cde hash` recomputes them, a rescan
  does not reuse them, `cde migrate` writes no hashes, and `cde dupes` and `dupe:hash` ignore them. `cde hash` and
  `cde dupes` print one line for each such catalog. `cde update --description` re-saves a `.cde` without its old
  hashes. An older build that reads a new catalog can miss duplicates, so run `cde hash` again after going back. (#222)

## 2026/10/27 2.1.0-beta.1 (pre-release)

First release from the new pipeline: one zip, `cde-2.1.0-beta.1-win-x64.zip`, with `cde`, `cdewin` and the new
`cde-tauri` app. All are self-contained; only cde-tauri needs the WebView2 runtime, which Windows 10/11 normally has.
Full PR list: https://github.com/CDEApp/CDE/compare/2.0.0...2.1.0-beta.1

### Breaking / compatibility

- breaking (all): Catalogs are now written in the columnar `.cdex` format. It is memory-mapped, so loading is near
  instant and uses little RAM. Legacy `.cde` catalogs still load. (#31)
- breaking (hashing): Partial hashes now cover exactly the first block (#37). Partial hashes in older catalogs are
  treated as "not hashed" and recomputed by the next `cde hash`; full hashes are unchanged. Don't run an older build
  over catalogs hashed by this one.
- change (all): Targets .NET 10.
- change (cdeWin): Sizes now round half away from zero and use `.` as the decimal separator in every locale, and
  show the next unit at boundaries (`1 MB`, not `1024 KB`). This matches cde-tauri. (#76)

### cde-tauri (new desktop app)

- feature: New Tauri desktop frontend, with the `cdeApi` sidecar. Directory tree and listing, search with streamed
  results, Catalogs tab and TreeMap view. (#52)
- feature: Folder total sizes in a right-aligned tree column (#71). Each catalog root shows its catalog name,
  aligned next to the size. (#77, #81)
- feature: Sortable Name / Size / Modified columns in the Directory listing. Folders stay first. (#84)
- feature: User-defined colour rules (regex on name → style) in a new Colours tab, saved automatically. (#78)
- feature: Rescan a catalog in the background while you keep browsing. Hashes and the description carry over, and the
  old catalog is restored if anything fails. (#87)
- feature: Create a new catalog from the app (File > New Catalog… or the Catalogs tab). (#92)
- feature: Keyboard navigation in the Directory tree: arrows, Home/End. (#93)
- feature: The Directory tab keeps its expanded folders, selection and scroll position when you switch tabs. (#90)
- feature: TreeMap tiles show their size under the name when it fits. (#94)
- improvement: Directory listing and folder tree are virtualized for very large folders. (#88)
- fix: Mantine dialogs (New catalog, unreachable root) now have proper spacing and sizes. (#97)

### cde (CLI) and cdeLib

- feature: The scan option "follow junctions" is stored in the catalog, and a rescan reuses it. (#85)
- improvement: Faster scanning: directory enumeration, index-based hash reuse and a direct `.cdex` write. (#55)
- improvement: Faster hashing: full-file hashes stream in chunks, with no per-file allocations. (#53)
- improvement: Faster duplicate finding: it works over `.cdex` readers and only pairs repeated sizes. (#57)
- improvement: Lower memory for paths and search; regex search times out instead of hanging. (#21, #54, #59)
- fix: Warn when `appsettings.json` is missing. (#28)

### cdeWin

- feature: The Log tab now shows the app's log (Serilog) events, including startup and catalog loading. (#72)
- improvement: The About box shows a short commit hash and has a tidier layout. (#70)
- fix: Catch the error when the alternative Explorer is undefined or can't be launched. (#22)

### Build and release

- feature: GitHub release pipeline. Publish Release cuts a stable release and Publish Pre-release cuts alpha / beta /
  rc. Release notes are drafted from PR labels. (#98, #100)
- improvement: SonarCloud analysis and Codecov coverage in CI. Markdown-only changes skip the build. (#48, #50, #83)
- improvement: Unit tests for the cde-tauri frontend (Vitest). (#73)

## 2022- vNEXT

- improvement: Reduce filesize a bit
- improvement (CDE): Tidy up help commands
- improvement (CDE): Better display of progress
- fix (CDEWin): Fix error on close.

## 2017/06/04 11:42 cdeWin v1.7.1.0, cde v1.7.1.0

- Fixed bug in cdeWin explore to file/folder.

## 2016/06/19 20:22 cdeWin v1.6.0.0, cde v1.6

- Clean up.
- Added populousfolders.

## 2014/12/19 08:13 cdeWin v1.5.0.0, cde v1.5

- Updated VS.
- Updated to latest AlphaFS can now hash files with names could not open before.
- example. files with a trailing "fullstop" or "hash" in file name or folder path.
- Bumped version.

## 2014/08/18 xx:xx cdeWin v1.4.0.0, cde v1.4

- Updated AlphaFS library to 1.6Dev to address filestream open problem discovered.

## 2014/08/17 19:48 cdeWin v1.2.8.0, cde v1.3

- Fixed bug in duplicate file finding, likely many large files were not found as duplicates.

## 2013/12/17 17:42 v1.2.8.0

- Modified progress reporting, search button turns pinkish while searching now as well.

## 2013/11/11 16:03 v1.2.7.0

- No longer focuses pattern search box whenever window activated. Was to annoying.
- Now sorts Directory Tab list view columns consistently.

## 2013/02/01 23:48 v.1.2.6.0

- Added Right Click Context Copy Full path to clipboard for selected items in Search Result Pane.
- Also shortcut to ctrl-c.

## 2013/02/01 13:20 v.1.2.5.0

- Fixed a bug catalogs were not sorted at first level directory. (old bug)

## 2012/09/12 15:13 v1.2.4.0

- Fixing sorty by column For columns > 6 in Catalog tab.
- Added new column to Catalog tab for Scan Duration.
- Fixed a thrown exception in contact author in about box if mailto: was setup.
- Added a Reload catalogs button to the Catalog tab. It will use more ram using it.

## 2012/07/16 20:35 v1.2.3.0

- Renamed the Space and Used columns in catalog list view to "Available" "Size".
- Added a Used Column which shows Size of catalog.
- These fields previously were previously miss labelled.

## 2012/07/13 cdeWin v1.2.2.0

- Directory Tree view now has context menu.
- Context menus dont appear unless an item is under cursor now.
- Fixed NPE starting cdeWin with no profile config.

##2012/07/11 cdeWin v1.2.1.0 

- Shows column sort order arrows now, really bugged me I didnt have them.

## 2012/07/11 cdeWin v1.2.0.0

- Much much improved auto complete on the Pattern entry field.
- up down arrow now navigates back/forward through history, drop down arrow now shows history.
- It limits itself to remembering 50 patterns

##2012/07/09 cdeWin v1.1.0.0

- About box, contact link.
- Loader window at startup, with logging.

## 2012/03/13 cdeWin 

- Read lock cde files.
- Fixed build for .Net 4 references even with .Net 4.5 installed.
- Icon.
- Load cde files one directory down as well as current dir.

## 2012/01/15 cdeWin 

- Fixed a redraw problem in Directory pane list view.

## 2012/01/04 cdeWin

- Added advanced search
- Moved configuration file to profile storage.
- Loads catalogs from current dir and profile dir.

## 2011/12/30 cdeWin

- Fixed list view render crashes in caching.

##2011/12/26 cdeWin

- Initial gui viewer version.

