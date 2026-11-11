import { describe, expect, it } from "vitest";
import type { SearchResultRow } from "./api/types";
import { refToString } from "./api/types";
import { summarizeRows, summaryText } from "./selectionSummary";

const row = (n: number, size: number, isDirectory = false): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: n },
  name: `r${n}`,
  size,
  modified: "",
  isModifiedBad: false,
  isDirectory,
  isReparsePoint: false,
  fullPath: `/r${n}`,
  parentPath: "/",
  catalogName: "c",
});
const keys = (...rows: SearchResultRow[]) => new Set(rows.map((r) => refToString(r.ref)));

const rows = [row(0, 1024), row(1, 5000, true), row(2, 2048), row(3, 100, true), row(4, 1024)];

describe("summarizeRows", () => {
  it("counts a mixed selection, summing file bytes only", () => {
    const s = summarizeRows(rows, keys(rows[0], rows[1], rows[2]));
    expect(s).toEqual({ scope: "selection", count: 3, files: 2, folders: 1, fileBytes: 3072 });
  });

  it("summarises all results when nothing is selected", () => {
    const s = summarizeRows(rows, new Set());
    expect(s).toEqual({ scope: "results", count: 5, files: 3, folders: 2, fileBytes: 4096 });
  });

  it("matches the whole-result figures when everything is selected", () => {
    const all = summarizeRows(rows, keys(...rows));
    const none = summarizeRows(rows, new Set());
    expect({ ...all, scope: "x" }).toEqual({ ...none, scope: "x" });
    expect(all.scope).toBe("selection");
  });

  it("handles no results", () => {
    expect(summarizeRows([], new Set())).toEqual({ scope: "results", count: 0, files: 0, folders: 0, fileBytes: 0 });
  });

  it("sums 50,000 selected rows", () => {
    const many = Array.from({ length: 50_000 }, (_, i) => row(i, 10));
    const s = summarizeRows(many, keys(...many));
    expect(s.count).toBe(50_000);
    expect(s.fileBytes).toBe(500_000);
  });
});

describe("summarizeRows with a filter", () => {
  const shown = [rows[0], rows[1]];

  it("labels the shown rows when none of the selected rows is among them", () => {
    const s = summarizeRows(shown, keys(rows[3]), true);
    expect(s).toEqual({ scope: "shown", count: 2, files: 1, folders: 1, fileBytes: 1024 });
    expect(summaryText(s)).toBe("2 shown (1 folder), 1 KB in 1 file");
  });

  it("counts only the selected rows that are still shown", () => {
    const s = summarizeRows(shown, keys(rows[0], rows[3]), true);
    expect(s).toEqual({ scope: "selection", count: 1, files: 1, folders: 0, fileBytes: 1024 });
  });

  it("is the plain results summary when the filter is off", () => {
    expect(summarizeRows(rows, new Set(), false).scope).toBe("results");
  });
});

describe("summaryText", () => {
  it("states the selection, its folders and the file-only size", () => {
    expect(summaryText(summarizeRows(rows, keys(rows[0], rows[1], rows[2])))).toBe("3 selected (1 folder), 3 KB in 2 files");
  });

  it("labels the whole result set when nothing is selected", () => {
    expect(summaryText(summarizeRows(rows, new Set()))).toBe("All 5 (2 folders), 4 KB in 3 files");
  });

  it("omits the folder note when there are none", () => {
    expect(summaryText(summarizeRows([rows[0]], keys(rows[0])))).toBe("1 selected, 1 KB in 1 file");
  });
});
