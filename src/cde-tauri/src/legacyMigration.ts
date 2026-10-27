// Legacy .cde catalogs: the sidecar lists them instead of loading them, and the UI offers to migrate them to .cdex.
// When to prompt, the per-folder "Not now" memory, and the dialog / status-bar text. No React, no network:
// unit-tested in legacyMigration.test.ts.

import type { LegacyCatalog, MigrateProgress, MigrateResultBody } from "./api/types";
import { mb } from "./format";

// Windows paths: compare case-insensitively, ignoring separators style and a trailing separator.
function folderKey(path: string): string {
  return path.replace(/\//g, "\\").replace(/\\+$/, "").toLowerCase();
}

export function sameFolder(a: string, b: string): boolean {
  return folderKey(a) === folderKey(b);
}

/** Open the migrate dialog by itself: there are legacy catalogs and "Not now" was not chosen for this folder. */
export function shouldPromptLegacy(
  legacy: readonly LegacyCatalog[],
  folder: string | null,
  dismissed: readonly string[] | undefined,
): boolean {
  if (legacy.length === 0 || !folder) return false;
  return !(dismissed ?? []).some((d) => sameFolder(d, folder));
}

/** The "Not now" list with `folder` added (once). */
export function dismissFolder(dismissed: readonly string[] | undefined, folder: string): string[] {
  const list = [...(dismissed ?? [])];
  return list.some((d) => sameFolder(d, folder)) ? list : [...list, folder];
}

export interface LegacySummary {
  count: number;
  totalBytes: number;
  /** Can be migrated where they are. */
  writable: LegacyCatalog[];
  /** In a folder that can't be written to. */
  readOnly: LegacyCatalog[];
}

export function summarizeLegacy(legacy: readonly LegacyCatalog[]): LegacySummary {
  return {
    count: legacy.length,
    totalBytes: legacy.reduce((sum, l) => sum + l.sizeBytes, 0),
    writable: legacy.filter((l) => l.writable),
    readOnly: legacy.filter((l) => !l.writable),
  };
}

function plural(n: number, word: string): string {
  return `${n.toLocaleString("en-US")} ${word}${n === 1 ? "" : "s"}`;
}

/** The status-bar notice, or null when there is nothing to migrate. */
export function legacyNotice(legacy: readonly LegacyCatalog[]): string | null {
  return legacy.length === 0 ? null : `${plural(legacy.length, "legacy catalog")} not loaded`;
}

/** The dialog's opening line. */
export function legacyFoundText(summary: LegacySummary, folder: string): string {
  return `Found ${plural(summary.count, "legacy catalog")} (${mb(summary.totalBytes)}) in ${folder}.`;
}

/** The last part of a path, for either separator. */
export function fileName(path: string): string {
  return path.split(/[\\/]/).pop() ?? path;
}

export function migratingText(p: MigrateProgress): string {
  return `Migrating ${p.current.toLocaleString("en-US")} of ${p.total.toLocaleString("en-US")}: ${fileName(p.path)}`;
}

/** The end-of-migration report: a headline, then one line per failure and warning. */
export function migrationReport(r: MigrateResultBody): { headline: string; lines: string[] } {
  return {
    headline: `Migrated ${r.migrated.toLocaleString("en-US")} of ${plural(r.total, "catalog")}.`,
    lines: [
      ...r.failures.map((f) => `Failed: ${fileName(f.path)}: ${f.message}`),
      ...r.warnings.map((w) => `Warning: ${fileName(w.path)}: ${w.message}`),
    ],
  };
}
