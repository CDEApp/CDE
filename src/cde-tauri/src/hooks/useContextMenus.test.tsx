// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { CatalogInfo, DirectoryNode, SearchResultRow, TreemapRow } from "../api/types";
import { applyJob, emptyJobs } from "../jobsModel";
import { useContextMenus, type UseContextMenusOptions } from "./useContextMenus";

const catalog = (catalogId: number, file: string) =>
  ({ catalogId, rootPath: `R${catalogId}:\\`, actualFileName: file, defaultFileName: "" }) as unknown as CatalogInfo;
const ref = { catalogId: 1, entryIndex: 5 };
const labels = (items: { label: string; separator?: boolean }[] | undefined) =>
  (items ?? []).filter((i) => !i.separator).map((i) => i.label);

function setup(over: Partial<UseContextMenusOptions> = {}) {
  const opts: UseContextMenusOptions = {
    commands: [],
    catalogs: [catalog(1, "C-one.cdex")],
    jobs: emptyJobs,
    selectedKeyRef: { current: "1-5" },
    runShell: vi.fn(),
    showTreemap: vi.fn(),
    searchFromHere: vi.fn(),
    findDuplicates: vi.fn(),
    viewInTree: vi.fn(),
    rescanCatalog: vi.fn(),
    editDescription: vi.fn(),
    hashCatalogs: vi.fn(),
    collapseAll: vi.fn(),
    ...over,
  };
  const hook = renderHook((props: UseContextMenusOptions) => useContextMenus(props), { initialProps: opts });
  return { ...hook, opts };
}

describe("useContextMenus", () => {
  it("opens a tree folder's menu where it was clicked, and close clears it", () => {
    const { result } = setup();
    act(() => result.current.onTreeContextMenu(ref, "C:\\a", 10, 20));
    expect(result.current.menu).toMatchObject({ x: 10, y: 20 });
    expect(labels(result.current.menu?.items)).toContain("Collapse all");
    act(() => result.current.close());
    expect(result.current.menu).toBeNull();
  });

  it("keeps the Directory pane's handlers stable while the inputs change, and builds from the latest inputs", () => {
    const { result, rerender, opts } = setup();
    const before = { tree: result.current.onTreeContextMenu, list: result.current.onListContextMenu };
    rerender({ ...opts, commands: [{ id: 0, label: "Open in VS Code" }], catalogs: [catalog(1, "C-one.cdex"), catalog(2, "x.cdex")] });
    expect(result.current.onTreeContextMenu).toBe(before.tree);
    expect(result.current.onListContextMenu).toBe(before.list);
    act(() => result.current.onListContextMenu({ ref, fullPath: "C:\\a.txt", isDirectory: false } as DirectoryNode, 0, 0));
    expect(labels(result.current.menu?.items)).toContain("Open in VS Code");
  });

  it("the tree background menu maps the selected folder, and offers no TreeMap with nothing selected", () => {
    const { result, opts } = setup();
    act(() => result.current.onTreeBackgroundMenu(0, 0));
    result.current.menu!.items[0].onClick();
    expect(opts.showTreemap).toHaveBeenCalledWith({ catalogId: 1, entryIndex: 5 });

    const none = setup({ selectedKeyRef: { current: null } });
    act(() => none.result.current.onTreeBackgroundMenu(0, 0));
    expect(none.result.current.menu!.items[0].disabled).toBe(true);
  });

  it("a search result from a catalog that was reloaded away cannot edit that catalog's description", () => {
    const { result } = setup();
    const row = (catalogName: string) => ({ ref, fullPath: "C:\\a", isDirectory: true, catalogName }) as SearchResultRow;
    const editItem = () => result.current.menu!.items.find((i) => i.label === "Edit Description…")!;
    act(() => result.current.onResultContextMenu(row("C-one.cdex"), [], 0, 0));
    expect(editItem().disabled).toBeFalsy();
    act(() => result.current.onResultContextMenu(row("other.cdex"), [], 0, 0));
    expect(editItem().disabled).toBe(true);
  });

  it("the catalog row menu edits that catalog's description and rescans it", () => {
    const { result, opts } = setup();
    act(() => result.current.onCatalogContextMenu(opts.catalogs[0], 0, 0));
    expect(labels(result.current.menu?.items)).toEqual(["Show TreeMap", "Edit Description…", "Rescan", "Hash files…"]);
    result.current.menu!.items[1].onClick();
    expect(opts.editDescription).toHaveBeenCalledWith(opts.catalogs[0]);
    result.current.menu!.items[2].onClick();
    expect(opts.rescanCatalog).toHaveBeenCalledWith(1);
    result.current.menu!.items[3].onClick();
    expect(opts.hashCatalogs).toHaveBeenCalledWith([1]);
  });

  it("disables Hash files… for a legacy catalog and for one a job holds", () => {
    const legacy = { ...catalog(1, "C-one.cde"), sourcePath: "C:\\cats\\C-one.cde" } as CatalogInfo;
    const a = setup({ catalogs: [legacy] });
    act(() => a.result.current.onCatalogContextMenu(legacy, 0, 0));
    expect(a.result.current.menu!.items[3]).toMatchObject({ disabled: true, title: expect.stringMatching(/legacy/i) });

    const held = applyJob(emptyJobs, {
      id: 1,
      kind: "hash",
      title: "Hashing",
      catalogId: null,
      catalogIds: [1],
      catalogName: "x",
      state: "running",
      queuedAtUtc: "",
      startedAtUtc: null,
      endedAtUtc: null,
      phase: "hashing",
      entriesScanned: 0,
      note: "",
      result: null,
      error: null,
    });
    const b = setup({ jobs: held });
    act(() => b.result.current.onCatalogContextMenu(b.opts.catalogs[0], 0, 0));
    expect(b.result.current.menu!.items[3]).toMatchObject({ disabled: true, title: expect.stringMatching(/already queued or running/) });
  });

  it("a treemap tile's View in Tree reveals that entry", () => {
    const { result, opts } = setup();
    const tile = { ref, isDirectory: true } as TreemapRow;
    act(() => result.current.onTreemapTileMenu(tile, "C:\\a", 0, 0));
    result.current.menu!.items.find((i) => i.label === "View in Tree")!.onClick();
    expect(opts.viewInTree).toHaveBeenCalledWith(ref);
  });

  it("the Directory tab menu collapses the tree", () => {
    const { result, opts } = setup();
    act(() => result.current.onDirectoryTabMenu(0, 0));
    result.current.menu!.items[0].onClick();
    expect(opts.collapseAll).toHaveBeenCalledOnce();
  });
});
