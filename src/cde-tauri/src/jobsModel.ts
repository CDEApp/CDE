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

export function applySnapshot(snapshot: JobsSnapshot): JobsModel {
  return { jobs: new Map(snapshot.jobs.map((j) => [j.id, j])), maxConcurrent: snapshot.maxConcurrent };
}

function wouldReopenFinished(current: JobDto, next: JobDto): boolean {
  return !isActive(current.state) && isActive(next.state);
}

export function applyJob(model: JobsModel, job: JobDto): JobsModel {
  const cur = model.jobs.get(job.id);
  if (cur && wouldReopenFinished(cur, job)) return model;
  const jobs = new Map(model.jobs);
  jobs.set(job.id, job);
  return { ...model, jobs };
}

export function addIfUnknown(model: JobsModel, job: JobDto): JobsModel {
  if (model.jobs.has(job.id)) return model;
  const jobs = new Map(model.jobs);
  jobs.set(job.id, job);
  return { ...model, jobs };
}

/** A finished job in the first snapshot, not submitted by this window: it belongs to an earlier session. */
export function isFromEarlierSession(job: JobDto, submittedIds: ReadonlySet<number>): boolean {
  return !isActive(job.state) && !submittedIds.has(job.id);
}

export function removeJob(model: JobsModel, id: number): JobsModel {
  if (!model.jobs.has(id)) return model;
  const jobs = new Map(model.jobs);
  jobs.delete(id);
  return { ...model, jobs };
}

export function removeJobs(model: JobsModel, ids: Iterable<number>): JobsModel {
  const jobs = new Map(model.jobs);
  for (const id of ids) jobs.delete(id);
  return jobs.size === model.jobs.size ? model : { ...model, jobs };
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

export function loadBlockedReason(jobsBusy: boolean): string | null {
  return jobsBusy
    ? "Unavailable while catalog jobs are queued, running or finishing (reloading would drop the catalogs they are changing)"
    : null;
}

const n = (v: number) => v.toLocaleString("en-US");

export function startedLabel(j: JobDto): string {
  if (!j.startedAtUtc) return "queued";
  const t = new Date(j.startedAtUtc);
  return Number.isNaN(t.getTime()) ? j.startedAtUtc : t.toLocaleString();
}

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
