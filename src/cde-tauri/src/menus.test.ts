import { describe, expect, it, vi } from "vitest";
import {
  catalogMenu,
  collapseAllItem,
  editDescriptionItem,
  entryMenu,
  listMenu,
  rescanItem,
  treeBackgroundMenu,
  treeNodeMenu,
  type EntryMenuContext,
} from "./menus";
import { applyJob, emptyJobs } from "./jobsModel";
import type { JobDto } from "./api/types";
import type { DirectoryNode, SearchResultRow } from "./api/types";

const ref = { catalogId: 3, entryIndex: 9 };

function context(overrides: Partial<EntryMenuContext> = {}) {
  const ctx: EntryMenuContext = {
    commands: [],
    runShell: vi.fn(),
    showTreemap: vi.fn(),
    searchFromHere: vi.fn(),
    findDuplicates: vi.fn(),
    copyPath: vi.fn(),
    copyText: vi.fn(),
    rescanFor: (catalogId) => ({ label: "Rescan", onClick: () => {}, title: `rescan ${catalogId}` }),
    editDescription: vi.fn(),
    ...overrides,
  };
  return ctx;
}

const labels = (items: { label: string }[]) => items.map((i) => i.label);

describe("entryMenu", () => {
  it("a file gets the shell actions, a separator and Copy Full Path only", () => {
    const items = entryMenu(context(), { ref, fullPath: "/a/b.txt", isDirectory: false });
    expect(labels(items)).toEqual(["Open", "Explore", "Properties", "", "Copy Full Path", "Edit Description…"]);
    expect(items[3].separator).toBe(true);
  });

  it("Edit Description… opens the editor for the entry's catalog", () => {
    const ctx = context();
    const items = entryMenu(ctx, { ref, fullPath: "/a/b.txt", isDirectory: false });
    items.find((i) => i.label === "Edit Description…")!.onClick();
    expect(ctx.editDescription).toHaveBeenCalledWith(3);
  });

  it("Edit Description… is disabled for an entry that may belong to a reloaded catalog", () => {
    const items = entryMenu(context(), { ref, fullPath: "/a/b.txt", isDirectory: false, canEditDescription: false });
    expect(items.find((i) => i.label === "Edit Description…")!.disabled).toBe(true);
  });

  it("a folder adds Show TreeMap and Search from here before the path item and Rescan last", () => {
    const items = entryMenu(context(), { ref, fullPath: "/a", isDirectory: true });
    expect(labels(items)).toEqual(["Open", "Explore", "Properties", "", "Show TreeMap", "Search from here", "Copy Full Path", "Rescan", "Edit Description…"]);
    expect(items[items.length - 2].title).toBe("rescan 3");
  });

  it("View in Tree appears only when a handler is given, between Copy Full Path and Rescan", () => {
    const onViewInTree = vi.fn();
    const items = entryMenu(context(), { ref, fullPath: "/a", isDirectory: true, onViewInTree });
    expect(labels(items).slice(-4)).toEqual(["Copy Full Path", "View in Tree", "Rescan", "Edit Description…"]);
    items.find((i) => i.label === "View in Tree")!.onClick();
    expect(onViewInTree).toHaveBeenCalledOnce();
  });

  it("custom commands follow the built-in actions and run with their id", () => {
    const ctx = context({ commands: [{ id: 11, label: "Edit" }, { id: 12, label: "Diff" }] });
    const items = entryMenu(ctx, { ref, fullPath: "/a", isDirectory: false });
    expect(labels(items).slice(0, 5)).toEqual(["Open", "Explore", "Properties", "Edit", "Diff"]);
    items[4].onClick();
    expect(ctx.runShell).toHaveBeenCalledWith(ref, "custom", 12);
  });

  it("items call the matching action with the entry", () => {
    const ctx = context();
    const items = entryMenu(ctx, { ref, fullPath: "/a", isDirectory: true });
    for (const label of ["Open", "Explore", "Properties", "Show TreeMap", "Search from here", "Copy Full Path"]) {
      items.find((i) => i.label === label)!.onClick();
    }
    expect(ctx.runShell).toHaveBeenNthCalledWith(1, ref, "open");
    expect(ctx.runShell).toHaveBeenNthCalledWith(2, ref, "explore");
    expect(ctx.runShell).toHaveBeenNthCalledWith(3, ref, "properties");
    expect(ctx.showTreemap).toHaveBeenCalledWith(ref);
    expect(ctx.searchFromHere).toHaveBeenCalledWith(ref, "/a");
    expect(ctx.copyPath).toHaveBeenCalledWith("/a");
  });
});

