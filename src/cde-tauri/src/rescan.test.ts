import { describe, expect, it } from "vitest";
import type { DirectoryNode, EntryRef, SearchResultRow } from "./api/types";
import {
  anyRescanning,
  beginRescan,
  emptyRescanState,
  endRescan,
  isRescanning,
  rescanMenuItem,
  reloadDisabledReason,
  resolveSurvivingFolder,
  rowStillResolves,
  staleResultsBanner,
  statusLines,
  updateRescan,
} from "./rescan";

describe("rescan bookkeeping", () => {
  it("tracks several catalogs at once, one per catalog", () => {
    let s = emptyRescanState();
    s = beginRescan(s, 0, "C:\\");
    s = beginRescan(s, 2, "D:\\");
    expect(isRescanning(s, 0)).toBe(true);
    expect(isRescanning(s, 1)).toBe(false);
    expect(isRescanning(s, 2)).toBe(true);
    s = endRescan(s, 0);
    expect(isRescanning(s, 0)).toBe(false);
    expect(anyRescanning(s)).toBe(true);
    s = endRescan(s, 2);
    expect(anyRescanning(s)).toBe(false);
  });

  it("does not replace a running entry when begun again", () => {
    let s = beginRescan(emptyRescanState(), 0, "C:\\");
    s = updateRescan(s, 0, { phase: "scanning", entriesScanned: 5000 });
    s = beginRescan(s, 0, "C:\\");
    expect(statusLines(s)[0].text).toContain("5,000");
  });

  it("never mutates the previous state", () => {
    const a = beginRescan(emptyRescanState(), 0, "C:\\");
    const b = updateRescan(a, 0, { phase: "scanning", entriesScanned: 10 });
    expect(statusLines(a)[0].text).not.toContain("10");
    expect(statusLines(b)[0].text).toContain("10");
    expect(isRescanning(endRescan(b, 0), 0)).toBe(false);
    expect(isRescanning(b, 0)).toBe(true);
  });

  it("ignores progress for a catalog that is not rescanning", () => {
    const s = updateRescan(emptyRescanState(), 3, { phase: "scanning", entriesScanned: 1 });
    expect(anyRescanning(s)).toBe(false);
  });

  it("disables Rescan only for the catalog that is rescanning", () => {
    const s = beginRescan(emptyRescanState(), 1, "D:\\");
    expect(rescanMenuItem(s, 1)).toMatchObject({ label: "Rescan", disabled: true });
    expect(rescanMenuItem(s, 1).title).toMatch(/already/i);
    expect(rescanMenuItem(s, 0)).toMatchObject({ label: "Rescan", disabled: false });
  });

  it("disables reload while any rescan runs, with a reason", () => {
    expect(reloadDisabledReason(emptyRescanState())).toBeNull();
    const s = beginRescan(emptyRescanState(), 1, "D:\\");
    expect(reloadDisabledReason(s)).toMatch(/rescan/i);
    expect(reloadDisabledReason(endRescan(s, 1))).toBeNull();
  });

  it("describes each running rescan for the status bar", () => {
    let s = beginRescan(emptyRescanState(), 0, "C:\\");
    s = updateRescan(s, 0, { phase: "scanning", entriesScanned: 12345 });
    s = beginRescan(s, 1, "D:\\");
    const lines = statusLines(s);
    expect(lines.map((l) => l.catalogId)).toEqual([0, 1]);
    expect(lines[0].text).toBe("Rescanning C:\\ — 12,345 entries");
    expect(lines[1].text).toBe("Rescanning D:\\…");
    s = updateRescan(s, 1, { phase: "swapping", entriesScanned: 0 });
    expect(statusLines(s)[1].text).toBe("Finishing rescan of D:\\…");
  });
});

