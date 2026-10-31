# Releasing CDE

A release is two archives attached to a GitHub Release, each with a `.sha256` file beside it:
`cde-<version>-win-x64.zip` and `cde-<version>-linux-x64.tar.gz`. Installers (MSI/NSIS, `.deb`,
`.AppImage`), code signing, arm64 and macOS builds are not part of it.

## Cut a release

1. Merge the pull requests for the release into `develop`. Release Drafter keeps a draft release up
   to date on each push to `develop` (see "Labels and version bumps").
2. Open Actions, choose **Publish Release**, and set "Use workflow from" to `develop`.
3. Leave `version` empty to use the draft's version, or type one (for example `2.1.0`).
4. Run it with **dry-run** ticked (the default). It checks the version and writes a summary, and
   changes nothing.
5. Run it again with **dry-run** unticked. It creates an annotated tag, pushes it, and calls
   `release.yml`, which builds, zips and publishes the release.

The version checks are: it is `MAJOR.MINOR.PATCH`, the tag does not exist, it is higher than the
latest stable tag, and the branch is `develop`. With no `version`, the version comes from the most
recently created draft release; if that is not higher than the latest stable tag the run fails and says
which tag and which draft disagree. Only one Publish Release run goes at a time.

Pushing a tag such as `2.1.0` by hand also runs `release.yml`, but only if the tagged commit is on
`develop`. Prefer Publish Release. `release.yml` checks the tag on every path (tag push, Publish
workflows, re-run) against the other existing tags: a stable tag must be higher than every other
stable tag, and a pre-release's base must be higher than the latest stable tag. So a hand-pushed
`1.9.0` after `2.0.0` is refused, and a stable release is always marked "latest".

`release.yml` keeps the release a draft until its last step. It runs in stages: `check` (the tag),
then `build-windows` and `build-linux` in parallel, then `publish`, which needs both builds. `publish`
updates the draft, attaches both archives and their checksums, appends new contributors (optional; a
failure there is ignored), and only then publishes. So a failed build, on either platform, never
leaves a public release without its archives.

### If a release run fails

A pushed tag blocks that version: Publish Release refuses a tag that already exists. Either:

- **Re-run the build for the same tag.** Fix the cause (on `develop` if it was a workflow or script
  fix, noting the tag still points at the old commit, so a code fix needs a new tag), then in
  Actions run **Release** with "Use workflow from" `develop` and `tag` set to the tag, for example
  `2.1.0`. It checks out the tag, checks it is on `develop`, and runs the whole release again. It
  needs the draft release to still exist; it finds the draft by tag.
  Or open the failed run and use **Re-run failed jobs**.
- **Delete the tag deliberately** if the tagged commit itself is wrong: delete the tag (and the
  draft release, if you want a fresh one) on GitHub, fix `develop`, and run Publish Release again.
  Never delete a tag of a published release.

If the release was already published, there is nothing to re-run.

## Publishing a pre-release

Use a pre-release to put an alpha, beta or release candidate in people's hands before the stable
release. The version rules (parsing, ordering, the next number) are in
`src/cde-tauri/scripts/release-version.mjs`, which has unit tests; the workflows call it.

1. Open Actions, choose **Publish Pre-release**, and set "Use workflow from" to `develop`.
2. Pick the `channel`: `alpha`, `beta` or `rc`.
3. Leave `base` empty to use the next stable version from the release draft (for example `2.1.0`),
   or type the version the pre-release leads to.
4. Run it with **dry-run** ticked (the default). The summary shows the tag it would create.
5. Run it again with **dry-run** unticked. It creates an annotated tag, pushes it, and calls
   `release.yml`.

**Tag and numbering.** The tag is `X.Y.Z-<channel>.N`, for example `2.1.0-beta.1`. `N` is the
highest existing number for that base and channel plus one, starting at 1, so the next run gives
`2.1.0-beta.2`, and `2.1.0-rc.1` starts again at 1. The base must be higher than the latest stable
tag, and the tag must be new. Going backwards in channel (an `alpha` after an `rc` for the same
base) is allowed, with a warning in the log. Order is SemVer: `2.1.0-alpha.1` < `2.1.0-beta.1` <
`2.1.0-rc.1` < `2.1.0`.

