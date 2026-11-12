import { describe, expect, it } from "vitest";
import type { SearchResultRow } from "./api/types";
import {
  entryKind,
  fileExtension,
  RESULT_COLUMN_BY_ID,
  RESULT_COLUMN_IDS,
  resolveVisibleColumns,
  visibleColumnIds,
  wantsDuplicateCounts,
  withDuplicateCounts,
} from "./resultColumns";

describe("fileExtension", () => {
  it.each([
    ["report.pdf", "pdf"],
    ["archive.tar.gz", "gz"],
    ["Photo.JPG", "JPG"],
    [".gitignore", ""],
    ["Makefile", ""],
    ["trailing.", ""],
  ])("%s -> %j", (name, ext) => {
    expect(fileExtension({ name, isDirectory: false })).toBe(ext);
  });

  it("is empty for a folder, dots or not", () => {
    expect(fileExtension({ name: "v1.2", isDirectory: true })).toBe("");
  });
});

describe("entryKind", () => {
  it("is link for a reparse point, even a folder one, then folder, then file", () => {
    expect(entryKind({ isDirectory: true, isReparsePoint: true })).toBe("link");
    expect(entryKind({ isDirectory: true, isReparsePoint: false })).toBe("folder");
    expect(entryKind({ isDirectory: false, isReparsePoint: false })).toBe("file");
  });
});

describe("visible columns", () => {
  const DEFAULT = ["name", "size", "modified", "catalog", "path"];

  it("defaults to the original five columns, with the new ones hidden", () => {
    expect([...resolveVisibleColumns(undefined)]).toEqual(DEFAULT);
    expect(RESULT_COLUMN_IDS).toEqual([...DEFAULT, "extension", "type", "dupes", "hashed"]);
  });

  it("applies an old saved order unchanged to the default columns", () => {
    expect(visibleColumnIds(["path", "name", "size", "modified", "catalog"], undefined)).toEqual([
      "path", "name", "size", "modified", "catalog",
    ]);
  });

  it("ignores unknown ids and falls back to the defaults when none is left", () => {
    expect([...resolveVisibleColumns(["size", "gone", 4])]).toEqual(["size"]);
    expect([...resolveVisibleColumns(["gone"])]).toEqual(DEFAULT);
    expect([...resolveVisibleColumns([])]).toEqual(DEFAULT);
    expect([...resolveVisibleColumns("name")]).toEqual(DEFAULT);
  });

  it("orders the shown columns by the saved order", () => {
    expect(visibleColumnIds(["type", "name"], ["name", "type", "path"])).toEqual(["type", "name", "path"]);
  });
});

describe("Dupes and Hashed columns", () => {
  const file = (over: Partial<SearchResultRow> = {}) =>
    ({ name: "a.bin", isDirectory: false, ...over }) as SearchResultRow;
  const dupes = RESULT_COLUMN_BY_ID.get("dupes")!;
  const hashed = RESULT_COLUMN_BY_ID.get("hashed")!;
  const HASH = "0123456789abcdef0123456789abcdef";

  it("are off by default", () => {
    expect(dupes.defaultVisible).toBe(false);
    expect(hashed.defaultVisible).toBe(false);
    expect(wantsDuplicateCounts(undefined)).toBe(false);
  });

  it("show the count for a hashed file with copies", () => {
    const r = file({ contentHash: HASH, duplicateCount: 3 });
    expect(dupes.cell(r)).toBe("3");
    expect(dupes.csv(r)).toBe("3");
    expect(hashed.cell(r)).toBe("yes");
    expect(hashed.csv(r)).toBe("true");
  });

  it("show 0 for a hashed unique file", () => {
    const r = file({ contentHash: HASH, duplicateCount: 0 });
    expect(dupes.cell(r)).toBe("0");
    expect(dupes.cellTitle!(r)).toBe("Unique");
  });

  it("never show an unhashed file as 0", () => {
    const r = file();
    expect(dupes.cell(r)).toBe("not hashed");
    expect(dupes.csv(r)).toBe("");
    expect(hashed.cell(r)).toBe("no");
    expect(hashed.csv(r)).toBe("false");
  });

  it("show a hashed file whose copies were not counted as not counted", () => {
    const r = file({ contentHash: HASH });
    expect(dupes.cell(r)).toBe("—");
    expect(dupes.cellTitle!(r)).toMatch(/Search again/);
    expect(dupes.csv(r)).toBe("");
  });

  it("leave a folder blank", () => {
    const r = file({ isDirectory: true });
    expect(dupes.cell(r)).toBe("");
    expect(hashed.cell(r)).toBe("");
  });

  it("sort unhashed rows as NaN so they group after the hashed ones", () => {
    expect(dupes.sortValue(file())).toBeNaN();
    expect(dupes.sortValue(file({ contentHash: HASH, duplicateCount: 0 }))).toBe(0);
    expect(hashed.sortValue(file())).toBeNaN();
    expect(hashed.sortValue(file({ contentHash: HASH }))).toBe(1);
  });

  it("ask the engine for counts only while Dupes is visible", () => {
    expect(wantsDuplicateCounts(["name", "dupes"])).toBe(true);
    expect(wantsDuplicateCounts(["name", "hashed"])).toBe(false);
    expect(withDuplicateCounts({ pattern: "x" }, true).includeDuplicateCounts).toBe(true);
    expect(withDuplicateCounts({ pattern: "x", includeDuplicateCounts: true }, false).includeDuplicateCounts).toBe(false);
  });
});
