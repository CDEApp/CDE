// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { CatalogsResponse, DirectoryNode, EntryRef } from "../api/types";
import { folderChain, parseRef, useCatalogSelection } from "./useCatalogSelection";

const { show } = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock("@mantine/notifications", () => ({ notifications: { show } }));

const node = (catalogId: number, entryIndex: number, name: string, fullPath: string, isDirectory = true) =>
  ({ ref: { catalogId, entryIndex }, name, fullPath, isDirectory }) as DirectoryNode;

const root = node(1, 0, "r", "/r");
const a = node(1, 1, "a", "/r/a");
const b = node(1, 2, "b", "/r/a/b");

// A tiny catalog: /r > a > b. `children` lists by parent entry index; `path` returns the chain to a ref.
function fakeClient(tree: Record<number, DirectoryNode[]>, chains: Record<number, DirectoryNode[]>) {
  return {
    children: vi.fn(async (ref: EntryRef) => tree[ref.entryIndex] ?? []),
    path: vi.fn(async (ref: EntryRef) => chains[ref.entryIndex] ?? []),
    catalogs: vi.fn(async () => ({ catalogs: [{ catalogId: 1, rootPath: "/r" }] }) as unknown as CatalogsResponse),
  } as unknown as CdeApiClient;
}

const chains = { 0: [root], 1: [root, a], 2: [root, a, b] };

function setup(tree: Record<number, DirectoryNode[]> = { 0: [a], 1: [b] }) {
  const client = fakeClient(tree, chains);
  const showDirectory = vi.fn();
  const hook = renderHook(() => useCatalogSelection({ getClient: () => client, showDirectory }));
  return { ...hook, client, showDirectory };
}

beforeEach(() => show.mockClear());

describe("ref helpers", () => {
  it("parseRef inverts the catalog-index key and rejects junk", () => {
    expect(parseRef("3-12")).toEqual({ catalogId: 3, entryIndex: 12 });
    expect(parseRef("x-1")).toBeNull();
  });

  it("folderChain returns the ref's root-to-entry chain while it still ends at the folder's path", async () => {
    const client = fakeClient({}, chains);
    expect(await folderChain(client, b.ref, b.fullPath)).toEqual({ chain: [root, a, b], exact: true });
    expect(client.catalogs).not.toHaveBeenCalled();
  });
});

