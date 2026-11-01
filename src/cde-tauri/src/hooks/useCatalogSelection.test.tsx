// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { CatalogsResponse, DirectoryNode, EntryRef } from "../api/types";
import { parseRef, refsToPath, useCatalogSelection } from "./useCatalogSelection";

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

  it("refsToPath returns the root-to-entry ref chain", async () => {
    const client = fakeClient({}, chains);
    expect(await refsToPath(client, b.ref)).toEqual([root.ref, a.ref, b.ref]);
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
    // After the rescan /r/a exists but /r/a/b is gone.
    const { result, client } = setup({ 0: [a], 1: [] });
    await act(() => result.current.selectNode(b.ref, b.fullPath));
    const snapshot = result.current.snapshot();

    await act(() => result.current.restoreAfterRescan(client, 1, snapshot));
    expect(result.current.selectedKey).toBe("1-1");
    expect(result.current.dirPath).toBe("/r/a");
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
});