describe("treeNodeMenu", () => {
  it("is the folder menu plus Collapse all", () => {
    const onCollapseAll = vi.fn();
    const items = treeNodeMenu(context(), ref, "/a", onCollapseAll);
    expect(labels(items)).toEqual([
      "Open", "Explore", "Properties", "", "Show TreeMap", "Search from here", "Copy Full Path", "Rescan", "Edit Description…", "Collapse all",
    ]);
    items[items.length - 1].onClick();
    expect(onCollapseAll).toHaveBeenCalledOnce();
  });
});

describe("listMenu", () => {
  const node = (isDirectory: boolean): DirectoryNode => ({
    ref, name: "x", fullPath: "/a/x", isDirectory, hasChildren: false, size: 0, modified: "", isModifiedBad: false, isReparsePoint: false,
  });

  it("follows the node's own directory flag", () => {
    expect(labels(listMenu(context(), node(true)))).toContain("Show TreeMap");
    expect(labels(listMenu(context(), node(false)))).not.toContain("Show TreeMap");
  });

  it("never offers Collapse all", () => {
    expect(labels(listMenu(context(), node(true)))).not.toContain("Collapse all");
  });
});

describe("catalogMenu", () => {
  it("offers Show TreeMap, Edit Description… and the rescan item, in that order", () => {
    const rescan = { label: "Rescan", onClick: () => {} };
    const items = catalogMenu(7, vi.fn(), vi.fn(), rescan);
    expect(labels(items)).toEqual(["Show TreeMap", "Edit Description…", "Rescan"]);
    expect(items[2]).toBe(rescan);
  });

  it("Show TreeMap maps the catalog's root", () => {
    const showTreemap = vi.fn();
    catalogMenu(7, showTreemap, vi.fn(), { label: "Rescan", onClick: () => {} })[0].onClick();
    expect(showTreemap).toHaveBeenCalledWith({ catalogId: 7, entryIndex: 0 });
  });

  it("Edit Description… runs the handler", () => {
    const onEdit = vi.fn();
    catalogMenu(7, vi.fn(), onEdit, { label: "Rescan", onClick: () => {} })[1].onClick();
    expect(onEdit).toHaveBeenCalledOnce();
  });
});

describe("treeBackgroundMenu", () => {
  it("disables Show TreeMap when nothing is selected", () => {
    const showTreemap = vi.fn();
    const items = treeBackgroundMenu(null, showTreemap, () => {});
    expect(labels(items)).toEqual(["Show TreeMap", "Collapse all"]);
    expect(items[0].disabled).toBe(true);
    items[0].onClick();
    expect(showTreemap).not.toHaveBeenCalled();
  });

  it("maps the selection when there is one", () => {
    const showTreemap = vi.fn();
    const items = treeBackgroundMenu(ref, showTreemap, () => {});
    expect(items[0].disabled).toBe(false);
    items[0].onClick();
    expect(showTreemap).toHaveBeenCalledWith(ref);
  });
});

describe("collapseAllItem", () => {
  it("calls the handler", () => {
    const f = vi.fn();
    collapseAllItem(f).onClick();
    expect(f).toHaveBeenCalledOnce();
  });
});

describe("rescanItem", () => {
  it("is enabled with a tooltip naming the root", () => {
    const onRescan = vi.fn();
    const item = rescanItem(emptyJobs, 3, "/data", onRescan);
    expect(item).toMatchObject({ label: "Rescan", disabled: false, title: "Rescan /data in the background" });
    item.onClick();
    expect(onRescan).toHaveBeenCalledWith(3);
  });

  it("has no tooltip when the root is unknown", () => {
    expect(rescanItem(emptyJobs, 3, undefined, () => {}).title).toBeUndefined();
  });

  it("is disabled with the running-rescan reason while that catalog rescans", () => {
    const state = applyJob(emptyJobs, { id: 1, kind: "rescan", catalogId: 3, state: "queued" } as JobDto);
    expect(rescanItem(state, 3, "/data", () => {})).toMatchObject({
      disabled: true,
      title: "A job for this catalog is already queued or running",
    });
    expect(rescanItem(state, 4, "/other", () => {}).disabled).toBe(false);
  });
});

describe("editDescriptionItem", () => {
  it("is a labelled item that runs the handler", () => {
    const onEdit = vi.fn();
    const item = editDescriptionItem(onEdit);
    expect(item.label).toBe("Edit Description…");
    item.onClick();
    expect(onEdit).toHaveBeenCalledOnce();
  });
});

