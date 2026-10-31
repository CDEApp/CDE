#!/usr/bin/env bash
# Checks that a staged Linux release folder reports the release version.
#
# A mismatch means MinVer did not see the tag (cde would carry a pre-release height version such as
# 2.0.1-alpha.0.354) or the Tauri version was not stamped (it would still say 0.1.0). Either way the
# archive must not be published.
#
#   cde:        `cde --version` prints "cde <version>" optionally followed by "+<commit>".
#   cde-tauri:  the stamped "version" in tauri.conf.json and the [package] version in Cargo.toml.
#               The app cannot be run headless on the runner, so its stamped sources are checked instead.
#
# Usage:
#   verify-release-versions-linux.sh --version 2.1.0 --root artifacts/release/cde-2.1.0-linux-x64
#                                    [--tauri-src src/cde-tauri/src-tauri]
set -uo pipefail

version=""
root=""
tauri_src="src/cde-tauri/src-tauri"

while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="${2-}"; shift 2 ;;
    --root) root="${2-}"; shift 2 ;;
    --tauri-src) tauri_src="${2-}"; shift 2 ;;
    *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
  esac
done

if [ -z "$version" ] || [ -z "$root" ]; then
  echo "error: --version and --root are required." >&2
  exit 2
fi

failed=false
ok()   { echo "ok    $1"; }
fail() { echo "FAIL  $1"; failed=true; }

# Every file a release holds must be there.
for f in cde/cde cde-tauri/cde-tauri cde-tauri/cdeApi; do
  if [ ! -f "$root/$f" ]; then fail "$f: file not found"; fi
done

# cde --version, e.g. "cde 2.1.0-beta.1+abc1234": the version up to a "+" must be the tag exactly. cde reads
# appsettings.json from the current directory and exits without it, so it runs from its own folder. Its first
# line is the version (the help follows); sed reads it all, so cde is never cut off by a closed pipe.
if [ -f "$root/cde/cde" ]; then
  reported="$(cd "$root/cde" && ./cde --version 2>/dev/null | sed -n 1p | grep -Eo '[0-9][0-9A-Za-z.+-]*' | head -n1 || true)"
  if [ "${reported%%+*}" = "$version" ]; then
    ok "cde --version: $reported (expected $version)"
  else
    fail "cde --version: '$reported' (expected $version)"
  fi
fi

# Both stamped files, read with node so a nested "version" key or a dependency's version is not taken.
conf_version="$(node -e 'try{process.stdout.write(String(JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).version))}catch{}' "$tauri_src/tauri.conf.json")"
if [ "$conf_version" = "$version" ]; then
  ok "tauri.conf.json: version=$conf_version (expected $version)"
else
  fail "tauri.conf.json: version='$conf_version' (expected $version)"
fi

cargo_version="$(awk '/^\[package\]/{p=1;next} /^\[/{p=0} p && /^version[[:space:]]*=/{gsub(/.*=[[:space:]]*"/,""); gsub(/".*/,""); print; exit}' "$tauri_src/Cargo.toml" 2>/dev/null)"
if [ "$cargo_version" = "$version" ]; then
  ok "Cargo.toml: version=$cargo_version (expected $version)"
else
  fail "Cargo.toml: version='$cargo_version' (expected $version)"
fi

if [ "$failed" = true ]; then
  echo "Version check failed: not every check reports $version."
  exit 1
fi
echo "All checks report $version."
