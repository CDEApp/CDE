import { describe, expect, it } from "vitest";
import type { CatalogInfo } from "./api/types";
import { type CatalogSort, type CatalogSortColumn, nextCatalogSort, sortCatalogs } from "./catalogSort";

function cat(id: number, o: Partial<CatalogInfo> = {}): CatalogInfo {
  return {
    catalogId: id,
    rootPath: `R${id}`,
    volumeName: "",
    dirEntryCount: 0,
    fileEntryCount: 0,
    rootSize: 0,
    driveLetterHint: "",
    sourcePath: null,
    actualFileName: "",
    ...o,
  } as CatalogInfo;
}

const ids = (cs: CatalogInfo[]) => cs.map((c) => c.catalogId);

describe("sortCatalogs", () => {
  const list = [
    cat(1, { rootPath: "C:\\b", volumeName: "vb", dirEntryCount: 10, fileEntryCount: 100, rootSize: 9, driveLetterHint: "D", sourcePath: "E:\\x\\zeta.cdex" }),
    cat(2, { rootPath: "C:\\a", volumeName: "Vc", dirEntryCount: 9, fileEntryCount: 20, rootSize: 10, driveLetterHint: "C", sourcePath: "E:\\x\\Alpha.cdex" }),
    cat(3, { rootPath: "C:\\C", volumeName: "va", dirEntryCount: 100, fileEntryCount: 3, rootSize: 1, driveLetterHint: "e", sourcePath: "E:\\x\\mid.cdex" }),
  ];

  const cases: [CatalogSortColumn, number[]][] = [
    ["root", [2, 1, 3]],
    ["volume", [3, 1, 2]],
    ["dirs", [2, 1, 3]],
    ["files", [3, 2, 1]],
    ["size", [3, 1, 2]],
    ["drive", [2, 1, 3]],
    ["catalog", [2, 3, 1]],
  ];

  it.each(cases)("%s ascending then descending", (column, asc) => {
    expect(ids(sortCatalogs(list, { column, direction: "asc" }))).toEqual(asc);
    expect(ids(sortCatalogs(list, { column, direction: "desc" }))).toEqual([...asc].reverse());
  });

  it("sorts by description, ignoring case and treating a missing one as empty", () => {
    const l = [cat(1, { description: "beta" }), cat(2, { description: "Alpha" }), cat(3, { description: undefined as unknown as string })];
    expect(ids(sortCatalogs(l, { column: "description", direction: "asc" }))).toEqual([3, 2, 1]);
  });

  it("falls back to actualFileName for the catalog column", () => {
    const l = [cat(1, { actualFileName: "b.cdex" }), cat(2, { actualFileName: "a.cdex" })];
    expect(ids(sortCatalogs(l, { column: "catalog", direction: "asc" }))).toEqual([2, 1]);
  });

  it("sorts empty and missing strings first when ascending", () => {
    const l = [cat(1, { volumeName: "x" }), cat(2, { volumeName: "" }), cat(3, { volumeName: undefined as unknown as string })];
    expect(ids(sortCatalogs(l, { column: "volume", direction: "asc" }))).toEqual([2, 3, 1]);
  });

  it("breaks ties by root path ascending in both directions, then original order", () => {
    const l = [
      cat(1, { rootPath: "b", rootSize: 5 }),
      cat(2, { rootPath: "a", rootSize: 5 }),
      cat(3, { rootPath: "A", rootSize: 5 }),
      cat(4, { rootPath: "c", rootSize: 1 }),
    ];
    expect(ids(sortCatalogs(l, { column: "size", direction: "asc" }))).toEqual([4, 2, 3, 1]);
    expect(ids(sortCatalogs(l, { column: "size", direction: "desc" }))).toEqual([2, 3, 1, 4]);
  });

  it("null sort keeps the incoming order in a new array", () => {
    const l = [cat(3), cat(1), cat(2)];
    const out = sortCatalogs(l, null);
    expect(ids(out)).toEqual([3, 1, 2]);
    expect(out).not.toBe(l);
  });

  it("does not mutate its input", () => {
    const l = [cat(2, { rootSize: 2 }), cat(1, { rootSize: 1 })];
    const copy = [...l];
    const out = sortCatalogs(l, { column: "size", direction: "asc" });
    expect(l).toEqual(copy);
    expect(out).not.toBe(l);
  });
});

describe("nextCatalogSort", () => {
  it("starts a column ascending from null or a different column, and flips the same column", () => {
    let s: CatalogSort = null;
    s = nextCatalogSort(s, "size");
    expect(s).toEqual({ column: "size", direction: "asc" });
    s = nextCatalogSort(s, "size");
    expect(s).toEqual({ column: "size", direction: "desc" });
    s = nextCatalogSort(s, "size");
    expect(s).toEqual({ column: "size", direction: "asc" });
    s = nextCatalogSort({ column: "size", direction: "desc" }, "root");
    expect(s).toEqual({ column: "root", direction: "asc" });
  });
});