const res = (name: string, parentPath: string, entryIndex: number) =>
  ({ ref: { catalogId: 3, entryIndex }, name, parentPath, fullPath: `${parentPath}${name}`, isDirectory: false }) as SearchResultRow;
const HASH = "0123456789abcdef0123456789abcdef";
const NL = String.fromCharCode(10);
const two = [res("b.txt", "/x/", 1), res("a.txt", "/y/", 2)];
const one = (items: { label: string }[], label: string) => items.find((i) => i.label === label)! as ReturnType<typeof entryMenu>[number];

describe("entryMenu for search results", () => {
  it("a single result keeps the shell actions enabled and offers the three copy variants", () => {
    const ctx = context({ commands: [{ id: 7, label: "Hash" }] });
    const items = entryMenu(ctx, { ref, fullPath: "/x/b.txt", isDirectory: false, selection: [two[0]] });
    expect(labels(items)).toEqual(["Open", "Explore", "Properties", "Hash", "", "Find duplicates of this file", "Copy Full Path", "Copy Name", "Copy Parent Folder", "Edit Description…"]);
    expect(items.filter((i) => i.disabled).map((i) => i.label)).toEqual(["Find duplicates of this file"]);
    one(items, "Open").onClick();
    expect(ctx.runShell).toHaveBeenCalledWith(ref, "open");
  });

  it("Find duplicates of this file searches the file's content hash", () => {
    const ctx = context();
    const hashed = { ...two[0], contentHash: HASH };
    const items = entryMenu(ctx, { ref, fullPath: "/x/b.txt", isDirectory: false, selection: [hashed] });
    const item = one(items, "Find duplicates of this file");
    expect(item.disabled).toBeFalsy();
    item.onClick();
    expect(ctx.findDuplicates).toHaveBeenCalledWith(HASH);
  });

  it("Find duplicates is disabled with a reason for an unhashed file and for a folder", () => {
    const unhashed = one(entryMenu(context(), { ref, fullPath: "/x/b.txt", isDirectory: false, selection: [two[0]] }), "Find duplicates of this file");
    expect(unhashed.disabled).toBe(true);
    expect(unhashed.title).toMatch(/no full content hash/);
    const folder = one(entryMenu(context(), { ref, fullPath: "/x", isDirectory: true, selection: [{ ...two[0], isDirectory: true }] }), "Find duplicates of this file");
    expect(folder.disabled).toBe(true);
    expect(folder.title).toMatch(/Folders/);
  });

  it("the tree and list menus have no Find duplicates item", () => {
    expect(labels(entryMenu(context(), { ref, fullPath: "/x/b.txt", isDirectory: false }))).not.toContain("Find duplicates of this file");
  });

  it("copy items put the selected values on the clipboard, one per line in the given order", () => {
    const ctx = context();
    const items = entryMenu(ctx, { ref, fullPath: "/x/b.txt", isDirectory: false, selection: two });
    one(items, "Copy Full Paths").onClick();
    one(items, "Copy Names").onClick();
    one(items, "Copy Parent Folders").onClick();
    expect(ctx.copyText).toHaveBeenNthCalledWith(1, "/x/b.txt" + NL + "/y/a.txt");
    expect(ctx.copyText).toHaveBeenNthCalledWith(2, "b.txt" + NL + "a.txt");
    expect(ctx.copyText).toHaveBeenNthCalledWith(3, "/x/" + NL + "/y/");
  });

  it("with several results the single-entry items are disabled and say why, while copy stays enabled", () => {
    const ctx = context({ commands: [{ id: 7, label: "Hash" }] });
    const items = entryMenu(ctx, { ref, fullPath: "/x", isDirectory: true, selection: two, onViewInTree: () => {} });
    const disabled = items.filter((i) => i.disabled).map((i) => i.label);
    expect(disabled).toEqual([
      "Open (one item only)",
      "Explore (one item only)",
      "Properties (one item only)",
      "Hash (one item only)",
      "Show TreeMap (one item only)",
      "Search from here (one item only)",
      "Find duplicates of this file (one item only)",
      "View in Tree (one item only)",
      "Rescan (one item only)",
      "Edit Description… (one item only)",
    ]);
    for (const i of items.filter((x) => x.disabled)) expect(i.title).toBeTruthy();
    expect(one(items, "Copy Full Paths").disabled).toBeFalsy();
    one(items, "Open (one item only)").onClick();
  });
});
