import { describe, expect, it } from "vitest";
import type { DirectoryNode } from "./api/types";
import { alignChildren, type ChildLists, DIFF_FILTERS, DIFF_MENU_FILTERS, type DiffFilterId, flattenVisible, relativeSegments } from "./diffModel";

let next = 0;
function node(name: string, over: Partial<DirectoryNode> = {}): DirectoryNode {
  return {
    ref: { catalogId: 1, entryIndex: next++ },
    name,
    fullPath: `C:\\${name}`,
    isDirectory: false,
    hasChildren: false,
    size: 10,
    modified: "2024-01-01T10:00:00Z",
    isModifiedBad: false,
    isReparsePoint: false,
    ...over,
  };
}
const dir = (name: string, size = 100) => node(name, { isDirectory: true, hasChildren: true, size });

describe("alignChildren", () => {
  it("lists folders first, then names ignoring case", () => {
    const rows = alignChildren([node("b.txt"), dir("Zed"), node("A.txt")], [dir("alpha")], 0, "");
    expect(rows.map((r) => r.name)).toEqual(["alpha", "Zed", "A.txt", "b.txt"]);
  });

  it("matches names case-insensitively and keys by parent path", () => {
    const [row] = alignChildren([node("Readme.TXT")], [node("readme.txt")], 2, "/docs");
    expect(row.key).toBe("/docs/readme.txt");
    expect(row.depth).toBe(2);
    expect(row.left?.name).toBe("Readme.TXT");
    expect(row.right?.name).toBe("readme.txt");
    expect(row.status).toBe("same");
  });

  it("marks names present on one side only", () => {
    const rows = alignChildren([node("l.txt")], [node("r.txt")], 0, "");
    expect(rows.map((r) => [r.name, r.status, r.left === null, r.right === null])).toEqual([
      ["l.txt", "left-only", false, true],
      ["r.txt", "right-only", true, false],
    ]);
  });

  it.each([
    ["equal size and time", {}, "same"],
    ["time within 2 seconds", { modified: "2024-01-01T10:00:02Z" }, "same"],
    ["time beyond 2 seconds", { modified: "2024-01-01T10:00:03Z" }, "different"],
    ["different size", { size: 11 }, "different"],
    ["one bad date", { isModifiedBad: true }, "different"],
  ])("file with %s", (_label, over, status) => {
    const [row] = alignChildren([node("f")], [node("f", over)], 0, "");
    expect(row.status).toBe(status);
  });

  it("treats two bad dates as equal", () => {
    const bad = { isModifiedBad: true };
    expect(alignChildren([node("f", bad)], [node("f", bad)], 0, "")[0].status).toBe("same");
  });

  it("compares a folder on both sides by aggregated size only", () => {
    const same = alignChildren([dir("d", 5)], [{ ...dir("d", 5), modified: "1999-01-01T00:00:00Z" }], 0, "")[0];
    expect(same.status).toBe("same");
    expect(same.isDirectory).toBe(true);
    expect(alignChildren([dir("d", 5)], [dir("d", 6)], 0, "")[0].status).toBe("different");
  });

  it("makes a file-versus-folder name one non-expandable different row", () => {
    const [row] = alignChildren([node("x")], [dir("x")], 0, "");
    expect(row.status).toBe("different");
    expect(row.isDirectory).toBe(false);
  });
});

