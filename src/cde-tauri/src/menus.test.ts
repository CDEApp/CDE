import { describe, expect, it, vi } from "vitest";
import {
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
import type { DirectoryNode } from "./api/types";

const ref = { catalogId: 3, entryIndex: 9 };

function context(overrides: Partial<EntryMenuContext> = {}) {
  const ctx: EntryMenuContext = {
    commands: [],
    runShell: vi.fn(),
    showTreemap: vi.fn(),
    searchFromHere: vi.fn(),
    copyPath: vi.fn(),
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
