import { describe, expect, it } from "vitest";
import { flipDirection, isHiddenByFilter, sortForKey, visibleRoots, type RootSort } from "./rootsView";
import type { TreeRoot } from "./treeModel";

const mk = (id: number, label: string, catalogName: string, size: number, fullPath = label + "\\"): TreeRoot => ({
  ref: { catalogId: id, entryIndex: 0 },
  label,
  fullPath,
  catalogName,
  size,
});

const c = mk(1, "C:", "zeta", 300);
const d = mk(2, "d:", "Alpha", 100);
const e = mk(3, "E:", "mid", 200);
const server = [c, d, e];
const ids = (rs: TreeRoot[]) => rs.map((r) => r.ref.catalogId);

describe("visibleRoots sorting", () => {
  const cases: [string, RootSort, number[]][] = [
    ["null keeps incoming order", null, [1, 2, 3]],
    ["root asc, ignoring case", { key: "root", direction: "asc" }, [1, 2, 3]],
    ["root desc", { key: "root", direction: "desc" }, [3, 2, 1]],
    ["catalog asc, ignoring case", { key: "catalog", direction: "asc" }, [2, 3, 1]],
    ["catalog desc", { key: "catalog", direction: "desc" }, [1, 3, 2]],
    ["size asc", { key: "size", direction: "asc" }, [2, 3, 1]],
    ["size desc", { key: "size", direction: "desc" }, [1, 3, 2]],
  ];
  it.each(cases)("%s", (_n, sort, expected) => {
    expect(ids(visibleRoots(server, sort, ""))).toEqual(expected);
  });

  it("breaks ties by root label, ascending even when the primary key is descending", () => {
    const rs = [mk(1, "b:", "x", 5), mk(2, "A:", "x", 5), mk(3, "c:", "x", 9)];
    expect(ids(visibleRoots(rs, { key: "size", direction: "desc" }, ""))).toEqual([3, 2, 1]);
    expect(ids(visibleRoots(rs, { key: "catalog", direction: "desc" }, ""))).toEqual([2, 1, 3]);
  });

  it("keeps incoming order for identical keys", () => {
    const rs = [mk(1, "A:", "x", 5), mk(2, "a:", "x", 5)];
    expect(ids(visibleRoots(rs, { key: "size", direction: "asc" }, ""))).toEqual([1, 2]);
  });

  it("does not mutate the input", () => {
    const input = [c, d, e];
    visibleRoots(input, { key: "size", direction: "asc" }, "a");
    expect(input).toEqual([c, d, e]);
    expect(visibleRoots(input, null, "")).not.toBe(input);
  });
});

describe("visibleRoots filtering", () => {
  const f = (filter: string) => ids(visibleRoots(server, null, filter));
  it("empty or blank keeps all", () => {
    expect(f("")).toEqual([1, 2, 3]);
    expect(f("   ")).toEqual([1, 2, 3]);
  });
  it("matches the root name", () => expect(f("d:")).toEqual([2]));
  it("matches the root path", () => {
    const rs = [mk(1, "data", "a", 1, "/mnt/data"), mk(2, "x", "b", 1, "/srv/x")];
    expect(ids(visibleRoots(rs, null, "mnt"))).toEqual([1]);
  });
  it("matches the catalog name", () => expect(f("mi")).toEqual([3]));
  it("is case-insensitive and trims", () => expect(f("  ALPHA ")).toEqual([2]));
  it("matches partially in the middle", () => expect(f("lph")).toEqual([2]));
  it("returns nothing when nothing matches", () => expect(f("nope")).toEqual([]));
  it("filters then sorts", () => {
    expect(ids(visibleRoots(server, { key: "size", direction: "desc" }, "a"))).toEqual([1, 2]);
  });
});

describe("sort helpers", () => {
  it("size starts descending, text keys ascending, default is null", () => {
    expect(sortForKey("size")).toEqual({ key: "size", direction: "desc" });
    expect(sortForKey("root")).toEqual({ key: "root", direction: "asc" });
    expect(sortForKey(null)).toBeNull();
  });
  it("flips direction and leaves null alone", () => {
    expect(flipDirection({ key: "root", direction: "asc" })).toEqual({ key: "root", direction: "desc" });
    expect(flipDirection(null)).toBeNull();
  });
});

describe("isHiddenByFilter", () => {
  it("is false for a blank filter", () => expect(isHiddenByFilter(server, 2, "  ")).toBe(false));
  it("is true when the filter excludes the catalog's roots", () => expect(isHiddenByFilter(server, 2, "zeta")).toBe(true));
  it("is false when the filter matches the catalog", () => expect(isHiddenByFilter(server, 2, "alpha")).toBe(false));
  it("is false for an unknown catalog", () => expect(isHiddenByFilter(server, 99, "zeta")).toBe(false));
});
