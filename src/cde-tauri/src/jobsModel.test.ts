import { describe, expect, it } from "vitest";
import type { JobDto, JobState } from "./api/types";
import {
  activeCatalogIds,
  activeCount,
  anyActive,
  applyJob,
  applySnapshot,
  emptyJobs,
  isFromEarlierSession,
  loadBlockedReason,
  progressNote,
  removeFinished,
  removeJob,
  rescanMenuItem,
  sortedJobs,
  startedLabel,
  statusLabel,
  statusLines,
} from "./jobsModel";

function job(id: number, over: Partial<JobDto> = {}): JobDto {
  return {
    id,
    kind: "rescan",
    title: `Rescanning C:\\d${id}`,
    catalogId: id,
    catalogName: `C-d${id}`,
    state: "queued",
    queuedAtUtc: "2026-01-01T00:00:00Z",
    startedAtUtc: null,
    endedAtUtc: null,
    phase: "queued",
    entriesScanned: 0,
    note: "",
    result: null,
    error: null,
    ...over,
  };
}

describe("reducer", () => {
  it("a snapshot replaces everything, including max concurrency", () => {
    const a = applyJob(emptyJobs, job(1));
    const b = applySnapshot({ jobs: [job(2)], maxConcurrent: 3 });
    expect([...b.jobs.keys()]).toEqual([2]);
    expect(b.maxConcurrent).toBe(3);
    expect(a.jobs.has(1)).toBe(true);
  });

  it("a job event upserts and never mutates the previous model", () => {
    const a = applyJob(emptyJobs, job(1));
    const b = applyJob(a, job(1, { state: "running", entriesScanned: 5 }));
    expect(a.jobs.get(1)!.state).toBe("queued");
    expect(b.jobs.get(1)!.entriesScanned).toBe(5);
  });

  it("a terminal state is final: a late active event is ignored", () => {
    const done = applyJob(emptyJobs, job(1, { state: "succeeded" }));
    expect(applyJob(done, job(1, { state: "running" }))).toBe(done);
  });

  it("a finished job this window did not submit is from an earlier session", () => {
    const submitted = new Set([2]);
    expect(isFromEarlierSession(job(1, { state: "succeeded" }), submitted)).toBe(true);
    expect(isFromEarlierSession(job(2, { state: "succeeded" }), submitted)).toBe(false);
    expect(isFromEarlierSession(job(3, { state: "running" }), submitted)).toBe(false);
  });

  it("removes one job, or every finished job", () => {
    let m = applySnapshot({
      jobs: [job(1, { state: "running" }), job(2, { state: "failed" }), job(3, { state: "cancelled" })],
      maxConcurrent: 2,
    });
    expect([...removeJob(m, 2).jobs.keys()]).toEqual([1, 3]);
    expect(removeJob(m, 99)).toBe(m);
    m = removeFinished(m);
    expect([...m.jobs.keys()]).toEqual([1]);
    expect(removeFinished(m)).toBe(m);
  });
});

describe("views", () => {
  const model = applySnapshot({
    jobs: [
      job(1, { state: "succeeded", endedAtUtc: "2026-01-01T01:00:00Z" }),
      job(2, { state: "queued" }),
      job(3, { state: "failed", endedAtUtc: "2026-01-01T03:00:00Z" }),
      job(4, { state: "running", startedAtUtc: "2026-01-01T02:00:00Z" }),
      job(5, { state: "queued" }),
      job(6, { state: "cancelled", endedAtUtc: "2026-01-01T02:30:00Z" }),
    ],
    maxConcurrent: 1,
  });

  it("sorts running, then queued in submit order, then finished newest first", () => {
    expect(sortedJobs(model).map((j) => j.id)).toEqual([4, 2, 5, 3, 6, 1]);
  });

  it("labels each state", () => {
    const states: JobState[] = ["queued", "running", "succeeded", "failed", "cancelled"];
    expect(states.map(statusLabel)).toEqual(["Queued", "Running", "Succeeded", "Failed", "Cancelled"]);
  });

  it("counts active jobs", () => {
    expect(anyActive(model)).toBe(true);
    expect(activeCount(model)).toBe(3);
    expect(anyActive(emptyJobs)).toBe(false);
    expect(anyActive(removeJob(removeJob(removeJob(model, 2), 4), 5))).toBe(false);
  });

  it("blocks load while anything is queued or running, with a reason", () => {
    expect(loadBlockedReason(emptyJobs)).toBeNull();
    expect(loadBlockedReason(applyJob(emptyJobs, job(1, { state: "queued" })))).toMatch(/queued or running/);
    expect(loadBlockedReason(applyJob(emptyJobs, job(1, { state: "succeeded" })))).toBeNull();
  });

  it("disables Rescan only for a catalog with an active job", () => {
    expect([...activeCatalogIds(model)].sort()).toEqual([2, 4, 5]);
    expect(rescanMenuItem(model, 4)).toMatchObject({ label: "Rescan", disabled: true });
    expect(rescanMenuItem(model, 1)).toMatchObject({ label: "Rescan", disabled: false });
    const create = applyJob(emptyJobs, job(7, { kind: "create", catalogId: null, state: "running" }));
    expect(activeCatalogIds(create).size).toBe(0);
  });

  it("shows queued until a job starts", () => {
    expect(startedLabel(job(1))).toBe("queued");
    expect(startedLabel(job(1, { startedAtUtc: "2026-01-01T02:00:00Z" }))).not.toBe("queued");
  });

  it("describes progress per state", () => {
    expect(progressNote(job(1))).toMatch(/waiting/i);
    expect(progressNote(job(1, { state: "running", phase: "scanning", entriesScanned: 12345, note: "C:\\a" }))).toBe(
      "scanning · 12,345 entries · C:\\a",
    );
    expect(progressNote(job(1, { state: "running", phase: "writing" }))).toBe("writing");
    expect(
      progressNote(
        job(1, {
          state: "succeeded",
          result: { catalogId: 1, rootPath: "C:\\", entries: 1500, savedPath: "x", accessDeniedFolderCount: 2 },
        }),
      ),
    ).toBe("1,500 entries; skipped 2 folders: 2 access denied");
    expect(progressNote(job(1, { state: "failed", error: { code: "rescan-failed", message: "disk full" } }))).toBe(
      "disk full",
    );
  });

  it("gives one status line per active job, running first", () => {
    const lines = statusLines(model);
    expect(lines.map((l) => l.id)).toEqual([4, 2, 5]);
    expect(lines[1].text).toBe("Queued: Rescanning C:\\d2");
    const m = applyJob(emptyJobs, job(1, { state: "running", phase: "scanning", entriesScanned: 7 }));
    expect(statusLines(m)[0].text).toBe("Rescanning C:\\d1 — 7 entries");
    expect(statusLines(applyJob(m, job(1, { state: "running", phase: "swapping", entriesScanned: 7 })))[0].text).toBe(
      "Finishing: Rescanning C:\\d1…",
    );
  });
});
