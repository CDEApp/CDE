import { describe, expect, it } from "vitest";
import type { DirectoryNode } from "./api/types";
import { changedCatalogs, flattenTree, nextSelection, type TreeRoot } from "./treeModel";

describe("changedCatalogs", () => {
  const c = (catalogId: number, over: Partial<TreeRoot> = {}): TreeRoot => ({
    ref: { catalogId, entryIndex: 0 },
    label: `R${catalogId}`,
    fullPath: `R${catalogId}`,
    catalogName: `cat${catalogId}`,
    size: 10,
    ...over,
  });

  it("is empty when the same catalogs are passed again (a refresh keeps the open tree)", () => {
    expect(changedCatalogs([c(0), c(1)], [c(0), c(1)])).toEqual(new Set());
  });

  it("flags only a rescanned catalog (generation bumped)", () => {
    expect(changedCatalogs([c(0), c(1)], [c(0), c(1, { generation: 1 })])).toEqual(new Set([1]));
  });

  it("flags a catalog id now holding a different catalog, or one that went away", () => {
    expect(changedCatalogs([c(0), c(1)], [c(0, { catalogName: "other", fullPath: "D:\\" })])).toEqual(new Set([0, 1]));
  });
});

function root(catalogId: number, label: string): TreeRoot {
  return { ref: { catalogId, entryIndex: 0 }, label, fullPath: label, catalogName: `cat${catalogId}`, size: 100 };
}

function dir(catalogId: number, entryIndex: number, name: string, hasChildren = false): DirectoryNode {
  return {
    ref: { catalogId, entryIndex },
    name,
    fullPath: `/${name}`,
    isDirectory: true,
    hasChildren,
    size: entryIndex,
    modified: "2020-01-01T00:00:00",
    isModifiedBad: false,
    isReparsePoint: false,
  };
}

const view = (rows: ReturnType<typeof flattenTree>) => rows.map((r) => `${"  ".repeat(r.depth)}${r.label}${r.expanded ? "/" : ""}`);

describe("flattenTree", () => {
  const roots = [root(0, "C:\\"), root(1, "D:\\")];
  const children = new Map<string, DirectoryNode[]>([
    ["0-0", [dir(0, 1, "Users", true), dir(0, 2, "Windows", true)]],
    ["0-2", [dir(0, 3, "System32"), dir(0, 4, "WinSxS")]],
  ]);

  it("shows only the roots when nothing is expanded", () => {
    expect(view(flattenTree(roots, new Set(), children))).toEqual(["C:\\", "D:\\"]);
  });

  it("lists an expanded node's children under it, nested by depth, before the next sibling", () => {
    const rows = flattenTree(roots, new Set(["0-0", "0-2"]), children);
    expect(view(rows)).toEqual(["C:\\/", "  Users", "  Windows/", "    System32", "    WinSxS", "D:\\"]);
  });

  it("hides the children of a collapsed node even though they are loaded", () => {
    expect(view(flattenTree(roots, new Set(["0-2"]), children))).toEqual(["C:\\", "D:\\"]);
    expect(view(flattenTree(roots, new Set(["0-0"]), children))).toEqual(["C:\\/", "  Users", "  Windows", "D:\\"]);
  });

  it("treats an expanded node whose children are not loaded yet as collapsed", () => {
    expect(view(flattenTree(roots, new Set(["1-0"]), children))).toEqual(["C:\\", "D:\\"]);
  });

  it("carries the row data the tree draws", () => {
    const rows = flattenTree(roots, new Set(["0-0"]), children);
    expect(rows[0]).toMatchObject({ key: "0-0", isRoot: true, hasChildren: true, catalogName: "cat0", size: 100 });
    expect(rows[2]).toMatchObject({
      key: "0-2",
      ref: { catalogId: 0, entryIndex: 2 },
      fullPath: "/Windows",
      isRoot: false,
      hasChildren: true,
      size: 2,
    });
    expect(rows[2].catalogName).toBeUndefined();
  });
});

describe("flattenTree parent links", () => {
  const roots = [root(0, "C:\\"), root(1, "D:\\")];
  const children = new Map<string, DirectoryNode[]>([
    ["0-0", [dir(0, 1, "Users", true), dir(0, 2, "Windows", true)]],
    ["0-2", [dir(0, 3, "System32")]],
  ]);

  it("links each row to its parent's key; roots have none", () => {
    const rows = flattenTree(roots, new Set(["0-0", "0-2"]), children);
    expect(rows.map((r) => [r.key, r.parentKey])).toEqual([
      ["0-0", null],
      ["0-1", "0-0"],
      ["0-2", "0-0"],
      ["0-3", "0-2"],
      ["1-0", null],
    ]);
  });
});

