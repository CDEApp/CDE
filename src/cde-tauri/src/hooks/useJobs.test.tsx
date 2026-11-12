// @vitest-environment jsdom
import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { JobRefusal, type CdeApiClient, type JobsHandlers } from "../api/client";
import type { JobDto } from "../api/types";
import { useJobs } from "./useJobs";

const { show, hide } = vi.hoisted(() => ({ show: vi.fn(), hide: vi.fn() }));
vi.mock("@mantine/notifications", () => ({ notifications: { show, hide } }));

function job(id: number, over: Partial<JobDto> = {}): JobDto {
  return {
    id,
    kind: "rescan",
    title: `Rescanning C:\\d${id}`,
    catalogId: id,
    catalogName: `C-d${id}`,
    state: "running",
    queuedAtUtc: "2026-01-01T00:00:00Z",
    startedAtUtc: "2026-01-01T00:00:01Z",
    endedAtUtc: null,
    phase: "scanning",
    entriesScanned: 0,
    note: "",
    result: null,
    error: null,
    ...over,
  };
}

const done = (id: number, over: Partial<JobDto> = {}) =>
  job(id, {
    state: "succeeded",
    endedAtUtc: "2026-01-01T00:01:00Z",
    result: { catalogId: id, rootPath: `C:\\d${id}`, entries: 42, savedPath: "x" },
    ...over,
  });

function setup() {
  let handlers!: JobsHandlers;
  const client = {
    subscribeJobs: vi.fn(async (h: JobsHandlers) => {
      handlers = h;
    }),
    submitJob: vi.fn(),
    cancelJob: vi.fn(async () => {}),
    dismissJob: vi.fn(async () => {}),
    clearFinishedJobs: vi.fn(async () => {}),
  };
  const afterRescan = vi.fn(async (_c: CdeApiClient, _id: number) => {});
  const refreshCatalogs = vi.fn(async (_c: CdeApiClient) => ({}));
  const openCatalog = vi.fn();
  const hook = renderHook(() =>
    useJobs({ getClient: () => client as unknown as CdeApiClient, sidecarReady: true, afterRescan, refreshCatalogs, openCatalog }),
  );
  const snapshot = (jobs: JobDto[]) => act(async () => handlers.onSnapshot({ jobs, maxConcurrent: 3 }));
  const event = (j: JobDto) => act(async () => handlers.onJob(j));
  const pushJob = (j: JobDto) => handlers.onJob(j);
  return { ...hook, client, afterRescan, refreshCatalogs, openCatalog, snapshot, event, pushJob };
}

beforeEach(() => {
  show.mockClear();
  hide.mockClear();
});

