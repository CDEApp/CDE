// Version rules for the release workflows, in one tested place. Tags are bare (no "v"):
//
//   stable       X.Y.Z                  2.1.0
//   pre-release  X.Y.Z-<channel>.N      2.1.0-beta.1     channel: alpha | beta | rc
//
// Precedence follows SemVer: for the same X.Y.Z, alpha < beta < rc < stable.
//
// The workflows call the CLI, for example:
//
//   git tag --list > tags.txt
//   node src/cde-tauri/scripts/release-version.mjs next-prerelease --base 2.1.0 --channel beta --tags-file tags.txt
//
//   latest-stable      --tags-file F                       print the highest stable tag (or nothing)
//   next-prerelease    --base X.Y.Z --channel C --tags-file F
//                                                          print the next free pre-release tag
//   validate-stable    --version V --tags-file F [--from-draft]
//   validate-prerelease --tag T --tags-file F
//   validate-release-tag --tag T --tags-file F             stable or pre-release; the tag may be in F
//   latest-draft       --releases-file F                   tag of the newest stable-named draft
//   draft-for-tag      --tag T --releases-file F           ids of the drafts for T, newest first
//                      (F is the output of `gh api --paginate [--slurp] repos/<repo>/releases`)
//   tag-kind           --tag T                             print "stable" or "prerelease"
//
// A validate command prints GitHub Actions ::error:: / ::warning:: lines and exits 1 on an error.
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

export const CHANNELS = ["alpha", "beta", "rc"];

const VERSION = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(alpha|beta|rc)\.([1-9]\d*))?$/;

/** Parse "X.Y.Z" or "X.Y.Z-(alpha|beta|rc).N"; null when it is neither. */
export function parseVersion(text) {
  const m = typeof text === "string" ? VERSION.exec(text) : null;
  if (!m) return null;
  return {
    raw: text,
    major: Number(m[1]),
    minor: Number(m[2]),
    patch: Number(m[3]),
    channel: m[4] ?? null,
    n: m[5] ? Number(m[5]) : null,
  };
}

export const isStable = (v) => v.channel === null;
export const baseOf = (v) => `${v.major}.${v.minor}.${v.patch}`;

/** SemVer precedence: negative, zero or positive. Accepts parsed versions or strings. */
export function compareVersions(a, b) {
  const x = typeof a === "string" ? parseVersion(a) : a;
  const y = typeof b === "string" ? parseVersion(b) : b;
  if (!x || !y) throw new Error(`cannot compare '${a?.raw ?? a}' and '${b?.raw ?? b}'`);
  for (const key of ["major", "minor", "patch"]) {
    if (x[key] !== y[key]) return x[key] < y[key] ? -1 : 1;
  }
  if (x.channel === null || y.channel === null) {
    if (x.channel === y.channel) return 0;
    return x.channel === null ? 1 : -1; // a pre-release is lower than its stable version
  }
  if (x.channel !== y.channel) return CHANNELS.indexOf(x.channel) < CHANNELS.indexOf(y.channel) ? -1 : 1;
  return Math.sign(x.n - y.n);
}

function parsedTags(tags) {
  return tags.map(parseVersion).filter((v) => v !== null);
}

/** The highest stable tag in the list, or null. Pre-releases and other tags are ignored. */
export function latestStable(tags) {
  const stable = parsedTags(tags).filter(isStable);
  if (stable.length === 0) return null;
  return stable.reduce((best, v) => (compareVersions(v, best) > 0 ? v : best)).raw;
}

/** The next free tag for a base and channel: the highest existing N plus one, starting at 1. */
export function nextPrerelease(base, channel, tags) {
  const b = parseVersion(base);
  if (!b || !isStable(b)) throw new Error(`base '${base}' is not a stable MAJOR.MINOR.PATCH version`);
  if (!CHANNELS.includes(channel)) throw new Error(`channel '${channel}' is not one of ${CHANNELS.join(", ")}`);
  const highest = parsedTags(tags)
    .filter((v) => v.channel === channel && baseOf(v) === base)
    .reduce((max, v) => Math.max(max, v.n), 0);
  return `${base}-${channel}.${highest + 1}`;
}

/**
 * Check a requested stable version against the existing tags.
 * Returns { errors: string[], warnings: string[] }; no errors means it can be released.
 */
export function validateStable(version, tags, { fromDraft = false } = {}) {
  const errors = [];
  const v = parseVersion(version);
  if (!v || !isStable(v)) {
    errors.push(`'${version}' is not a MAJOR.MINOR.PATCH version (no 'v' prefix).`);
    return { errors, warnings: [] };
  }
  if (tags.includes(version)) errors.push(`Tag ${version} already exists`);
  const latest = latestStable(tags);
  if (latest !== null && compareVersions(v, latest) <= 0) {
    errors.push(
      fromDraft
        ? `The release draft says ${version} but the latest tag is ${latest}, so the draft is out of date or the tag is ahead of it. Pass an explicit version input higher than ${latest}.`
        : `Version ${version} is not higher than the latest tag ${latest}.`,
    );
  }
  return { errors, warnings: [] };
}

/**
 * Check a requested pre-release tag against the existing tags: its base must be higher than the
 * latest stable tag and the tag must be new. Going backwards in channel for the same base (alpha
 * after an rc) is allowed, with a warning.
 */
