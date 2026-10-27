import { describe, expect, it } from "vitest";
import type { DirectoryNode } from "./api/types";
import { flattenTree, type TreeRoot } from "./treeModel";

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
