// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CdeApiClient, ReloadHandlers } from "../api/client";
import type { CatalogInfo, CatalogsResponse } from "../api/types";
import type { UiState, UiStateManager } from "../state/uiState";
import { useCatalogs } from "./useCatalogs";

const { open } = vi.hoisted(() => ({ open: vi.fn() }));
vi.mock("@tauri-apps/plugin-dialog", () => ({ open }));

const info = (catalogId: number, rootPath: string) =>
  ({ catalogId, rootPath, rootSize: 10, actualFileName: `c${catalogId}.cdex`, defaultFileName: "" }) as unknown as CatalogInfo;
const response = (catalogDir: string, ...catalogs: CatalogInfo[]) =>
  ({ catalogs, catalogDir, legacyCatalogs: [] }) as unknown as CatalogsResponse;

function fakeUi(initial: UiState = {}) {
  let state: UiState = { ...initial };
  return {
    get: () => state,
    update: (patch: Partial<UiState>) => {
      state = { ...state, ...patch };
    },
  } as unknown as UiStateManager;
}

function setup(blocked = false) {
  const order: string[] = [];
  const client = {
    reload: vi.fn(async (handlers: ReloadHandlers, _path?: string) => {
      order.push("reload");
      handlers.onProgress?.({ message: "Loading c1.cdex" } as never);
    }),
    catalogs: vi.fn(async () => response("D:\\cats", info(1, "C:\\"), info(2, "E:\\"))),
    setDescription: vi.fn(async (_id: number, _d: string) => {}),
  };
  const ui = fakeUi();
  const opts = {
    getClient: () => client as unknown as CdeApiClient,
    getUi: () => ui,
    setStatus: vi.fn(),
    isBlocked: () => blocked,
    onBeforeReload: vi.fn(() => order.push("beforeReload")),
    onLoaded: vi.fn(),
  };
  const hook = renderHook(() => useCatalogs(opts));
  return { ...hook, client, ui, opts, order };
}

const generations = (roots: { generation?: number }[]) => roots.map((r) => r.generation);

beforeEach(() => open.mockReset());

describe("useCatalogs", () => {
  it("the first load uses the saved folder, shows progress, and lists the catalogs as tree roots", async () => {
    const { result, client, opts } = setup();
    await act(() => result.current.loadInitial("D:\\cats"));
    expect(result.current.catalogDir).toBe("D:\\cats");
    expect(client.reload).toHaveBeenCalledWith(expect.anything(), "D:\\cats");
    expect(opts.setStatus).toHaveBeenCalledWith("Loading c1.cdex");
    expect(result.current.roots.map((r) => [r.label, r.ref])).toEqual([
      ["C:\\", { catalogId: 1, entryIndex: 0 }],
      ["E:\\", { catalogId: 2, entryIndex: 0 }],
    ]);
    expect(opts.onLoaded).toHaveBeenCalledWith(result.current.catalogs);
  });

  it("reload resets the old load's state first, then remounts every tree root", async () => {
    const { result, opts, order } = setup();
    await act(() => result.current.loadInitial(undefined));
    expect(generations(result.current.roots)).toEqual([0, 0]);
    await act(() => result.current.reload("D:\\cats"));
    expect(order).toEqual(["reload", "beforeReload", "reload"]);
    expect(generations(result.current.roots)).toEqual([1, 1]);
    expect(opts.setStatus).toHaveBeenLastCalledWith("");
    expect(opts.onLoaded).toHaveBeenCalledTimes(2);
  });

  it("reload does nothing while jobs block it", async () => {
    const { result, client, opts } = setup(true);
    await act(() => result.current.reload("D:\\cats"));
    expect(client.reload).not.toHaveBeenCalled();
    expect(opts.onBeforeReload).not.toHaveBeenCalled();
  });

  it("a failed reload says so and still re-reads the catalog list", async () => {
    const { result, client, opts } = setup();
    client.reload.mockRejectedValueOnce(new Error("folder gone"));
    await act(() => result.current.reload("D:\\cats"));
    expect(opts.setStatus).toHaveBeenLastCalledWith("Reload failed: folder gone");
    expect(client.catalogs).toHaveBeenCalledOnce();
    expect(opts.onLoaded).not.toHaveBeenCalled();
  });

  it("remountRoot remounts only that catalog's tree root on the next refresh", async () => {
    const { result, client } = setup();
    await act(() => result.current.loadInitial(undefined));
    act(() => result.current.remountRoot(2));
    await act(() => result.current.refreshCatalogs(client as unknown as CdeApiClient));
    expect(generations(result.current.roots)).toEqual([0, 1]);
  });

  it("Open Folder loads the picked folder and remembers it", async () => {
    open.mockResolvedValueOnce("F:\\more");
    const { result, client, ui } = setup();
    let opened = false;
    await act(async () => {
      opened = await result.current.openFolder();
    });
    expect(opened).toBe(true);
    expect(result.current.catalogDir).toBe("F:\\more");
    expect(ui.get().catalogDir).toBe("F:\\more");
    expect(client.reload).toHaveBeenCalledWith(expect.anything(), "F:\\more");
  });

  it("cancelling Open Folder loads nothing, and while blocked it does not even ask", async () => {
    open.mockResolvedValueOnce(null);
    const { result, client } = setup();
    expect(await act(() => result.current.openFolder())).toBe(false);
    expect(client.reload).not.toHaveBeenCalled();

    const blocked = setup(true);
    expect(await act(() => blocked.result.current.openFolder())).toBe(false);
    expect(open).toHaveBeenCalledOnce();
  });

  it("saveDescription saves through the sidecar, then refreshes the list", async () => {
    const { result, client } = setup();
    await act(() => result.current.saveDescription(2, "Backup drive"));
    expect(client.setDescription).toHaveBeenCalledWith(2, "Backup drive");
    expect(client.catalogs).toHaveBeenCalledOnce();
  });
});
