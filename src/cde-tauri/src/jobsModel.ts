// The webview's copy of the sidecar's job registry: a reducer over the "snapshot" / "job" events, plus the pure
// views the Jobs tab, status bar and menus read. No React, no network: unit-tested in jobsModel.test.ts.

import { skipSummary } from "./format";
import type { JobDto, JobState, JobsSnapshot } from "./api/types";

export interface JobsModel {
  readonly jobs: ReadonlyMap<number, JobDto>;
  readonly maxConcurrent: number;
}

export const emptyJobs: JobsModel = { jobs: new Map(), maxConcurrent: 0 };

export function isActive(state: JobState): boolean {
  return state === "queued" || state === "running";
}

/** A snapshot replaces everything: it is the first event of every (re)connection. */
export function applySnapshot(snapshot: JobsSnapshot): JobsModel {
  return { jobs: new Map(snapshot.jobs.map((j) => [j.id, j])), maxConcurrent: snapshot.maxConcurrent };
}

/** Upsert one job. A terminal state is final, so a late event cannot move a finished job back to active. */
export function applyJob(model: JobsModel, job: JobDto): JobsModel {
  const cur = model.jobs.get(job.id);
  if (cur && !isActive(cur.state) && isActive(job.state)) return model;
  const jobs = new Map(model.jobs);
  jobs.set(job.id, job);
  return { ...model, jobs };
}

/** Add a job only if it is not known yet: a submit response is older than any stream event for the same job. */
export function insertJob(model: JobsModel, job: JobDto): JobsModel {
  if (model.jobs.has(job.id)) return model;
  const jobs = new Map(model.jobs);
  jobs.set(job.id, job);
  return { ...model, jobs };
}

export function removeJob(model: JobsModel, id: number): JobsModel {
  if (!model.jobs.has(id)) return model;
  const jobs = new Map(model.jobs);
  jobs.delete(id);
  return { ...model, jobs };
}

export function removeFinished(model: JobsModel): JobsModel {
  const jobs = new Map([...model.jobs].filter(([, j]) => isActive(j.state)));
  return jobs.size === model.jobs.size ? model : { ...model, jobs };
}

function activeJobs(model: JobsModel): JobDto[] {
  return sortedJobs(model).filter((j) => isActive(j.state));
}

export function anyActive(model: JobsModel): boolean {
  for (const j of model.jobs.values()) if (isActive(j.state)) return true;
  return false;
}

export function activeCount(model: JobsModel): number {
  let n = 0;
  for (const j of model.jobs.values()) if (isActive(j.state)) n++;
  return n;
}

const byId = (a: JobDto, b: JobDto) => a.id - b.id;

function endedMs(j: JobDto): number {
  const t = j.endedAtUtc ? Date.parse(j.endedAtUtc) : NaN;
  return Number.isNaN(t) ? 0 : t;
}

/** Running first, then queued (each in submit order), then finished jobs, newest first. */
export function sortedJobs(model: JobsModel): JobDto[] {
  const all = [...model.jobs.values()];
  const running = all.filter((j) => j.state === "running").sort(byId);
  const queued = all.filter((j) => j.state === "queued").sort(byId);
  const finished = all.filter((j) => !isActive(j.state)).sort((a, b) => endedMs(b) - endedMs(a) || b.id - a.id);
  return [...running, ...queued, ...finished];
}

const LABELS: Record<JobState, string> = {
  queued: "Queued",
  running: "Running",
  succeeded: "Succeeded",
  failed: "Failed",
  cancelled: "Cancelled",
};

export function statusLabel(state: JobState): string {
  return LABELS[state];
}

/** Catalog ids with a queued or running job (a rescan holds its catalog; a create has no id yet). */
export function activeCatalogIds(model: JobsModel): Set<number> {
  const ids = new Set<number>();
  for (const j of model.jobs.values()) if (isActive(j.state) && j.catalogId !== null) ids.add(j.catalogId);
  return ids;
}

export function rescanMenuItem(
  model: JobsModel,
  catalogId: number,
): { label: string; disabled: boolean; title?: string } {
  return activeCatalogIds(model).has(catalogId)
    ? { label: "Rescan", disabled: true, title: "A job for this catalog is already queued or running" }
    : { label: "Rescan", disabled: false };
}

/** Why Reload / File > Open Folder / Migrate are unavailable right now, or null when they are allowed. */
export function loadBlockedReason(model: JobsModel): string | null {
  return anyActive(model)
    ? "Unavailable while catalog jobs are queued or running (reloading would drop the catalogs they are changing)"
    : null;
}

// ---------------------------------------------------------------- text

const n = (v: number) => v.toLocaleString("en-US");

/** The Started column: when the job began, or "queued" while it waits for a free slot. */
export function startedLabel(j: JobDto): string {
  if (!j.startedAtUtc) return "queued";
  const t = new Date(j.startedAtUtc);
  return Number.isNaN(t.getTime()) ? j.startedAtUtc : t.toLocaleString();
}

/** The Progress column: phase, entries and note while active; the outcome once finished. */
export function progressNote(j: JobDto): string {
  switch (j.state) {
    case "queued":
      return "Waiting for a free slot";
    case "running":
      return [j.phase, j.entriesScanned > 0 ? `${n(j.entriesScanned)} entries` : "", j.note].filter(Boolean).join(" · ");
    case "succeeded": {
      const r = j.result;
      if (!r) return "";
      const skipped = skipSummary(r.excludedFolderCount, r.accessDeniedFolderCount, r.unreadableFolderCount);
      return `${n(r.entries)} entries${skipped ? `; ${skipped}` : ""}`;
    }
    case "failed":
      return j.error?.message ?? "Failed";
    case "cancelled":
      return "Cancelled; nothing was changed";
  }
}

/** One status-bar line per active job. */
export function statusLines(model: JobsModel): { id: number; text: string }[] {
  return activeJobs(model).map((j) => ({ id: j.id, text: statusText(j) }));
}

function statusText(j: JobDto): string {
  if (j.state === "queued") return `Queued: ${j.title}`;
  if (j.phase === "saving" || j.phase === "swapping") return `Finishing: ${j.title}…`;
  if (j.phase === "writing") return `Writing: ${j.title}…`;
  return j.entriesScanned > 0 ? `${j.title} — ${n(j.entriesScanned)} entries` : `${j.title}…`;
}