export function validatePrerelease(tag, tags) {
  const errors = [];
  const warnings = [];
  const v = parseVersion(tag);
  if (!v || isStable(v)) {
    errors.push(`'${tag}' is not an X.Y.Z-(${CHANNELS.join("|")}).N pre-release version (no 'v' prefix).`);
    return { errors, warnings };
  }
  if (tags.includes(tag)) errors.push(`Tag ${tag} already exists`);
  const latest = latestStable(tags);
  const base = parseVersion(baseOf(v));
  if (latest !== null && compareVersions(base, latest) <= 0) {
    errors.push(`Base ${baseOf(v)} is not higher than the latest stable tag ${latest}.`);
  }
  const higher = parsedTags(tags)
    .filter((t) => !isStable(t) && baseOf(t) === baseOf(v) && compareVersions(t, v) > 0)
    .sort(compareVersions)
    .at(-1);
  if (higher) {
    warnings.push(`${tag} is lower than the existing pre-release ${higher.raw} for the same base.`);
  }
  return { errors, warnings };
}

/**
 * Check the tag a Release run was started for, whichever way it started (tag push, Publish
 * workflow, or a re-run). The tag itself is expected to be in the list on a re-run, so it is left
 * out of the comparison. A stable tag must be higher than every other stable tag; a pre-release's
 * base must be higher than the latest stable tag.
 */
export function validateReleaseTag(tag, tags) {
  const v = parseVersion(tag);
  if (!v) {
    return {
      errors: [`Tag '${tag}' is neither MAJOR.MINOR.PATCH nor MAJOR.MINOR.PATCH-(${CHANNELS.join("|")}).N`],
      warnings: [],
    };
  }
  const others = tags.filter((t) => t !== tag);
  return isStable(v) ? validateStable(tag, others) : validatePrerelease(tag, others);
}

// ---- Releases (the JSON of `gh api --paginate [--slurp] repos/<repo>/releases`) ----

/** Parse the releases JSON: one array, an array of pages (--slurp), or pages run together. */
export function parseReleases(text) {
  // Split the text into its top-level JSON values by tracking brackets outside strings, so a "] ["
  // inside a release body is left alone.
  const values = [];
  let depth = 0;
  let inString = false;
  let escaped = false;
  let start = -1;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inString) {
      if (escaped) escaped = false;
      else if (c === "\\") escaped = true;
      else if (c === '"') inString = false;
    } else if (c === '"') inString = true;
    else if (c === "[" || c === "{") {
      if (depth === 0) start = i;
      depth++;
    } else if (c === "]" || c === "}") {
      depth--;
      if (depth === 0) values.push(JSON.parse(text.slice(start, i + 1)));
    }
  }
  return values.flat(Infinity);
}

const byNewest = (a, b) => (a.created_at < b.created_at ? 1 : a.created_at > b.created_at ? -1 : 0);

/**
 * The tag of the newest draft release named like a stable version (X.Y.Z): the draft Release Drafter
 * keeps for the next stable release. A draft for a pre-release tag is ignored. Null if none.
 */
export function latestDraft(releases) {
  const drafts = releases
    .filter((r) => {
      const v = parseVersion(r.tag_name);
      return r.draft && v !== null && isStable(v);
    })
    .sort(byNewest);
  return drafts.length > 0 ? drafts[0].tag_name : null;
}

/** The ids of the draft releases for exactly this tag, newest first. */
export function draftsForTag(tag, releases) {
  return releases
    .filter((r) => r.draft && r.tag_name === tag)
    .sort(byNewest)
    .map((r) => r.id);
}

// ---- CLI ----

function readTags(file) {
  if (!file) throw new Error("--tags-file is required");
  return readFileSync(file, "utf8")
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean);
}

function readReleases(file) {
  if (!file) throw new Error("--releases-file is required");
  return parseReleases(readFileSync(file, "utf8"));
}

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (!a.startsWith("--")) throw new Error(`unexpected argument '${a}'`);
    const key = a.slice(2);
    const next = argv[i + 1];
    if (next === undefined || next.startsWith("--")) args[key] = true;
    else {
      args[key] = next;
      i++;
    }
  }
  return args;
}

function report({ errors, warnings }) {
  for (const w of warnings) console.log(`::warning::${w}`);
  for (const e of errors) console.log(`::error::${e}`);
  return errors.length === 0 ? 0 : 1;
}

/** Run a CLI command; returns the exit code. Output goes to the console. */
export function run(argv) {
  const [command, ...rest] = argv;
  const args = parseArgs(rest);
  switch (command) {
    case "latest-stable": {
      const latest = latestStable(readTags(args["tags-file"]));
      if (latest) console.log(latest);
      return 0;
    }
    case "next-prerelease":
      console.log(nextPrerelease(args.base, args.channel, readTags(args["tags-file"])));
      return 0;
    case "validate-stable":
      return report(validateStable(args.version, readTags(args["tags-file"]), { fromDraft: args["from-draft"] === true }));
    case "validate-release-tag":
      return report(validateReleaseTag(args.tag, readTags(args["tags-file"])));
    case "latest-draft": {
      const tag = latestDraft(readReleases(args["releases-file"]));
      if (tag) console.log(tag);
      return 0;
    }
    case "draft-for-tag":
      for (const id of draftsForTag(args.tag, readReleases(args["releases-file"]))) console.log(id);
      return 0;
    case "validate-prerelease":
      return report(validatePrerelease(args.tag, readTags(args["tags-file"])));
    case "tag-kind": {
      const v = parseVersion(args.tag);
      if (!v) {
        console.log(`::error::Tag '${args.tag}' is neither MAJOR.MINOR.PATCH nor MAJOR.MINOR.PATCH-(alpha|beta|rc).N`);
        return 1;
      }
      console.log(isStable(v) ? "stable" : "prerelease");
      return 0;
    }
    default:
      console.error(`release-version: unknown command '${command}'`);
      return 2;
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    process.exitCode = run(process.argv.slice(2));
  } catch (e) {
    console.log(`::error::release-version: ${e.message}`);
    process.exitCode = 1;
  }
}