describe("useJobs completion handling", () => {
  it("runs the post-swap refresh and toast once per rescan, however often the job is seen finished", async () => {
    const t = setup();
    await t.snapshot([job(1)]);
    await t.event(done(1));
    await t.event(done(1));
    await t.snapshot([done(1)]);
    expect(t.afterRescan).toHaveBeenCalledTimes(1);
    expect(t.afterRescan).toHaveBeenCalledWith(t.client, 1);
    expect(show.mock.calls.filter(([a]) => a.title === "Rescan complete")).toHaveLength(1);
  });

  it("does not announce jobs that were already finished in the first snapshot", async () => {
    const t = setup();
    await t.snapshot([done(1), job(2)]);
    expect(t.afterRescan).not.toHaveBeenCalled();
    expect(show).not.toHaveBeenCalled();
    expect(t.result.current.model.jobs.size).toBe(2);
  });

  it("announces a job this window submitted that finished before the first snapshot arrived", async () => {
    const t = setup();
    t.client.submitJob.mockResolvedValue(job(3, { state: "queued" }));
    await act(() => t.result.current.rescanCatalog(3));
    await t.snapshot([done(3)]);
    expect(t.afterRescan).toHaveBeenCalledOnce();
    expect(t.afterRescan).toHaveBeenCalledWith(t.client, 3);
  });

  it("announces a submitted job the first snapshot already showed finished before the submit returned", async () => {
    const t = setup();
    await t.snapshot([done(3)]);
    t.client.submitJob.mockResolvedValue(job(3, { state: "queued" }));
    await act(() => t.result.current.rescanCatalog(3));
    expect(t.afterRescan).toHaveBeenCalledOnce();
    expect(t.result.current.model.jobs.get(3)?.state).toBe("succeeded");
  });

  it("announces a job that finished while the stream was down, from the reconnect snapshot", async () => {
    const t = setup();
    await t.snapshot([job(1)]);
    await t.snapshot([done(1)]);
    expect(t.afterRescan).toHaveBeenCalledOnce();
  });

  it("reports a failed view refresh separately from the rescan", async () => {
    const t = setup();
    t.afterRescan.mockRejectedValueOnce(new Error("boom"));
    await t.snapshot([]);
    await t.event(done(1));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Rescan complete, view not refreshed" }));
  });

  it("refreshes the catalog list and toasts Catalog created once for a create", async () => {
    const t = setup();
    await t.snapshot([]);
    const created = done(5, { kind: "create", catalogName: "D-data" });
    await t.event(created);
    await t.event(created);
    expect(t.refreshCatalogs).toHaveBeenCalledTimes(1);
    expect(t.afterRescan).not.toHaveBeenCalled();
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Catalog created", color: "green" }));
  });

  it("stays busy until a finished rescan's view refresh completes", async () => {
    const t = setup();
    let release!: () => void;
    t.afterRescan.mockReturnValueOnce(new Promise<void>((r) => (release = r)));
    await t.snapshot([job(1)]);
    expect(t.result.current.busy).toBe(true);
    await t.event(done(1));
    expect(t.result.current.busy).toBe(true);
    await act(async () => release());
    expect(t.result.current.busy).toBe(false);
  });

  it("stays busy until a finished create's catalog refresh completes", async () => {
    const t = setup();
    let release!: () => void;
    t.refreshCatalogs.mockReturnValueOnce(new Promise((r) => (release = () => r({}))));
    await t.snapshot([job(5, { kind: "create" })]);
    await t.event(done(5, { kind: "create" }));
    expect(t.result.current.busy).toBe(true);
    await act(async () => release());
    expect(t.result.current.busy).toBe(false);
  });

  it("toasts a failed job and leaves the views alone", async () => {
    const t = setup();
    await t.snapshot([]);
    await t.event(job(1, { state: "failed", error: { code: "rescan-failed", message: "disk full" } }));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Rescan failed", color: "red" }));
    expect(t.afterRescan).not.toHaveBeenCalled();
  });

  it("opens the unreachable-root modal for a root-not-accessible failure", async () => {
    const t = setup();
    await t.snapshot([]);
    await t.event(
      job(1, { state: "failed", error: { code: "root-not-accessible", message: "gone", rootPath: "/gone" } }),
    );
    expect(t.result.current.unreachableRoot).toBe("/gone");
    expect(show).not.toHaveBeenCalled();
    act(() => t.result.current.clearUnreachable());
    expect(t.result.current.unreachableRoot).toBeNull();
  });

  it("toasts a cancelled job", async () => {
    const t = setup();
    await t.snapshot([]);
    await t.event(job(1, { state: "cancelled" }));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Rescan cancelled" }));
  });
});

