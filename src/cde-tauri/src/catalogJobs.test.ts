import { describe, expect, it } from "vitest";
import {
  anyCreating,
  anyJobRunning,
  beginCreate,
  emptyCreateJobs,
  endCreate,
  jobKey,
  jobStatusLines,
  loadBlockedReason,
  parseJobKey,
  updateCreate,
} from "./catalogJobs";
import { beginRescan, emptyRescanState, updateRescan } from "./rescan";

const job = (id: number, rootPath = "D:\\Photos") => ({
  id,
  rootPath,
  fileName: "D-Photos",
  targetPath: "C:\\cat\\D-Photos.cdex",
});

describe("create job bookkeeping", () => {
  it("tracks several creates at once, each by its own id", () => {
    let s = emptyCreateJobs();
    s = beginCreate(s, job(1));
    s = beginCreate(s, job(2, "E:\\Music"));
    expect(anyCreating(s)).toBe(true);
    s = endCreate(s, 1);
    expect(anyCreating(s)).toBe(true);
    s = endCreate(s, 2);
    expect(anyCreating(s)).toBe(false);
  });

  it("keeps the real count through the writing/saving phases (which report 0)", () => {
    let s = beginCreate(emptyCreateJobs(), job(1));
    s = updateCreate(s, 1, { phase: "scanning", entriesScanned: 5000 });
    s = updateCreate(s, 1, { phase: "writing", entriesScanned: 0 });
    expect(s.get(1)).toMatchObject({ phase: "writing", entriesScanned: 5000 });
  });

  it("ignores updates and ends for unknown jobs, and never mutates the previous state", () => {
    const s0 = beginCreate(emptyCreateJobs(), job(1));
    expect(updateCreate(s0, 9, { phase: "scanning", entriesScanned: 1 })).toBe(s0);
    expect(endCreate(s0, 9)).toBe(s0);
    const s1 = endCreate(s0, 1);
    expect(s0.size).toBe(1);
    expect(s1.size).toBe(0);
  });

  it("does not replace a running job when begun again with the same id", () => {
    let s = beginCreate(emptyCreateJobs(), job(1));
    s = updateCreate(s, 1, { phase: "scanning", entriesScanned: 7000 });
    s = beginCreate(s, job(1));
    expect(s.get(1)?.entriesScanned).toBe(7000);
  });
});

describe("combined rescan + create jobs", () => {
  it("anyJobRunning is true for either kind", () => {
    const rescans = beginRescan(emptyRescanState(), 0, "C:\\");
    const creates = beginCreate(emptyCreateJobs(), job(1));
    expect(anyJobRunning(emptyRescanState(), emptyCreateJobs())).toBe(false);
    expect(anyJobRunning(rescans, emptyCreateJobs())).toBe(true);
    expect(anyJobRunning(emptyRescanState(), creates)).toBe(true);
  });

  it("disables reload / open folder with a reason naming what runs", () => {
    const rescans = beginRescan(emptyRescanState(), 0, "C:\\");
    const creates = beginCreate(emptyCreateJobs(), job(1));
    expect(loadBlockedReason(emptyRescanState(), emptyCreateJobs())).toBeNull();
    expect(loadBlockedReason(rescans, emptyCreateJobs())).toMatch(/rescan/i);
    expect(loadBlockedReason(emptyRescanState(), creates)).toMatch(/new catalog|creat/i);
    expect(loadBlockedReason(rescans, creates)).toMatch(/rescan.*creat|creat.*rescan/i);
  });

  it("lists status lines for both kinds with distinct, parseable keys", () => {
    let rescans = beginRescan(emptyRescanState(), 3, "C:\\");
    rescans = updateRescan(rescans, 3, { phase: "scanning", entriesScanned: 1200 });
    let creates = beginCreate(emptyCreateJobs(), job(7));
    creates = updateCreate(creates, 7, { phase: "scanning", entriesScanned: 2500 });

    const lines = jobStatusLines(rescans, creates);
    expect(lines.map((l) => l.key)).toEqual(["rescan:3", "create:7"]);
    expect(lines[0].text).toContain("Rescanning C:\\");
    expect(lines[1].text).toContain("Creating catalog D:\\Photos");
    expect(lines[1].text).toContain("2,500");
    expect(parseJobKey("rescan:3")).toEqual({ kind: "rescan", id: 3 });
    expect(parseJobKey("create:7")).toEqual({ kind: "create", id: 7 });
    expect(parseJobKey("nonsense")).toBeNull();
    expect(jobKey("create", 7)).toBe("create:7");
  });

  it("words the saving phase as finishing", () => {
    let creates = beginCreate(emptyCreateJobs(), job(7));
    creates = updateCreate(creates, 7, { phase: "saving", entriesScanned: 0 });
    expect(jobStatusLines(emptyRescanState(), creates)[0].text).toMatch(/Finishing/);
  });
});
