#!/usr/bin/env bash
# Stages the built Linux apps into the release folder layout and writes the tar.gz and its checksum.
#
# Layout (the folder name is cde-<version>-linux-x64):
#
#   cde-<version>-linux-x64/
#     cde/        cde + appsettings.json
#     cde-tauri/  cde-tauri + cdeApi (the sidecar)
#
# There is no cdewin: it is a Windows Forms app. cde comes from the Fallout build's artifacts folder
# (`./build.sh PublishRelease --self-contained true --runtime linux-x64`). cde-tauri comes from
# `pnpm tauri build --no-bundle`; that build also copies the sidecar next to it as cdeApi.
#
# A tar.gz keeps the executable bit, which a zip does not. Debug symbols (*.pdb) are left out.
#
# Usage:
#   stage-release-linux.sh --version 2.1.0 [--artifacts artifacts] [--out artifacts/release]
#                          [--tauri-release-dir src/cde-tauri/src-tauri/target/release] [--no-tauri]
#
# --no-tauri stages cde only, for the pull request job that does not build the Tauri shell.
set -euo pipefail

version=""
artifacts="artifacts"
out="artifacts/release"
tauri_dir="src/cde-tauri/src-tauri/target/release"
with_tauri=true

# An option that takes a value must have one: "$@" here is the option and what follows it.
need_value() {
  if [ $# -lt 2 ]; then
    echo "error: $1 needs a value." >&2
    exit 2
  fi
}

while [ $# -gt 0 ]; do
  case "$1" in
    --version) need_value "$@"; version="$2"; shift 2 ;;
    --artifacts) need_value "$@"; artifacts="$2"; shift 2 ;;
    --out) need_value "$@"; out="$2"; shift 2 ;;
    --tauri-release-dir) need_value "$@"; tauri_dir="$2"; shift 2 ;;
    --no-tauri) with_tauri=false; shift ;;
    *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
  esac
done

# The version rules live in release-version.mjs (tested); ask it, so there is one pattern. It exits
# non-zero for anything that is not X.Y.Z or X.Y.Z-(alpha|beta|rc).N.
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -z "$version" ] || ! node "$here/../../src/cde-tauri/scripts/release-version.mjs" tag-kind --tag "$version" >/dev/null 2>&1; then
  echo "error: version '$version' is not MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH-(alpha|beta|rc).N (no 'v' prefix)." >&2
  exit 1
fi

name="cde-$version-linux-x64"
stage="$out/$name"

require() {
  if [ ! -f "$1" ]; then
    echo "error: Missing build output: $1" >&2
    exit 1
  fi
}

# Check everything first, so a missing file never leaves a half-staged folder or an archive.
require "$artifacts/cde/cde"
if [ "$with_tauri" = true ]; then
  require "$tauri_dir/cde-tauri"
  require "$tauri_dir/cdeApi"
fi

rm -rf "$stage" "$out/$name.tar.gz" "$out/$name.tar.gz.sha256"
mkdir -p "$stage/cde"

# Everything the cde publish wrote, subfolders included, except debug symbols.
cp -R "$artifacts/cde/." "$stage/cde/"
find "$stage/cde" -type f -name '*.pdb' -delete
chmod +x "$stage/cde/cde"

if [ "$with_tauri" = true ]; then
  mkdir -p "$stage/cde-tauri"
  cp "$tauri_dir/cde-tauri" "$tauri_dir/cdeApi" "$stage/cde-tauri/"
  chmod +x "$stage/cde-tauri/cde-tauri" "$stage/cde-tauri/cdeApi"
fi
echo "Staged $stage"

# Relative names, from inside the out folder: tar reads "C:/..." as host:path on a Windows Git Bash.
(
  cd "$out"
  tar --owner=0 --group=0 --numeric-owner -czf "$name.tar.gz" "$name"
  # `sha256sum -c` format: "<hash>  <file name>", written by hand because sha256sum marks the name with
  # "*" (binary mode) on a Windows Git Bash and not on Linux.
  printf '%s  %s\n' "$(sha256sum "$name.tar.gz" | cut -d' ' -f1)" "$name.tar.gz" > "$name.tar.gz.sha256"
  echo "Archived $out/$name.tar.gz"
  cat "$name.tar.gz.sha256"
  tar -tzvf "$name.tar.gz" | grep -v '/$' || true
)