describe("useCatalogSelection", () => {
  it("selectNode records the selection and loads its listing", async () => {
    const { result } = setup();
    await act(() => result.current.selectNode(a.ref, a.fullPath));
    expect(result.current.selectedKey).toBe("1-1");
    expect(result.current.dirPath).toBe("/r/a");
    expect(result.current.dirNodes).toEqual([b]);
  });

  it("drops a listing that a newer selection overtook", async () => {
    let release!: (n: DirectoryNode[]) => void;
    const { result, client } = setup();
    (client.children as ReturnType<typeof vi.fn>)
      .mockImplementationOnce(() => new Promise((resolve) => (release = resolve)))
      .mockImplementationOnce(async () => [b]);
    let first!: Promise<boolean>;
    act(() => {
      first = result.current.selectNode(root.ref, root.fullPath);
    });
    await act(() => result.current.selectNode(a.ref, a.fullPath));
    await act(async () => {
      release([a]);
      expect(await first).toBe(false);
    });
    expect(result.current.selectedKey).toBe("1-1");
    expect(result.current.dirNodes).toEqual([b]);
  });

  it("restores the selection to the nearest surviving parent after a rescan removed it", async () => {
    // After the rescan /r/a exists but /r/a/b is gone, and so is its old index.
    const { result, client } = setup({ 0: [a], 1: [] });
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    const snapshot = result.current.snapshot();
    (client.path as ReturnType<typeof vi.fn>).mockResolvedValueOnce([]);

    await act(() => result.current.restoreAfterRescan(client, 1, snapshot));
    expect(result.current.selectedKey).toBe("1-1");
    expect(result.current.dirPath).toBe("/r/a");
  });

  it("does not override a folder the user clicked while the restore was still working", async () => {
    const { result, client } = setup({ 0: [a], 1: [b] });
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    const snapshot = result.current.snapshot();

    // Hold the restore at its first await (the old ref, whose index the rescan shifted), then let the
    // user click the root before it resumes.
    let release!: () => void;
    (client.path as ReturnType<typeof vi.fn>).mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          release = () => resolve([]);
        }),
    );
    let restoring!: Promise<void>;
    act(() => {
      restoring = result.current.restoreAfterRescan(client, 1, snapshot);
    });
    await act(() => result.current.selectNode(root.ref, root.fullPath));
    await act(async () => {
      release();
      await restoring;
    });
    expect(result.current.selectedKey).toBe("1-0");
    expect(result.current.dirPath).toBe("/r");
  });

  it("keeps the exact folder when it survives the rescan", async () => {
    const { result, client } = setup({ 0: [a], 1: [b], 2: [] });
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    await act(() => result.current.restoreAfterRescan(client, 1, result.current.snapshot()));
    expect(result.current.selectedKey).toBe("1-2");
    expect(result.current.expandTo).toEqual([root.ref, a.ref, b.ref]);
  });

  it("leaves the selection alone when it is in a different catalog", async () => {
    const { result, client } = setup();
    await act(() => result.current.selectNode(a.ref, a.fullPath));
    await act(() => result.current.restoreAfterRescan(client, 2, result.current.snapshot()));
    expect(result.current.selectedKey).toBe("1-1");
    expect(client.catalogs).not.toHaveBeenCalled();
  });

  it("resetForReload clears the selection and the listing", async () => {
    const { result } = setup();
    await act(() => result.current.selectNode(a.ref, a.fullPath));
    act(() => result.current.resetForReload());
    expect(result.current.selectedKey).toBeNull();
    expect(result.current.dirPath).toBe("");
    expect(result.current.dirNodes).toEqual([]);
    expect(result.current.expandTo).toBeUndefined();
  });

  it("viewInTree shows the directory view, selects the parent and reveals it", async () => {
    const { result, showDirectory } = setup();
    await act(() => result.current.viewInTree(b.ref, b.fullPath));
    expect(showDirectory).toHaveBeenCalledOnce();
    expect(result.current.selectedKey).toBe("1-1"); // the parent of b
    expect(result.current.revealKey).toBe("1-1");
    expect(result.current.revealNonce).toBe(1);
    expect(result.current.expandTo).toEqual([root.ref, a.ref]);
  });

  it("viewInTree warns instead of navigating when the row no longer resolves", async () => {
    const { result } = setup();
    await act(() => result.current.viewInTree(b.ref, "/r/elsewhere/b"));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Not found" }));
    expect(result.current.selectedKey).toBeNull();
  });

  it("canGoUp is false with no selection and at a catalog root, true below it", async () => {
    const { result } = setup();
    expect(result.current.canGoUp).toBe(false);
    await act(() => result.current.selectNode(root.ref, root.fullPath));
    expect(result.current.canGoUp).toBe(false);
    await act(() => result.current.selectNode(a.ref, a.fullPath));
    expect(result.current.canGoUp).toBe(true);
  });

  it("goUp opens the parent folder and expands the tree to it", async () => {
    const { result } = setup({ 0: [a], 1: [b], 2: [] });
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    let navigated = false;
    await act(async () => {
      navigated = await result.current.goUp();
    });
    expect(navigated).toBe(true);
    expect(result.current.selectedKey).toBe("1-1");
    expect(result.current.dirPath).toBe("/r/a");
    expect(result.current.dirNodes).toEqual([b]);
    expect(result.current.expandTo).toEqual([root.ref, a.ref]);
  });

  it("goUp does nothing when a newer selection overtook its path lookup", async () => {
    let release!: (n: DirectoryNode[]) => void;
    const { result, client } = setup({ 0: [a], 1: [b], 2: [] });
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    (client.path as ReturnType<typeof vi.fn>).mockImplementationOnce(() => new Promise((resolve) => (release = resolve)));
    let up!: Promise<boolean>;
    act(() => {
      up = result.current.goUp();
    });
    await act(() => result.current.selectNode(root.ref, root.fullPath));
    await act(async () => {
      release([root, a, b]);
      expect(await up).toBe(false);
    });
    expect(result.current.selectedKey).toBe("1-0");
  });
});