describe("nextSelection", () => {
  const roots = [root(0, "C:\\"), root(1, "D:\\")];
  const children = new Map<string, DirectoryNode[]>([
    ["0-0", [dir(0, 1, "Users", true), dir(0, 2, "Windows", true)]],
    ["0-2", [dir(0, 3, "System32")]],
  ]);
  // rows: 0-0 C:\ (open), 0-1 Users (collapsed, has kids), 0-2 Windows (open), 0-3 System32 (leaf), 1-0 D:\ (collapsed)
  const rows = flattenTree(roots, new Set(["0-0", "0-2"]), children);
  const nav = (sel: string | null, key: string) => nextSelection(rows, sel, key);

  it("Down selects the next visible row, including an expanded node's first child", () => {
    expect(nav("0-0", "ArrowDown")).toEqual({ select: "0-1" });
    expect(nav("0-3", "ArrowDown")).toEqual({ select: "1-0" });
  });

  it("Down on the last row does nothing", () => {
    expect(nav("1-0", "ArrowDown")).toBeNull();
  });

  it("Up selects the previous visible row; on the first row does nothing", () => {
    expect(nav("1-0", "ArrowUp")).toEqual({ select: "0-3" });
    expect(nav("0-1", "ArrowUp")).toEqual({ select: "0-0" });
    expect(nav("0-0", "ArrowUp")).toBeNull();
  });

  it("Left collapses an expanded folder and keeps the selection on it", () => {
    expect(nav("0-2", "ArrowLeft")).toEqual({ collapse: "0-2" });
  });

  it("Left on a collapsed folder or a leaf selects the parent", () => {
    expect(nav("0-3", "ArrowLeft")).toEqual({ select: "0-2" });
    expect(nav("0-1", "ArrowLeft")).toEqual({ select: "0-0" });
  });

  it("Left collapses an expanded root; on a collapsed root does nothing", () => {
    expect(nav("0-0", "ArrowLeft")).toEqual({ collapse: "0-0" });
    expect(nav("1-0", "ArrowLeft")).toBeNull();
  });

  it("Right expands a collapsed node that has children", () => {
    expect(nav("0-1", "ArrowRight")).toEqual({ expand: "0-1" });
    expect(nav("1-0", "ArrowRight")).toEqual({ expand: "1-0" });
  });

  it("Right does nothing on an expanded node or a leaf", () => {
    expect(nav("0-0", "ArrowRight")).toBeNull();
    expect(nav("0-3", "ArrowRight")).toBeNull();
  });

  it("Home and End select the first and last visible row", () => {
    expect(nav("0-2", "Home")).toEqual({ select: "0-0" });
    expect(nav("0-2", "End")).toEqual({ select: "1-0" });
    expect(nav("0-0", "Home")).toBeNull();
    expect(nav("1-0", "End")).toBeNull();
  });

  it("with nothing selected, Down and Home select the first row, End the last, others nothing", () => {
    expect(nav(null, "ArrowDown")).toEqual({ select: "0-0" });
    expect(nav(null, "Home")).toEqual({ select: "0-0" });
    expect(nav(null, "End")).toEqual({ select: "1-0" });
    expect(nav(null, "ArrowUp")).toBeNull();
    expect(nav(null, "ArrowLeft")).toBeNull();
    expect(nav(null, "ArrowRight")).toBeNull();
  });

  it("treats a selection that is not in rows (collapsed away) like no selection", () => {
    expect(nav("9-9", "ArrowDown")).toEqual({ select: "0-0" });
    expect(nav("9-9", "ArrowRight")).toBeNull();
    expect(nav("9-9", "ArrowLeft")).toBeNull();
  });

  it("does nothing on an empty tree", () => {
    for (const k of ["ArrowDown", "ArrowUp", "ArrowLeft", "ArrowRight", "Home", "End"]) {
      expect(nextSelection([], null, k)).toBeNull();
    }
  });

  it("ignores other keys", () => {
    expect(nav("0-0", "a")).toBeNull();
    expect(nav("0-0", "Enter")).toBeNull();
    expect(nav("0-0", "*")).toBeNull();
  });
});