describe("stale search results", () => {
  const row = (catalogId: number): SearchResultRow =>
    ({ ref: { catalogId, entryIndex: 5 }, fullPath: "x" }) as SearchResultRow;
  const name = (id: number) => (id === 0 ? "C:\\" : `cat${id}`);

  it("is null when nothing was rescanned since the search", () => {
    expect(staleResultsBanner([row(0)], new Set(), name)).toBeNull();
  });

  it("is null when the rescanned catalog has no rows in the results", () => {
    expect(staleResultsBanner([row(0)], new Set([1]), name)).toBeNull();
  });

  it("names the rescanned catalog the results point into", () => {
    expect(staleResultsBanner([row(0), row(1)], new Set([0]), name)).toBe(
      "Catalog C:\\ was rescanned — results may be out of date.",
    );
  });

  it("lists several rescanned catalogs", () => {
    expect(staleResultsBanner([row(0), row(1), row(2)], new Set([2, 0]), name)).toBe(
      "Catalogs C:\\, cat2 were rescanned — results may be out of date.",
    );
  });

  it("is null for an empty result list", () => {
    expect(staleResultsBanner([], new Set([0]), name)).toBeNull();
  });
});

describe("rowStillResolves", () => {
  const chain = (...paths: string[]) => paths.map((fullPath) => ({ fullPath }) as DirectoryNode);

  it("accepts a chain that ends at the row's path", () => {
    expect(rowStillResolves("C:\\a\\b.txt", chain("C:\\", "C:\\a", "C:\\a\\b.txt"))).toBe(true);
  });

  it("is case-insensitive", () => {
    expect(rowStillResolves("c:\\A\\b.txt", chain("C:\\", "C:\\a", "C:\\a\\B.txt"))).toBe(true);
  });

  it("rejects an index that now points at a different entry", () => {
    expect(rowStillResolves("C:\\a\\b.txt", chain("C:\\", "C:\\a", "C:\\a\\other.txt"))).toBe(false);
  });

  it("rejects an empty chain", () => {
    expect(rowStillResolves("C:\\a", [])).toBe(false);
  });
});

describe("resolveSurvivingFolder (nearest surviving parent)", () => {
  // Fake catalog: C:\ -> a -> b -> c, plus C:\ -> z
  const dir = (catalogId: number, entryIndex: number, name: string, fullPath: string): DirectoryNode =>
    ({ ref: { catalogId, entryIndex }, name, fullPath, isDirectory: true }) as DirectoryNode;
  const tree: Record<string, DirectoryNode[]> = {
    "0-0": [dir(0, 1, "a", "C:\\a"), dir(0, 2, "z", "C:\\z")],
    "0-1": [dir(0, 3, "b", "C:\\a\\b")],
    "0-3": [dir(0, 4, "c", "C:\\a\\b\\c")],
  };
  const list = async (ref: EntryRef) => tree[`${ref.catalogId}-${ref.entryIndex}`] ?? [];
  const root: EntryRef = { catalogId: 0, entryIndex: 0 };

  it("finds the folder itself when it still exists", async () => {
    const r = await resolveSurvivingFolder(root, "C:\\", "C:\\a\\b\\c", list);
    expect(r).toMatchObject({ ref: { catalogId: 0, entryIndex: 4 }, fullPath: "C:\\a\\b\\c", exact: true });
  });

  it("falls back to the nearest surviving parent when the folder is gone", async () => {
    const r = await resolveSurvivingFolder(root, "C:\\", "C:\\a\\b\\gone\\deeper", list);
    expect(r).toMatchObject({ ref: { catalogId: 0, entryIndex: 3 }, fullPath: "C:\\a\\b", exact: false });
  });

  it("falls back to the catalog root when the whole branch is gone", async () => {
    const r = await resolveSurvivingFolder(root, "C:\\", "C:\\gone\\x", list);
    expect(r).toMatchObject({ ref: root, fullPath: "C:\\", exact: false });
  });

  it("matches names case-insensitively and accepts either separator", async () => {
    const r = await resolveSurvivingFolder(root, "C:\\", "c:/A/B", list);
    expect(r).toMatchObject({ ref: { catalogId: 0, entryIndex: 3 }, exact: true });
  });

  it("returns the root for the root itself", async () => {
    const r = await resolveSurvivingFolder(root, "C:\\", "C:\\", list);
    expect(r).toMatchObject({ ref: root, exact: true });
  });

  it("works for roots without a trailing separator", async () => {
    const t: Record<string, DirectoryNode[]> = { "1-0": [dir(1, 1, "x", "/data/x")] };
    const r = await resolveSurvivingFolder({ catalogId: 1, entryIndex: 0 }, "/data", "/data/x", async (ref) => t[`${ref.catalogId}-${ref.entryIndex}`] ?? []);
    expect(r).toMatchObject({ ref: { catalogId: 1, entryIndex: 1 }, exact: true });
  });
});
