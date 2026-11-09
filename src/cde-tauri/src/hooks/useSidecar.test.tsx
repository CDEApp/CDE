// @vitest-environment jsdom
import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { UiState } from "../state/uiState";
import { useSidecar } from "./useSidecar";

const h = vi.hoisted(() => {
  const state = {
    uiState: { catalogDir: "D:\\cats" } as Record<string, unknown>,
    putUiState: vi.fn(async (_s: Record<string, unknown>) => {}),
    kill: vi.fn(async () => {}),
    startSidecar: vi.fn(),
    closeHandler: null as null | ((e: { preventDefault: () => void }) => Promise<void>),
    destroy: vi.fn(async () => {}),
  };
  return state;
});

vi.mock("../sidecar", () => ({ startSidecar: h.startSidecar }));
vi.mock("../api/client", () => ({
  CdeApiClient: class {
    constructor(
      readonly baseUrl: string,
      readonly token: string,
    ) {}
    getUiState = async () => h.uiState;
    putUiState = h.putUiState;
  },
}));
vi.mock("@tauri-apps/api/window", () => ({
  getCurrentWindow: () => ({
    onCloseRequested: async (cb: typeof h.closeHandler) => {
      h.closeHandler = cb;
      return () => {};
    },
    destroy: h.destroy,
  }),
}));

const sidecar = { connection: { baseUrl: "http://127.0.0.1:5000", token: "t" }, kill: h.kill };

beforeEach(() => {
  h.startSidecar.mockReset().mockResolvedValue(sidecar);
  h.kill.mockClear();
  h.putUiState.mockReset().mockResolvedValue(undefined);
  h.destroy.mockClear();
  h.closeHandler = null;
});

describe("useSidecar", () => {
  it("is starting, with no client, until the sidecar is up and boot has run", async () => {
    let finishBoot!: () => void;
    const boot = vi.fn(() => new Promise<void>((resolve) => (finishBoot = resolve)));
    const { result } = renderHook(() => useSidecar({ boot }));
    expect(result.current.ready).toBe(false);
    expect(result.current.status).toBe("Starting…");
    expect(() => result.current.getClient()).toThrow("not ready");

    await waitFor(() => expect(boot).toHaveBeenCalledOnce());
    expect(boot).toHaveBeenCalledWith({ catalogDir: "D:\\cats" } satisfies UiState);
    expect(result.current.getClient()).toMatchObject({ baseUrl: "http://127.0.0.1:5000", token: "t" });
    expect(result.current.ready).toBe(false);

    await act(async () => finishBoot());
    expect(result.current.ready).toBe(true);
    expect(result.current.status).toBe("");
    expect(result.current.getUi()?.get()).toEqual({ catalogDir: "D:\\cats" });
  });

  it("a failing boot step reports the failure and never turns ready", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { result } = renderHook(() => useSidecar({ boot: async () => Promise.reject(new Error("no catalogs")) }));
    await waitFor(() => expect(result.current.status).toBe("Failed to start: no catalogs"));
    expect(result.current.ready).toBe(false);
  });

  it("a sidecar that does not start reports why", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    h.startSidecar.mockRejectedValueOnce("handshake timed out");
    const boot = vi.fn(async () => {});
    const { result } = renderHook(() => useSidecar({ boot }));
    await waitFor(() => expect(result.current.status).toBe("Failed to start: handshake timed out"));
    expect(boot).not.toHaveBeenCalled();
  });

  it("closing the window saves UI state, kills the sidecar, then closes", async () => {
    const { result } = renderHook(() => useSidecar({ boot: async () => {} }));
    await waitFor(() => expect(result.current.ready).toBe(true));
    const preventDefault = vi.fn();
    await act(() => h.closeHandler!({ preventDefault }));
    expect(preventDefault).toHaveBeenCalled();
    expect(h.putUiState).toHaveBeenCalledWith({ catalogDir: "D:\\cats" });
    expect(h.kill).toHaveBeenCalledOnce();
    expect(h.destroy).toHaveBeenCalledOnce();
  });

  it("a failed save does not stop the close", async () => {
    const { result } = renderHook(() => useSidecar({ boot: async () => {} }));
    await waitFor(() => expect(result.current.ready).toBe(true));
    h.putUiState.mockRejectedValueOnce(new Error("sidecar gone"));
    await act(() => h.closeHandler!({ preventDefault: () => {} }));
    expect(h.kill).toHaveBeenCalledOnce();
    expect(h.destroy).toHaveBeenCalledOnce();
  });

  it("a sidecar that comes up after unmount is killed, and boot never runs", async () => {
    let up!: (s: typeof sidecar) => void;
    h.startSidecar.mockReturnValueOnce(new Promise((resolve) => (up = resolve)));
    const boot = vi.fn(async () => {});
    const { unmount } = renderHook(() => useSidecar({ boot }));
    unmount();
    await act(async () => up(sidecar));
    expect(h.kill).toHaveBeenCalledOnce();
    expect(boot).not.toHaveBeenCalled();
  });
});