describe("navigation from refs a rescan made stale", () => {
  // The rescanned file adds /r/x, so the indexes shift: /r/a is now 2 and /r/a/b is 3.
  const x2 = node(1, 1, "x", "/r/x");
  const a2 = node(1, 2, "a", "/r/a");
  const b2 = node(1, 3, "b", "/r/a/b");
  const rescanned = {
    tree: { 0: [x2, a2], 1: [], 2: [b2], 3: [] } as Record<number, DirectoryNode[]>,
    chains: { 0: [root], 1: [root, x2], 2: [root, a2], 3: [root, a2, b2] } as Record<number, DirectoryNode[]>,
  };

  // Select /r/a/b in the old file, then swap the sidecar's answers to the rescanned file's.
  async function selectThenRescan() {
    const tree: Record<number, DirectoryNode[]> = { 0: [a], 1: [b], 2: [] };
    const liveChains: Record<number, DirectoryNode[]> = { ...chains };
    const client = fakeClient(tree, liveChains);
    const hook = renderHook(() => useCatalogSelection({ getClient: () => client, showDirectory: vi.fn() }));
    await act(() => hook.result.current.selectNode(b.ref, b.fullPath));
    for (const k of Object.keys(tree)) delete tree[Number(k)];
    Object.assign(tree, rescanned.tree);
    for (const k of Object.keys(liveChains)) delete liveChains[Number(k)];
    Object.assign(liveChains, rescanned.chains);
    return { ...hook, client };
  }

  it("goUp opens the parent of the folder on screen, found by its path", async () => {
    const { result } = await selectThenRescan();
    await act(() => result.current.goUp());
    expect(result.current.dirPath).toBe("/r/a");
    expect(result.current.selectedKey).toBe("1-2");
    expect(result.current.dirNodes).toEqual([b2]);
    expect(result.current.expandTo).toEqual([root.ref, a2.ref]);
  });

  it("activateDir opens the folder that was clicked, found by its path", async () => {
    const { result } = await selectThenRescan();
    await act(() => result.current.activateDir(b));
    expect(result.current.selectedKey).toBe("1-3");
    expect(result.current.dirPath).toBe("/r/a/b");
    expect(result.current.dirNodes).toEqual([]);
    expect(result.current.expandTo).toEqual([root.ref, a2.ref, b2.ref]);
  });

  it("activateDir on a folder the rescan removed opens its nearest surviving parent and says so", async () => {
    const { result } = await selectThenRescan();
    await act(() => result.current.activateDir(node(1, 2, "gone", "/r/a/gone")));
    expect(result.current.dirPath).toBe("/r/a");
    expect(result.current.selectedKey).toBe("1-2");
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Not found" }));
  });

  it("restoreAfterRescan finds the folder at its new index by path", async () => {
    const { result, client } = await selectThenRescan();
    await act(() => result.current.restoreAfterRescan(client, 1, { selectedKey: "1-2", dirPath: "/r/a/b" }));
    expect(result.current.selectedKey).toBe("1-3");
    expect(result.current.dirPath).toBe("/r/a/b");
    expect(result.current.expandTo).toEqual([root.ref, a2.ref, b2.ref]);
  });
});

describe("navigation errors", () => {
  it.each([
    ["goUp", (s: ReturnType<typeof useCatalogSelection>) => s.goUp()],
    ["activateDir", (s: ReturnType<typeof useCatalogSelection>) => s.activateDir(a)],
  ])("%s shows the error and keeps the selection when the sidecar call fails", async (_name, navigate) => {
    const { result, client } = setup();
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    (client.path as ReturnType<typeof vi.fn>).mockRejectedValue(new Error("sidecar went away"));
    await act(async () => {
      await navigate(result.current);
    });
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ color: "red", message: "sidecar went away" }));
    expect(result.current.selectedKey).toBe("1-2");
  });

  it("a failed listing fetch shows the error and does not leave the old folder's rows under the new path", async () => {
    const { result, client } = setup();
    await act(() => result.current.selectNode(a.ref, a.fullPath));
    (client.children as ReturnType<typeof vi.fn>).mockRejectedValue(new Error("listing failed"));
    let opened = true;
    await act(async () => {
      opened = await result.current.selectNode(root.ref, root.fullPath);
    });
    expect(opened).toBe(false);
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ color: "red", message: "listing failed" }));
    expect(result.current.dirPath).toBe("/r");
    expect(result.current.dirNodes).toEqual([]);
  });

  it("activateDir reports a listing fetch that fails after the folder was found", async () => {
    const { result, client } = setup();
    (client.children as ReturnType<typeof vi.fn>).mockRejectedValue(new Error("listing failed"));
    await act(() => result.current.activateDir(a));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ color: "red", message: "listing failed" }));
  });
});