describe("useJobs actions", () => {
  it("queues a rescan without a toast and shows the job at once", async () => {
    const t = setup();
    t.client.submitJob.mockResolvedValue(job(3, { state: "queued" }));
    await act(() => t.result.current.rescanCatalog(3));
    expect(t.client.submitJob).toHaveBeenCalledWith({ kind: "rescan", catalogId: 3 });
    expect(t.result.current.model.jobs.get(3)?.state).toBe("queued");
    expect(show).not.toHaveBeenCalled();
  });

  it("keeps newer streamed progress when the submit response arrives after it", async () => {
    const t = setup();
    await t.snapshot([]);
    let release!: (j: JobDto) => void;
    t.client.submitJob.mockReturnValue(new Promise((r) => (release = r)));
    let pending!: Promise<void>;
    act(() => {
      pending = t.result.current.rescanCatalog(3);
    });
    await t.event(job(3, { entriesScanned: 500 }));
    await act(async () => {
      release(job(3, { state: "queued", startedAtUtc: null, phase: "queued" }));
      await pending;
    });
    expect(t.result.current.model.jobs.get(3)).toMatchObject({ state: "running", entriesScanned: 500 });
  });

  it("ignores a second rescan click for the same catalog while the first request is in flight", async () => {
    const t = setup();
    let release!: (j: JobDto) => void;
    t.client.submitJob.mockReturnValue(new Promise((r) => (release = r)));
    let first!: Promise<void>;
    act(() => {
      first = t.result.current.rescanCatalog(3);
      void t.result.current.rescanCatalog(3);
    });
    expect(t.client.submitJob).toHaveBeenCalledOnce();
    await act(async () => {
      release(job(3, { state: "queued" }));
      await first;
    });
  });

  it("sends a create request with the form fields", async () => {
    const t = setup();
    t.client.submitJob.mockResolvedValue(job(4, { kind: "create", catalogId: null, state: "queued" }));
    await act(() =>
      t.result.current.createCatalog({ rootPath: "D:\\x", fileName: "D-x", description: "d", followJunctions: true }),
    );
    expect(t.client.submitJob).toHaveBeenCalledWith({
      kind: "create",
      rootPath: "D:\\x",
      fileName: "D-x",
      description: "d",
      followJunctions: true,
    });
  });

  it("toasts a refusal", async () => {
    const t = setup();
    t.client.submitJob.mockRejectedValue(new JobRefusal({ code: "already-running", message: "Already queued." }));
    await act(() => t.result.current.rescanCatalog(3));
    expect(show).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Rescan not started", message: "Already queued.", color: "red" }),
    );
  });

  it("opens the unreachable-root modal when a create is refused for its root", async () => {
    const t = setup();
    t.client.submitJob.mockRejectedValue(new JobRefusal({ code: "root-not-accessible", message: "no" }));
    await act(() =>
      t.result.current.createCatalog({ rootPath: "Z:\\gone", fileName: "Z", description: "", followJunctions: false }),
    );
    expect(t.result.current.unreachableRoot).toBe("Z:\\gone");
    expect(show).not.toHaveBeenCalled();
  });

  it("cancel, dismiss and clear finished call the client; dismiss and clear drop the rows locally", async () => {
    const t = setup();
    await t.snapshot([job(1), done(2), job(3, { state: "failed", endedAtUtc: "2026-01-01T00:02:00Z" })]);
    await act(() => t.result.current.cancelJob(1));
    expect(t.client.cancelJob).toHaveBeenCalledWith(1);
    await act(() => t.result.current.dismissJob(2));
    expect(t.client.dismissJob).toHaveBeenCalledWith(2);
    expect([...t.result.current.model.jobs.keys()]).toEqual([1, 3]);
    await act(() => t.result.current.clearFinished());
    expect(t.client.clearFinishedJobs).toHaveBeenCalledOnce();
    expect([...t.result.current.model.jobs.keys()]).toEqual([1]);
  });

  it("clear finished keeps a job that finished while the request was in flight", async () => {
    const t = setup();
    await t.snapshot([job(1), done(2)]);
    let release!: () => void;
    t.client.clearFinishedJobs.mockReturnValueOnce(new Promise<void>((r) => (release = r)));
    let pending!: Promise<unknown>;
    act(() => {
      pending = t.result.current.clearFinished();
    });
    await t.event(done(1));
    await act(async () => {
      release();
      await pending;
    });
    expect([...t.result.current.model.jobs.keys()]).toEqual([1]);
  });

  it("keeps the row and toasts when a dismiss is refused", async () => {
    const t = setup();
    await t.snapshot([done(2)]);
    t.client.dismissJob.mockRejectedValueOnce(new Error("jobs: 409"));
    await act(() => t.result.current.dismissJob(2));
    expect(t.result.current.model.jobs.has(2)).toBe(true);
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Could not dismiss the job" }));
  });
});

