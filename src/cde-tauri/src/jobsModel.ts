import { skipSummary } from "./format";
import { hashProgressNote, hashResultNote, isLegacyCatalog } from "./hashJobs";
import type { CatalogInfo, JobDto, JobState, JobsSnapshot } from "./api/types";

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
  for (const j of model.jobs.values()) {
    if (!isActive(j.state)) continue;
    if (j.catalogId !== null) ids.add(j.catalogId);
    for (const id of j.catalogIds ?? []) ids.add(id);
  }
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

/** Why this catalog cannot be hashed now, or null when it can. */
export function hashDisabledReason(model: JobsModel, catalog: Pick<CatalogInfo, "catalogId" | "sourcePath">): string | null {
  if (isLegacyCatalog(catalog)) return "Legacy .cde catalogs cannot be hashed. Migrate it first";
  if (activeCatalogIds(model).has(catalog.catalogId)) return "A job for this catalog is already queued or running";
  return null;
}

export function hashableCatalogIds(model: JobsModel, catalogs: readonly Pick<CatalogInfo, "catalogId" | "sourcePath">[]): number[] {
  return catalogs.filter((c) => hashDisabledReason(model, c) === null).map((c) => c.catalogId);
}

/** Why the menu bar's Hash Catalogs has nothing to offer, or null when at least one catalog can be hashed. */
export function hashAllDisabledReason(model: JobsModel, catalogs: readonly Pick<CatalogInfo, "catalogId" | "sourcePath">[]): string | null {
  if (catalogs.length === 0) return "No catalogs are loaded";
  return hashableCatalogIds(model, catalogs).length === 0 ? "No loaded catalog can be hashed now (legacy, or a job holds it)" : null;
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
      if (j.kind === "hash") return [hashProgressNote(j.phase, j.hash), j.note].filter(Boolean).join(" · ");
      return [j.phase, j.entriesScanned > 0 ? `${n(j.entriesScanned)} entries` : "", j.note].filter(Boolean).join(" · ");
    case "succeeded": {
      const r = j.result;
      if (!r) return "";
      if (j.kind === "hash") return r.hash ? hashResultNote(r.hash) : "";
      const skipped = skipSummary(r.excludedFolderCount, r.accessDeniedFolderCount, r.unreadableFolderCount);
      return `${n(r.entries)} entries${skipped ? `; ${skipped}` : ""}`;
    }
    case "failed":
      return j.error?.message ?? "Failed";
    case "cancelled":
      return "Cancelled; nothing was changed";
  }
}