**What it publishes.** `release.yml` builds and packs exactly as for a stable release, named
`cde-2.1.0-beta.1-win-x64.zip` and `cde-2.1.0-beta.1-linux-x64.tar.gz`, each with its `.sha256`. It then
creates a draft GitHub release with both archives attached, generated notes (changes since the latest stable tag), marked as a pre-release and
not "latest", and publishes it as the last step. The .NET exes get the version from MinVer, and the
Tauri app is stamped with the full pre-release version. The version check compares each exe's
ProductVersion with the tag (see "Versions in the exes" below).

**Relation to the next stable release.** A pre-release does not touch the Release Drafter draft,
which keeps collecting merged PRs for the next stable release. Release Drafter ignores published
pre-releases when it looks for the previous release (`include-pre-releases: false`), so the stable
notes cover everything since the last stable release, and the stable version is still worked out
from the labels. When you are ready, run **Publish Release** as usual: `2.1.0` is higher than
`2.1.0-rc.2` and releases normally. Publish Release compares against stable tags only.

**Re-runs.** If the build for a pre-release tag fails, run **Release** with "Use workflow from"
`develop` and `tag` set to the pre-release tag (see "If a release run fails"). A leftover draft for
the tag is replaced. The next Publish Pre-release run would pick the next number instead.

### Versions in the exes

For the tag `2.1.0-beta.1`:

| Exe | ProductVersion | FileVersion |
|---|---|---|
| `cde.exe`, `cdewin.exe`, `cdeApi.exe` (MinVer) | `2.1.0-beta.1+<commit>` | `2.1.0.0` |
| `cde-tauri.exe` (stamped) | `2.1.0-beta.1` | `2.1.0-beta.1` |

A Windows file version is numeric only, so MinVer drops the label there; Tauri writes the stamped
string as is. `verify-release-versions.ps1` checks ProductVersion (without the `+commit`) against
the tag, and FileVersion against `X.Y.Z.0` for the MinVer exes and the tag for the Tauri exe.

## The Linux archive

`cde-<version>-linux-x64.tar.gz` unpacks to `cde-<version>-linux-x64/`, with the same layout as the
Windows zip minus `cdewin` (a Windows Forms app):

```text
cde-<version>-linux-x64/
  cde/        cde (self-contained, single file) + appsettings.json
  cde-tauri/  cde-tauri + cdeApi (the sidecar)
```

- **Why tar.gz:** a zip loses the executable bit.
- **Built on Ubuntu 22.04** (`build-linux`), the oldest we support: a Linux binary needs a glibc at least as
  new as the one it was linked against. The system libraries `cde-tauri` needs at run time
  (`libwebkit2gtk-4.1` and friends) are listed in the README, not bundled.
- **Built by the same Fallout target as Windows:** `./build.sh PublishRelease --self-contained true --runtime
  linux-x64`. `--runtime` defaults to `win-x64`, and any runtime that is not a Windows one skips `cdewin`.
- **Staged and checked by two bash scripts**, `.github/scripts/stage-release-linux.sh` and
  `verify-release-versions-linux.sh`, with tests in `src/cde-tauri/scripts/linux-release-scripts.test.mjs`. The
  Windows PowerShell scripts are untouched. The version check runs `cde --version` (from its own folder, because
  `cde` reads `appsettings.json` from the current directory) and reads the stamped `tauri.conf.json` and
  `Cargo.toml`, because the app cannot be run without a display on the runner.
- **Pull requests** get a Linux smoke job (`linux` in `build.yml`): it publishes `cde` and `cdeApi` for
  `linux-x64`, checks that they start, and packs the cde archive with the staging script. It does not build the
  Tauri shell and does not run the .NET tests on Linux.
- **Known limits on Linux:** `cde scan` fails (the catalog file name is built from a drive letter and a volume
  label), and Open, Explore, Properties and custom commands do nothing in `cde-tauri` (it uses
  `NoopShellActions`). Both are in the README.

## Decisions

