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
  isActive,
  isFromEarlierSession,
  removeFinished,
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
  const modelRef = useRef(model);
  const handled = useRef(new Set<number>());
  const submitted = useRef(new Set<number>());
  const finishedAtStart = useRef(new Set<number>());
  const gotSnapshot = useRef(false);
  const inFlightSubmits = useRef(new Set<string>());
  const latestCallbacks = useRef<Callbacks>({ afterRescan, refreshCatalogs, openCatalog });
  latestCallbacks.current = { afterRescan, refreshCatalogs, openCatalog };

  const update = useCallback((next: JobsModel) => {
    modelRef.current = next;
    setModel(next);
  }, []);

  const settle = useCallback(
    (job: JobDto) => {
      if (isActive(job.state) || handled.current.has(job.id) || finishedAtStart.current.has(job.id)) return;
      handled.current.add(job.id);
      if (job.state === "succeeded" && job.result) {
        const announce = job.kind === "rescan" ? announceRescan : announceCreate;
        void announce(getClient(), job, latestCallbacks.current).catch((e) => console.error("job completion handling failed", e));
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
    async (req: SubmitJobRequest, inFlightKey: string | null, refusedTitle: string) => {
      if (inFlightKey) {
        if (inFlightSubmits.current.has(inFlightKey)) return;
        inFlightSubmits.current.add(inFlightKey);
      }
      try {
        const accepted = await getClient().submitJob(req);
        submitted.current.add(accepted.id);
        finishedAtStart.current.delete(accepted.id);
        const next = addIfUnknown(modelRef.current, accepted);
        update(next);
        settle(next.jobs.get(accepted.id)!);
      } catch (e) {
        if (e instanceof JobRefusal && e.rootNotAccessible) {
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
        await c.clearFinishedJobs();
        update(removeFinished(modelRef.current));
      }),
    [guarded, update],
  );

  const clearUnreachable = useCallback(() => setUnreachableRoot(null), []);

  return { model, unreachableRoot, clearUnreachable, rescanCatalog, createCatalog, cancelJob, dismissJob, clearFinished };
}