describe("useJobs rescanMany", () => {
  const unreachable = (root: string) => new JobRefusal({ code: "root-not-accessible", message: "no", rootPath: root });

  it("rescans one catalog at a time and reports all unreachable roots in one notification, with no modal", async () => {
    const t = setup();
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => {
      if (req.catalogId === 2) throw unreachable("Z:\\gone");
      if (req.catalogId === 3) throw unreachable("\\\\nas\\media");
      return job(req.catalogId, { state: "queued" });
    });
    await t.snapshot([]);
    await act(() => t.result.current.rescanMany([1, 2, 3]));
    expect(t.client.submitJob).toHaveBeenCalledTimes(1);
    expect(show.mock.calls.filter(([a]) => /skipped/.test(a.title))).toHaveLength(0);
    await t.event(done(1));
    expect(t.client.submitJob).toHaveBeenCalledTimes(3);
    expect(t.result.current.unreachableRoot).toBeNull();
    const skipped = show.mock.calls.filter(([a]) => /skipped/.test(a.title));
    expect(skipped).toHaveLength(1);
    expect(skipped[0][0].title).toBe("2 catalogs skipped, root not reachable");
    expect(skipped[0][0].message).toContain("Z:\\gone");
    expect(skipped[0][0].message).toContain("\\\\nas\\media");
  });

  it("collects a root that turns unreachable while its job runs, after the other jobs have finished", async () => {
    const t = setup();
    await t.snapshot([]);
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => job(req.catalogId, { state: "queued" }));
    await act(() => t.result.current.rescanMany([1, 2]));
    expect(show).not.toHaveBeenCalled();
    expect(t.client.submitJob).toHaveBeenCalledTimes(1);

    await t.event(done(1));
    expect(t.client.submitJob).toHaveBeenCalledTimes(2);
    await t.event(job(2, { state: "failed", error: { code: "root-not-accessible", message: "gone", rootPath: "E:\\usb" } }));

    expect(t.result.current.unreachableRoot).toBeNull();
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "1 catalog skipped, root not reachable" }));
    expect(t.afterRescan).toHaveBeenCalledWith(t.client, 1);
  });

  it("stays quiet when every catalog rescans fine, and when one is already running", async () => {
    const t = setup();
    await t.snapshot([]);
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => {
      if (req.catalogId === 2) throw new JobRefusal({ code: "already-running", message: "Already queued." });
      return job(req.catalogId, { state: "queued" });
    });
    await act(() => t.result.current.rescanMany([1, 2]));
    await t.event(done(1));
    expect(show.mock.calls.filter(([a]) => /skipped|not started/.test(a.title))).toHaveLength(0);
  });

  it("moves on when a job finishes before its submit returns, and still gathers an unreachable root", async () => {
    const t = setup();
    await t.snapshot([]);
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => {
      const failed = req.catalogId === 1
        ? job(1, { state: "failed", error: { code: "root-not-accessible", message: "gone", rootPath: "E:\\usb" } })
        : done(req.catalogId);
      t.pushJob(failed);
      return job(req.catalogId, { state: "queued" });
    });
    await act(() => t.result.current.rescanMany([1, 2]));
    expect(t.client.submitJob).toHaveBeenCalledTimes(2);
    expect(t.result.current.unreachableRoot).toBeNull();
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "1 catalog skipped, root not reachable" }));
  });

  it("adds a second bulk rescan to the running one, so still one catalog at a time", async () => {
    const t = setup();
    await t.snapshot([]);
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => job(req.catalogId, { state: "queued" }));
    await act(() => t.result.current.rescanMany([1, 2]));
    await act(() => t.result.current.rescanMany([2, 3]));
    expect(t.client.submitJob).toHaveBeenCalledTimes(1);
    expect(t.result.current.busy).toBe(true);
    await t.event(done(1));
    await t.event(done(2));
    await t.event(done(3));
    expect(t.client.submitJob.mock.calls.map(([r]) => r.catalogId)).toEqual([1, 2, 3]);
  });

  it("does not queue again a catalog the open bulk rescan is running or has done", async () => {
    const t = setup();
    await t.snapshot([]);
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => job(req.catalogId, { state: "queued" }));
    await act(() => t.result.current.rescanMany([1, 2]));
    await act(() => t.result.current.rescanMany([1, 2, 3]));
    await t.event(done(1));
    await t.event(done(2));
    await t.event(done(3));
    expect(t.client.submitJob.mock.calls.map(([r]) => r.catalogId)).toEqual([1, 2, 3]);
  });

  it("stops the bulk rescan when the user cancels its running job", async () => {
    const t = setup();
    await t.snapshot([]);
    t.client.submitJob.mockImplementation(async (req: { catalogId: number }) => job(req.catalogId, { state: "queued" }));
    await act(() => t.result.current.rescanMany([1, 2, 3]));
    await t.event(job(1, { state: "cancelled" }));
    expect(t.client.submitJob).toHaveBeenCalledTimes(1);
    expect(t.result.current.busy).toBe(false);
  });

  it("reports other refusals once, together", async () => {
    const t = setup();
    t.client.submitJob.mockRejectedValue(new JobRefusal({ code: "busy", message: "Too many jobs." }));
    await act(() => t.result.current.rescanMany([1, 2]));
    const refused = show.mock.calls.filter(([a]) => /not started/.test(a.title));
    expect(refused).toHaveLength(1);
    expect(refused[0][0].title).toBe("2 rescans not started");
  });
});