describe("flattenVisible", () => {
  const roots = alignChildren(
    [dir("same-dir", 5), dir("diff-dir", 5), node("only-left"), node("same-file")],
    [dir("same-dir", 5), dir("diff-dir", 9), node("same-file")],
    0,
    "",
  );
  const children = new Map<string, ChildLists>([
    ["/diff-dir", { left: [node("a"), node("b")], right: [node("a", { size: 99 }), node("c")] }],
    ["/same-dir", { left: [node("k")], right: [node("k")] }],
  ]);
  const names = (rows: { name: string }[]) => rows.map((r) => r.name);

  it("shows only the roots while nothing is expanded", () => {
    expect(names(flattenVisible(new Set(), children, roots, "all"))).toEqual([
      "diff-dir", "same-dir", "only-left", "same-file",
    ]);
  });

  it("inserts the children of an expanded folder after it", () => {
    const rows = flattenVisible(new Set(["/diff-dir"]), children, roots, "all");
    expect(names(rows)).toEqual(["diff-dir", "a", "b", "c", "same-dir", "only-left", "same-file"]);
    expect(rows.find((r) => r.name === "a")?.depth).toBe(1);
  });

  it("filters diffs, hiding same folders with their children", () => {
    const rows = flattenVisible(new Set(["/diff-dir", "/same-dir"]), children, roots, "differences");
    expect(names(rows)).toEqual(["diff-dir", "a", "b", "c", "only-left"]);
  });

  it("filters same, hiding different folders with their children", () => {
    const rows = flattenVisible(new Set(["/diff-dir", "/same-dir"]), children, roots, "same");
    expect(names(rows)).toEqual(["same-dir", "k", "same-file"]);
  });

  it("adds no children for an expanded folder that is not loaded yet", () => {
    expect(names(flattenVisible(new Set(["/diff-dir"]), new Map(), roots, "all"))).toEqual([
      "diff-dir", "same-dir", "only-left", "same-file",
    ]);
  });
});

describe("DIFF_FILTERS", () => {
  const T0 = "2024-01-01T10:00:00Z";
  const T1 = "2024-01-01T11:00:00Z";
  const rows = alignChildren(
    [
      node("same"),
      node("leftNewer", { modified: T1 }),
      node("rightNewer"),
      node("sizeOnly", { size: 11 }),
      node("badDate", { isModifiedBad: true }),
      node("leftOnly"),
    ],
    [
      node("same"),
      node("leftNewer", { modified: T0 }),
      node("rightNewer", { modified: T1 }),
      node("sizeOnly", { size: 12 }),
      node("badDate"),
      node("rightOnly"),
    ],
    0,
    "",
  );

  const shown = (id: DiffFilterId) =>
    rows.filter(DIFF_FILTERS[id].shows).map((r) => r.name).sort();

  it.each<[DiffFilterId, string[]]>([
    ["all", ["badDate", "leftNewer", "leftOnly", "rightNewer", "rightOnly", "same", "sizeOnly"]],
    ["same", ["same"]],
    ["differences", ["badDate", "leftNewer", "leftOnly", "rightNewer", "rightOnly", "sizeOnly"]],
    ["noOrphans", ["badDate", "leftNewer", "rightNewer", "same", "sizeOnly"]],
    ["differencesNoOrphans", ["badDate", "leftNewer", "rightNewer", "sizeOnly"]],
    ["orphans", ["leftOnly", "rightOnly"]],
    ["leftNewer", ["leftNewer"]],
    ["rightNewer", ["rightNewer"]],
    ["leftNewerLeftOrphans", ["leftNewer", "leftOnly"]],
    ["rightNewerRightOrphans", ["rightNewer", "rightOnly"]],
    ["leftOrphans", ["leftOnly"]],
    ["rightOrphans", ["rightOnly"]],
  ])("%s", (id, expected) => {
    expect(shown(id)).toEqual(expected);
  });

  it("lists the ten menu entries in order with their labels", () => {
    expect(DIFF_MENU_FILTERS.map((id) => DIFF_FILTERS[id].label)).toEqual([
      "Show Differences",
      "Show No Orphans",
      "Show Differences but No Orphans",
      "Show Orphans",
      "Show Left Newer",
      "Show Right Newer",
      "Show Left Newer and Left Orphans",
      "Show Right Newer and Right Orphans",
      "Show Left Orphans",
      "Show Right Orphans",
    ]);
  });
});

describe("relativeSegments", () => {
  it.each([
    ["C:\\", "C:\\Users\\Bob", ["Users", "Bob"]],
    ["C:\\data", "c:/DATA/x\\y/", ["x", "y"]],
    ["/", "/usr/lib", ["usr", "lib"]],
    ["/home/me", "/home/me", []],
    ["C:\\data", "C:\\other", null],
    ["C:\\data\\deep", "C:\\data", null],
  ])("%s -> %s", (root, path, expected) => {
    expect(relativeSegments(root, path)).toEqual(expected);
  });
});
