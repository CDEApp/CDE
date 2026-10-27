// Bookkeeping for background catalog jobs. Rescans (rescan.ts, keyed by catalog id) and creates (here, keyed by a
// client-side job id: the catalog has no id yet) share the status bar and the rule that Reload / Open Folder are
// unavailable while either runs. No React, no network: unit-tested in catalogJobs.test.ts.

import { anyRescanning, statusLines as rescanStatusLines, type RescanState } from "./rescan";

export interface CreateJob {
  id: number;
  /** The folder being scanned. */
  rootPath: string;
  /** The new file's name (no extension). */
  fileName: string;
  /** Where it will be written, as the preflight reported it (used to spot a create that won a cancel race). */
  targetPath: string | null;
}

export interface CreateJobEntry extends CreateJob {
  /** "scanning" | "writing" | "saving" (as sent by the sidecar). */
  phase: string;
  entriesScanned: number;
}

/** One entry per running create (several may run at once). */
export type CreateJobs = ReadonlyMap<number, CreateJobEntry>;

export function emptyCreateJobs(): CreateJobs {
  return new Map();
}

export function beginCreate(state: CreateJobs, job: CreateJob): CreateJobs {
  if (state.has(job.id)) return state;
  const next = new Map(state);
  next.set(job.id, { ...job, phase: "scanning", entriesScanned: 0 });
  return next;
}

export function updateCreate(
  state: CreateJobs,
  id: number,
  progress: { phase: string; entriesScanned: number },
): CreateJobs {
  const cur = state.get(id);
  if (!cur) return state;
  const next = new Map(state);
  // The sidecar reports 0 entries for the writing/saving phases; keep the last real count.
  next.set(id, { ...cur, phase: progress.phase, entriesScanned: Math.max(cur.entriesScanned, progress.entriesScanned) });
  return next;
}

export function endCreate(state: CreateJobs, id: number): CreateJobs {
  if (!state.has(id)) return state;
  const next = new Map(state);
  next.delete(id);
  return next;
}

export function anyCreating(state: CreateJobs): boolean {
  return state.size > 0;
}

/** Any background catalog job at all. */
export function anyJobRunning(rescans: RescanState, creates: CreateJobs): boolean {
  return anyRescanning(rescans) || anyCreating(creates);
}

/** Why Reload / File > Open Folder are unavailable right now, or null when they are allowed. */
export function loadBlockedReason(rescans: RescanState, creates: CreateJobs): string | null {
  const rescanning = anyRescanning(rescans);
  const creating = anyCreating(creates);
  if (rescanning && creating)
    return "Unavailable while catalogs are being rescanned or created (reloading would drop the session they are changing)";
  if (rescanning)
    return "Unavailable while a catalog is being rescanned (reloading would drop the catalog being replaced)";
  if (creating) return "Unavailable while a new catalog is being created (reloading would drop the session it is joining)";
  return null;
}

// ---------------------------------------------------------------- status bar

export type JobKind = "rescan" | "create";

export function jobKey(kind: JobKind, id: number): string {
  return `${kind}:${id}`;
}

export function parseJobKey(key: string): { kind: JobKind; id: number } | null {
  const m = /^(rescan|create):(\d+)$/.exec(key);
  return m ? { kind: m[1] as JobKind, id: Number(m[2]) } : null;
}

function createText(j: CreateJobEntry): string {
  if (j.phase === "saving") return `Finishing catalog ${j.fileName}…`;
  if (j.phase === "writing") return `Writing catalog ${j.fileName}…`;
  if (j.entriesScanned > 0)
    return `Creating catalog ${j.rootPath} — ${j.entriesScanned.toLocaleString("en-US")} entries`;
  return `Creating catalog ${j.rootPath}…`;
}

/** One status-bar line per running job: rescans (by catalog id) first, then creates (by job id). */
export function jobStatusLines(rescans: RescanState, creates: CreateJobs): { key: string; text: string }[] {
  const lines = rescanStatusLines(rescans).map((l) => ({ key: jobKey("rescan", l.catalogId), text: l.text }));
  const created = [...creates.values()]
    .sort((a, b) => a.id - b.id)
    .map((j) => ({ key: jobKey("create", j.id), text: createText(j) }));
  return [...lines, ...created];
}
