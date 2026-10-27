# Changelog

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

