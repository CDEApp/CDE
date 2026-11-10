import { useCallback, useEffect, useRef, useState } from "react";
import { notifications } from "@mantine/notifications";
import { CdeApiClient, JobRefusal } from "../api/client";
import type { JobDto, SubmitJobRequest } from "../api/types";
import { skipSummary } from "../format";
import {
  applyJob,
  applySnapshot,
  emptyJobs,
  addIfUnknown,
  anyActive,
  isActive,
  isFromEarlierSession,
  removeJobs,
  removeJob,
  type JobsModel,
} from "../jobsModel";

export interface UseJobsOptions {
  getClient: () => CdeApiClient;
  sidecarReady: boolean;
  afterRescan: (client: CdeApiClient, catalogId: number) => Promise<void>;
  refreshCatalogs: (client: CdeApiClient) => Promise<unknown>;
  openCatalog: (catalogId: number, rootPath: string) => void;
  retryDelayMs?: number;
}

type Callbacks = Pick<UseJobsOptions, "afterRescan" | "refreshCatalogs" | "openCatalog">;

/**
 * One bulk rescan (Rescan all, Rescan stale, rescan on startup). Its catalogs rescan one at a time, so several
 * catalogs on one disk do not compete for it. Refusals and failures that would each open the unreachable-root
 * modal or a toast are gathered here and reported once, after the last job has settled.
 */
interface RescanBatch {
  /** Catalogs not submitted yet. */
  queue: number[];
  /** Every catalog this batch has taken on; a request that joins it adds only the others. */
  included: Set<number>;
  jobIds: Set<number>;
  submitting: boolean;
  unreachable: Set<string>;
  refused: string[];
}

function reportBatch(b: RescanBatch) {
  if (b.unreachable.size > 0) {
    const n = b.unreachable.size;
    notifications.show({
      color: "orange",
      title: `${n} ${n === 1 ? "catalog" : "catalogs"} skipped, root not reachable`,
      message: `${[...b.unreachable].join(", ")}. Check the drive or share is connected, then rescan.`,
      autoClose: 20000,
    });
  }
  if (b.refused.length > 0) {
    notifications.show({
      color: "red",
      title: `${b.refused.length} ${b.refused.length === 1 ? "rescan" : "rescans"} not started`,
      message: b.refused.join("; "),
    });
  }
}

const errText = (e: unknown) => (e instanceof Error ? e.message : String(e));

function skipped(job: JobDto): string {
  const r = job.result;
  const s = r && skipSummary(r.excludedFolderCount, r.accessDeniedFolderCount, r.unreadableFolderCount);
  return s ? `; ${s}` : "";
}

async function announceRescan(client: CdeApiClient, job: JobDto, cb: Callbacks) {
  const r = job.result!;
  try {
    await cb.afterRescan(client, r.catalogId);
    notifications.show({
      color: "green",
      title: "Rescan complete",
      message: `Rescanned ${r.rootPath} — ${r.entries.toLocaleString()} entries${skipped(job)}`,
    });
  } catch (e) {
    notifications.show({
      color: "orange",
      title: "Rescan complete, view not refreshed",
      message: `Rescanned ${r.rootPath}, but refreshing the view failed: ${errText(e)} — use Reload.`,
    });
  }
}

async function announceCreate(client: CdeApiClient, job: JobDto, cb: Callbacks) {
  const r = job.result!;
  let refreshed = true;
  try {
    await cb.refreshCatalogs(client);
  } catch {
    refreshed = false;
  }
  const toastId = `new-catalog-${r.catalogId}-${job.id}`;
  notifications.show({
    id: toastId,
    color: refreshed ? "green" : "orange",
    title: "Catalog created",
    autoClose: 15000,
    message: (
      <span>
        {refreshed
          ? `Created ${job.catalogName}.cdex — ${r.entries.toLocaleString()} entries${skipped(job)}. `
          : `Created ${job.catalogName}.cdex, but refreshing the view failed — use Reload. `}
        {refreshed && (
          <button
            className="toast-action"
            onClick={() => {
              notifications.hide(toastId);
              cb.openCatalog(r.catalogId, r.rootPath);
            }}
          >
            Open
          </button>
        )}
      </span>
    ),
  });
}

function announceFailure(job: JobDto, showUnreachable: (root: string) => void) {
  const err = job.error;
  if (err?.code === "root-not-accessible") {
    showUnreachable(err.rootPath || job.title);
    return;
  }
  const detail = err?.message ?? "unknown error";
  if (job.kind === "rescan") {
    notifications.show({
      color: "red",
      title: "Rescan failed",
      message: `${job.title}: ${detail}. The catalog was left unchanged.`,
    });
  } else {
    notifications.show({
      color: "red",
      title: "New catalog failed",
      message: `${job.title}: ${detail}${err?.code === "file-exists" ? "" : " Nothing was written."}`,
    });
  }
}

