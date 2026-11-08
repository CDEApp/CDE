// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { UiState, UiStateManager } from "../state/uiState";
import { useNewCatalog } from "./useNewCatalog";

const { open } = vi.hoisted(() => ({ open: vi.fn() }));
vi.mock("@tauri-apps/plugin-dialog", () => ({ open }));

function fakeUi(initial: UiState = {}) {
  let state: UiState = { ...initial };
  return {
    get: () => state,
    update: (patch: Partial<UiState>) => {
      state = { ...state, ...patch };
    },
  } as unknown as UiStateManager;
}

function setup(ui = fakeUi()) {
  return { ...renderHook(() => useNewCatalog({ getUi: () => ui })), ui };
}

beforeEach(() => open.mockReset());

describe("useNewCatalog", () => {
  it("picking a folder opens the dialog for it and remembers it for next time", async () => {
    open.mockResolvedValueOnce("D:\\Photos");
    const { result, ui } = setup();
    expect(result.current.root).toBeNull();
    await act(() => result.current.pick());
    expect(result.current.root).toBe("D:\\Photos");
    expect(ui.get().lastNewCatalogRoot).toBe("D:\\Photos");
  });

  it("the picker starts at the last root used", async () => {
    open.mockResolvedValueOnce(null);
    const { result } = setup(fakeUi({ lastNewCatalogRoot: "E:\\Music" }));
    await act(() => result.current.pick());
    expect(open).toHaveBeenCalledWith(expect.objectContaining({ directory: true, defaultPath: "E:\\Music" }));
  });

  it("cancelling the picker keeps the root already chosen", async () => {
    open.mockResolvedValueOnce("D:\\Photos").mockResolvedValueOnce(null);
    const { result, ui } = setup();
    await act(() => result.current.pick());
    await act(() => result.current.pick());
    expect(result.current.root).toBe("D:\\Photos");
    expect(ui.get().lastNewCatalogRoot).toBe("D:\\Photos");
  });

  it("close clears the root", async () => {
    open.mockResolvedValueOnce("D:\\Photos");
    const { result } = setup();
    await act(() => result.current.pick());
    act(() => result.current.close());
    expect(result.current.root).toBeNull();
  });
});
