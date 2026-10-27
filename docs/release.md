# Releasing CDE

A release is one zip, `cde-<version>-win-x64.zip`, attached to a GitHub Release, with a
`.sha256` file beside it. Installers (MSI/NSIS), code signing and non-Windows builds are not part
of it.

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
latest bare tag, and the branch is `develop`. With no `version`, the version comes from the most
recently created draft release; if that is not higher than the latest tag the run fails and says
which tag and which draft disagree. Only one Publish Release run goes at a time.

Pushing a tag such as `2.1.0` by hand also runs `release.yml`, but only if the tagged commit is on
`develop`. Prefer Publish Release.

`release.yml` keeps the release a draft until its last step: it builds, zips, updates the draft,
attaches the zip and checksum, appends new contributors (optional; a failure there is ignored), and
only then publishes. So a failed run never leaves a public release without its zip.

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

## Decisions

- **Tag format:** bare, no `v` prefix (`2.1.0`), like the existing `1.7.1` and `2.0.0`. MinVer keeps
  its default (no tag prefix). The tag trigger glob is `[0-9]*`.
- **Release branch:** `develop`. `release-publish.yml` refuses any other branch, and `release.yml`
  refuses a tag that is not on `develop`.
- **Runtime:** `cde.exe` and `cdewin.exe` are self-contained single files (win-x64), like `cdeApi`.
  Nothing needs installing, apart from WebView2 for `cde-tauri` (see below).
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