function announceCancelled(job: JobDto) {
  notifications.show({
    color: "gray",
    title: job.kind === "rescan" ? "Rescan cancelled" : "New catalog cancelled",
    message: job.kind === "rescan" ? `${job.title} was left unchanged.` : `${job.title}: nothing was written.`,
  });
}

export function useJobs({
  getClient,
  sidecarReady,
  afterRescan,
  refreshCatalogs,
  openCatalog,
  retryDelayMs,
}: UseJobsOptions) {
  const [model, setModel] = useState<JobsModel>(emptyJobs);
  const [unreachableRoot, setUnreachableRoot] = useState<string | null>(null);
  const [completing, setCompleting] = useState(0);
  const modelRef = useRef(model);
  const handled = useRef(new Set<number>());
  const submitted = useRef(new Set<number>());
  const finishedAtStart = useRef(new Set<number>());
  const gotSnapshot = useRef(false);
  const inFlightSubmits = useRef(new Set<string>());
  // At most one bulk rescan; a second request joins its queue, so catalogs still rescan one at a time.
  const batchRef = useRef<RescanBatch | null>(null);
  const [batchOpen, setBatchOpen] = useState(false);
  // Jobs that finished while the batch was submitting and before submit() knew their id: the server starts a job
  // inside POST /jobs, so its finished event can arrive first. submit() claims them; anything left is settled after.
  const settledEarly = useRef(new Map<number, JobDto>());
  const latestCallbacks = useRef<Callbacks>({ afterRescan, refreshCatalogs, openCatalog });
  latestCallbacks.current = { afterRescan, refreshCatalogs, openCatalog };

  const update = useCallback((next: JobsModel) => {
    modelRef.current = next;
    setModel(next);
  }, []);

  // settle() advances a batch when its job finishes, and advancing submits through submit(), which calls settle().
  const advanceBatch = useRef<(b: RescanBatch) => Promise<void>>(async () => {});

  const settle = useCallback(
    (job: JobDto) => {
      if (isActive(job.state) || handled.current.has(job.id) || finishedAtStart.current.has(job.id)) return;
      const batch = batchRef.current;
      if (batch?.submitting && !submitted.current.has(job.id)) {
        settledEarly.current.set(job.id, job);
        return;
      }
      handled.current.add(job.id);
      if (batch?.jobIds.has(job.id)) {
        batch.jobIds.delete(job.id);
        if (job.state === "failed" && job.error?.code === "root-not-accessible") {
          batch.unreachable.add(job.error.rootPath || job.title);
        } else if (job.state === "failed") {
          announceFailure(job, setUnreachableRoot);
        } else if (job.state === "cancelled") {
          batch.queue.length = 0; // cancelling the running catalog stops the bulk rescan
        }
        void advanceBatch.current(batch);
        if (job.state === "failed") return;
      }
      if (job.state === "succeeded" && job.result) {
        const announce = job.kind === "rescan" ? announceRescan : announceCreate;
        setCompleting((n) => n + 1);
        void announce(getClient(), job, latestCallbacks.current)
          .catch((e) => console.error("job completion handling failed", e))
          .finally(() => setCompleting((n) => n - 1));
      } else if (job.state === "failed") {
        announceFailure(job, setUnreachableRoot);
      } else if (job.state === "cancelled") {
        announceCancelled(job);
      }
    },
    [getClient],
  );

  useEffect(() => {
    if (!sidecarReady) return;
    const abort = new AbortController();
    void getClient().subscribeJobs(
      {
        signal: abort.signal,
        onSnapshot: (snap) => {
          update(applySnapshot(snap));
          const first = !gotSnapshot.current;
          gotSnapshot.current = true;
          for (const j of snap.jobs) {
            if (first && isFromEarlierSession(j, submitted.current)) finishedAtStart.current.add(j.id);
            else settle(j);
          }
        },
        onJob: (job) => {
          update(applyJob(modelRef.current, job));
          settle(job);
        },
        onDisconnect: (e) => console.warn("job stream disconnected; reconnecting", e),
      },
      retryDelayMs,
    );
    return () => abort.abort();
  }, [sidecarReady, getClient, update, settle, retryDelayMs]);

  const submit = useCallback(
    async (req: SubmitJobRequest, inFlightKey: string | null, refusedTitle: string, batch?: RescanBatch) => {
      if (inFlightKey) {
        if (inFlightSubmits.current.has(inFlightKey)) return;
        inFlightSubmits.current.add(inFlightKey);
      }
      try {
        const accepted = await getClient().submitJob(req);
        submitted.current.add(accepted.id);
        batch?.jobIds.add(accepted.id);
        settledEarly.current.delete(accepted.id); // the model already holds its final state; settle it below
        finishedAtStart.current.delete(accepted.id);
        const next = addIfUnknown(modelRef.current, accepted);
        update(next);
        settle(next.jobs.get(accepted.id)!);
      } catch (e) {
        if (batch) {
          // Already queued or running is the guard doing its job, not something to report.
          if (!(e instanceof JobRefusal && e.error.code === "already-running")) {
            if (e instanceof JobRefusal && e.rootNotAccessible) batch.unreachable.add(e.error.rootPath || errText(e));
            else batch.refused.push(errText(e));
          }
        } else if (e instanceof JobRefusal && e.rootNotAccessible) {
          setUnreachableRoot(e.error.rootPath || (req.kind === "create" ? req.rootPath : null) || e.message);
        } else {
          notifications.show({ color: "red", title: refusedTitle, message: errText(e) });
        }
      } finally {
        if (inFlightKey) inFlightSubmits.current.delete(inFlightKey);
      }
    },
    [getClient, update, settle],
  );

  const rescanCatalog = useCallback(
    (catalogId: number) => submit({ kind: "rescan", catalogId }, `rescan:${catalogId}`, "Rescan not started"),
    [submit],
  );

  // Submit the batch's next catalog once none of its jobs is active; report the batch when nothing is left.
  advanceBatch.current = async (b: RescanBatch) => {
    if (b.submitting || b.jobIds.size > 0) return;
    b.submitting = true;
    while (b.queue.length > 0 && b.jobIds.size === 0) {
      const id = b.queue.shift()!;
      await submit({ kind: "rescan", catalogId: id }, `rescan:${id}`, "Rescan not started", b);
    }
    b.submitting = false;
    const early = [...settledEarly.current.values()];
    settledEarly.current.clear();
    for (const job of early) settle(job);
    if (b.jobIds.size === 0 && b.queue.length === 0 && batchRef.current === b) {
      batchRef.current = null;
      setBatchOpen(false);
      reportBatch(b);
    }
  };

  const rescanMany = useCallback(async (catalogIds: readonly number[]) => {
    let batch = batchRef.current;
    if (!batch) {
      batch = { queue: [], included: new Set(), jobIds: new Set(), submitting: false, unreachable: new Set(), refused: [] };
      batchRef.current = batch;
      setBatchOpen(true);
    }
    for (const id of catalogIds) {
      if (batch.included.has(id)) continue;
      batch.included.add(id);
      batch.queue.push(id);
    }
    await advanceBatch.current(batch);
  }, []);

  const createCatalog = useCallback(
    (req: { rootPath: string; fileName: string; description: string; followJunctions: boolean }) =>
      submit({ kind: "create", ...req }, null, "New catalog not started"),
    [submit],
  );

  const guarded = useCallback(
    (action: string, run: (client: CdeApiClient) => Promise<void>) =>
      run(getClient()).catch((e) =>
        notifications.show({ color: "red", title: `Could not ${action}`, message: errText(e) }),
      ),
    [getClient],
  );

  const cancelJob = useCallback((id: number) => guarded("cancel the job", (c) => c.cancelJob(id)), [guarded]);

  const dismissJob = useCallback(
    (id: number) =>
      guarded("dismiss the job", async (c) => {
        await c.dismissJob(id);
        update(removeJob(modelRef.current, id));
      }),
    [guarded, update],
  );

  const clearFinished = useCallback(
    () =>
      guarded("clear finished jobs", async (c) => {
        const finished = [...modelRef.current.jobs.values()].filter((j) => !isActive(j.state)).map((j) => j.id);
        await c.clearFinishedJobs();
        update(removeJobs(modelRef.current, finished));
      }),
    [guarded, update],
  );

  const clearUnreachable = useCallback(() => setUnreachableRoot(null), []);
  // A finished job's refresh still writes the catalog list and selection, and a bulk rescan's queued catalog ids are
  // list positions, so a reload must wait for both.
  // A finished job's refresh still writes the catalog list and selection, so a reload must wait for it too.
  const busy = anyActive(model) || completing > 0 || batchOpen;

  return { model, busy, unreachableRoot, clearUnreachable, rescanCatalog, rescanMany, createCatalog, cancelJob, dismissJob, clearFinished };
}
