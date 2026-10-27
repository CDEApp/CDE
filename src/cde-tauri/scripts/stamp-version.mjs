// Stamps a release version into the files that hold the Tauri app's version, which are fixed at
// 0.1.0 in source: package.json, src-tauri/tauri.conf.json and src-tauri/Cargo.toml (plus the
// cde-tauri entry in Cargo.lock, so a --locked build still matches).
//
//   node scripts/stamp-version.mjs 2.1.0
//
// tauri.conf.json's "version" is what Tauri writes into the exe's Windows version resource.
// The pure functions are exported for scripts/stamp-version.test.mjs; the CLI runs only when
// this file is the entry point.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?$/;

export function assertVersion(version) {
  if (typeof version !== "string" || !SEMVER.test(version)) {
    throw new Error(`'${version}' is not a semantic version (expected MAJOR.MINOR.PATCH, no 'v' prefix)`);
  }
  return version;
}

// Replace the first top-level "version": "..." in a JSON document, keeping the formatting.
export function stampJson(text, version) {
  assertVersion(version);
  const re = /^(\s{0,4}"version"\s*:\s*)"[^"]*"/m;
  if (!re.test(text)) throw new Error('no top-level "version" field found');
  return text.replace(re, `$1"${version}"`);
}

// Replace the version in the [package] table of Cargo.toml (not a dependency's version).
export function stampCargoToml(text, version) {
  assertVersion(version);
  const re = /(\[package\][^[]*?^version\s*=\s*)"[^"]*"/ms;
  if (!re.test(text)) throw new Error("no version found in the [package] table");
  return text.replace(re, `$1"${version}"`);
}

// Replace the version of the named package in Cargo.lock.
export function stampCargoLock(text, packageName, version) {
  assertVersion(version);
  const re = new RegExp(`(\\[\\[package\\]\\]\\r?\\nname = "${packageName}"\\r?\\nversion = )"[^"]*"`);
  if (!re.test(text)) return text; // no lock entry yet; cargo will create it
  return text.replace(re, `$1"${version}"`);
}

function stampFile(path, fn) {
  const before = readFileSync(path, "utf8");
  const after = fn(before);
  writeFileSync(path, after);
  console.log(`${before === after ? "unchanged" : "stamped  "} ${path}`);
}

function main(version) {
  assertVersion(version);
  const root = join(dirname(fileURLToPath(import.meta.url)), "..");
  const tauri = join(root, "src-tauri");
  stampFile(join(root, "package.json"), (t) => stampJson(t, version));
  stampFile(join(tauri, "tauri.conf.json"), (t) => stampJson(t, version));
  stampFile(join(tauri, "Cargo.toml"), (t) => stampCargoToml(t, version));
  stampFile(join(tauri, "Cargo.lock"), (t) => stampCargoLock(t, "cde-tauri", version));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main(process.argv[2]);
  } catch (e) {
    console.error(`stamp-version: ${e.message}`);
    process.exit(1);
  }
}