describe("useJobs hash jobs", () => {
  const hashJob = (id: number, over: Partial<JobDto> = {}) =>
    job(id, {
      kind: "hash",
      title: "Hashing C-one",
      catalogId: null,
      catalogIds: [0, 2],
      phase: "hashing",
      ...over,
    });
  const hashDone = (id: number, filesFailed = 0) =>
    hashJob(id, {
      state: "succeeded",
      endedAtUtc: "2026-01-01T00:01:00Z",
      result: { catalogId: null, rootPath: null, entries: 0, savedPath: "x", hash: { filesHashed: 12, filesFailed } },
    });

  it("submits the catalogs and options as a hash job", async () => {
    const t = setup();
    t.client.submitJob.mockResolvedValue(hashJob(4, { state: "queued" }));
    await act(() => t.result.current.hashCatalogs([0, 2], { filter: "size:>2gb", includeUniqueSizes: true }));
    expect(t.client.submitJob).toHaveBeenCalledWith({
      kind: "hash",
      catalogIds: [0, 2],
      hash: { filter: "size:>2gb", includeUniqueSizes: true },
    });
  });

  it("announces a finished hash run once, refreshes the catalog list, and does not rescan-refresh", async () => {
    const t = setup();
    await t.snapshot([hashJob(4)]);
    await t.event(hashDone(4));
    await t.event(hashDone(4));
    expect(t.afterRescan).not.toHaveBeenCalled();
    expect(t.refreshCatalogs).toHaveBeenCalledTimes(1);
    const toasts = show.mock.calls.filter(([a]) => a.title === "Hashing complete");
    expect(toasts).toHaveLength(1);
    expect(toasts[0][0]).toMatchObject({ color: "green", message: expect.stringContaining("Hashed 12 files.") });
  });

  it("mentions the files that could not be read", async () => {
    const t = setup();
    await t.snapshot([hashJob(4)]);
    await t.event(hashDone(4, 2));
    expect(show).toHaveBeenCalledWith(
      expect.objectContaining({ color: "orange", message: expect.stringContaining("2 files could not be read") }),
    );
  });

  it("reports a failed and a cancelled hash run as hashing, not as a rescan", async () => {
    const t = setup();
    await t.snapshot([hashJob(4), hashJob(5)]);
    await t.event(hashJob(4, { state: "failed", error: { code: "hash-failed", message: "disk gone" } }));
    await t.event(hashJob(5, { state: "cancelled" }));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Hashing failed", message: expect.stringContaining("disk gone") }));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Hashing cancelled" }));
  });

  it("after a run that stopped part-way, refreshes the catalogs and says the finished hashes were saved", async () => {
    const t = setup();
    await t.snapshot([hashJob(4)]);
    t.refreshCatalogs.mockClear();
    await t.event(hashJob(4, { state: "failed", error: { code: "hash-failed", message: "Hash FAILED: 3 files hashed" } }));
    await waitFor(() => expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Hashing failed" })));
    expect(t.refreshCatalogs).toHaveBeenCalledTimes(1);
    expect(show).toHaveBeenCalledWith(
      expect.objectContaining({ message: expect.stringContaining("Hashes finished before the failure were saved.") }),
    );
  });

  it("after a run refused before it started, says nothing changed and does not refresh", async () => {
    const t = setup();
    await t.snapshot([hashJob(4)]);
    t.refreshCatalogs.mockClear();
    await t.event(hashJob(4, { state: "failed", error: { code: "catalog-unavailable", message: "busy" } }));
    await waitFor(() => expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Hashing failed" })));
    expect(t.refreshCatalogs).not.toHaveBeenCalled();
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ message: expect.stringContaining("Existing hashes were left unchanged.") }));
  });

  it("shows a refused hash run as a toast, even when a root is offline", async () => {
    const t = setup();
    t.client.submitJob.mockRejectedValue(
      new JobRefusal({ code: "root-not-accessible", message: "D:\\ cannot be reached", rootPath: "D:\\" }),
    );
    await act(() => t.result.current.hashCatalogs([0], null));
    expect(t.result.current.unreachableRoot).toBeNull();
    expect(show).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Hashing not started", message: expect.stringContaining("cannot be reached") }),
    );
  });
});