- **Tag format:** bare, no `v` prefix (`2.1.0`), like the existing `1.7.1` and `2.0.0`. MinVer keeps
  its default (no tag prefix). The tag trigger glob is `[0-9]*`.
- **Release branch:** `develop`. `release-publish.yml` refuses any other branch, and `release.yml`
  refuses a tag that is not on `develop`.
- **Runtime:** `cde.exe` and `cdewin.exe` are self-contained single files (win-x64), like `cdeApi`, and `cde`
  is the same for linux-x64. Nothing needs installing, apart from WebView2 for `cde-tauri` on Windows and
  WebKitGTK for `cde-tauri` on Linux (see above).
- **Release notes:** Release Drafter and PR labels, as in PdfPinata.
- **Publish settings:** `release.yml` calls the Fallout target `PublishRelease` with
  `--self-contained true`, the same target the CI build uses, so the settings live in one place
  (`build/Build.cs`). `build.cmd publish` is unchanged and still framework-dependent.
- **Version of the apps:** MinVer 8.0.0 for every .NET project, set once in
  `src/Directory.Build.props`. The Tauri app has no MinVer, so
  `src/cde-tauri/scripts/stamp-version.mjs <version>` writes the tag into `package.json`,
  `tauri.conf.json`, `Cargo.toml` and `Cargo.lock` before the build.

## Labels and version bumps

`pr-autolabel.yml` labels each pull request from its conventional-commit title. The label picks the
release-note category and the version bump (`.github/release-drafter.yml`).

| PR title | Label | Bump |
|---|---|---|
| `feat: ...` | `enhancement` | minor |
| `type!: ...` (any type, with `!`) | `breaking` | major |
| `fix: ...` | `bug` | patch |
| `perf: ...` | `area-performance` | patch |
| `docs: ...` | `documentation` | patch |
| `chore:`, `build:`, `ci:`, `style:`, `refactor:`, `test:` | `chore` | patch |
| no prefix | none | patch |

When a PR title is edited, the labels above that the new title no longer matches are removed and
the right ones added, so a `feat:` to `fix:` change moves the PR to the right category. Other labels
are left alone.

The labels must exist in the repository (`breaking`, `chore`, `enhancement`, `bug`,
`area-performance`, `documentation`, `dependencies`); the autolabeler cannot create them. The
highest bump wins. The draft's version comes from the last published release, so check it before
you release; the version input overrides it.

## What is in the zip

```
cde-<version>-win-x64/
  cde/        cde.exe, appsettings.json
  cdewin/     cdewin.exe, appsettings.json
  cde-tauri/  cde-tauri.exe, cdeApi.exe
```

`cdeApi.exe` is the sidecar. The Tauri app starts it by the name `cdeApi` from its own folder, so it
must stay next to `cde-tauri.exe`. `pnpm tauri build --no-bundle` copies it there (stripping the
target triple from `binaries/cdeApi-<triple>.exe`), and the staging script copies it from there.
Debug symbols (`.pdb`) are not shipped.

`cde.exe` reads `appsettings.json` from the current directory, so run it from its own folder (or
copy the file to where you run it).

## Requirements

`cde-tauri.exe` needs the Microsoft Edge WebView2 runtime. Windows 10 and 11 normally have it. The
release notes say so. Nothing else needs installing.

## Run the steps locally

From the repository root, with .NET 10, Node, pnpm and Rust installed (set `MinVerVersionOverride`
to fake a tag, as an untagged build reports a pre-release version):

```powershell
$v = '2.1.0'
$env:MinVerVersionOverride = $v
./build.ps1 PublishRelease --self-contained true
cd src/cde-tauri
pnpm install --frozen-lockfile
pnpm bundle-sidecar
node scripts/stamp-version.mjs $v        # edits tracked files; undo with git restore
pnpm tauri build --no-bundle
cd ../..
./.github/scripts/stage-release.ps1 -Version $v -Phase Stage
./.github/scripts/verify-release-versions.ps1 -Version $v -Root artifacts/release/cde-$v-win-x64
./.github/scripts/stage-release.ps1 -Version $v -Phase Zip
```

The zip and its checksum land in `artifacts/release`, which is ignored by git.
