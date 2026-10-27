// Stamps a release version into the files that hold the Tauri app's version, which are fixed at
// 0.1.0 in source: package.json, src-tauri/tauri.conf.json and src-tauri/Cargo.toml (plus the
// cde-tauri entry in Cargo.lock, so a --locked build still matches).
//
//   node scripts/stamp-version.mjs 2.1.0
//
// tauri.conf.json's "version" is what Tauri writes into the exe's Windows version resource.
// The pure functions are exported for scripts/stamp-version.test.mjs; the CLI runs only when
// this file is the entry point. It rewrites tracked files, so run it on a throwaway checkout (CI).
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { parseVersion } from "./release-version.mjs";

// The version rules live in release-version.mjs; this accepts exactly what it accepts.
export function assertVersion(version) {
  if (parseVersion(version) === null) {
    throw new Error(
      `'${version}' is not a release version (expected MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH-(alpha|beta|rc).N, no 'v' prefix)`,
    );
  }
  return version;
}

// Set the top-level "version" of a JSON document. The text is parsed and written back with
// two-space indentation and a trailing newline, so a nested "version" key is never touched.
export function stampJson(text, version) {
  assertVersion(version);
  const obj = JSON.parse(text);
  if (obj === null || typeof obj !== "object" || Array.isArray(obj) || typeof obj.version !== "string") {
    throw new Error('no top-level "version" field found');
  }
  obj.version = version;
  return `${JSON.stringify(obj, null, 2)}\n`;
}

// Replace the version in the [package] table of Cargo.toml (not a dependency's version). Works line
// by line: from the [package] header to the next table header, the first `version = ...` line.
export function stampCargoToml(text, version) {
  assertVersion(version);
  const lines = text.split("\n");
  const start = lines.findIndex((l) => /^\s*\[package\]\s*(#.*)?\r?$/.test(l));
  if (start < 0) throw new Error("no [package] table found");
  for (let i = start + 1; i < lines.length && !/^\s*\[/.test(lines[i]); i++) {
    const m = /^(\s*version\s*=\s*)"[^"]*"(.*)$/s.exec(lines[i]);
    if (m) {
      lines[i] = `${m[1]}"${version}"${m[2]}`;
      return lines.join("\n");
    }
  }
  throw new Error("no version found in the [package] table");
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
