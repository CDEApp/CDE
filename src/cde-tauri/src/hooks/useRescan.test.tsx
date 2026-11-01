// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RescanFailure, type CdeApiClient, type RescanHandlers } from "../api/client";
import type { CatalogsResponse, RescanResult } from "../api/types";
import { useRescan } from "./useRescan";

const { show } = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock("@mantine/notifications", () => ({ notifications: { show } }));

const result = { catalogId: 1, rootPath: "/r", entries: 5, savedPath: "/r.cdex" } as RescanResult;
const catalogs = {
  catalogs: [{ catalogId: 1, rootPath: "/r", scanEndUtcTicks: 1, dirEntryCount: 1, fileEntryCount: 1 }],
} as unknown as CatalogsResponse;

function setup(rescan: (id: number, h: RescanHandlers) => Promise<RescanResult | null>) {
  const client = { rescan: vi.fn(rescan), catalogs: vi.fn(async () => catalogs) } as unknown as CdeApiClient;
  const afterSwap = vi.fn(async () => {});
  const hook = renderHook(() => useRescan({ getClient: () => client, getCatalogs: () => catalogs, afterSwap }));
  return { ...hook, client, afterSwap };
}

beforeEach(() => show.mockClear());

describe("useRescan", () => {
  it("tracks a running rescan, then clears the state and the cancel handle when it completes", async () => {
    let signal: AbortSignal | undefined;
    let finish!: (r: RescanResult | null) => void;
    const { result: r, afterSwap } = setup((_id, h) => {
      signal = h.signal;
      return new Promise((resolve) => (finish = resolve));
    });

    let running!: Promise<void>;
    act(() => {
      running = r.current.rescanCatalog(1);
    });
    expect(r.current.rescans.has(1)).toBe(true);
    expect(r.current.rescansRef.current.has(1)).toBe(true);

    await act(async () => {
      finish(result);
      await running;
    });
    expect(afterSwap).toHaveBeenCalledOnce();
    expect(r.current.rescans.has(1)).toBe(false);

    act(() => r.current.cancelRescan(1)); // the handle is gone: nothing to abort
    expect(signal?.aborted).toBe(false);
  });

  it("cancelRescan aborts the running rescan, which is reported as cancelled and leaves the view alone", async () => {
    let signal: AbortSignal | undefined;
    const { result: r, afterSwap } = setup(async (_id, h) => {
      signal = h.signal;
      return null; // the client resolves null on abort
    });
    let running!: Promise<void>;
    act(() => {
      running = r.current.rescanCatalog(1);
      r.current.cancelRescan(1);
    });
    await act(() => running);
    expect(signal?.aborted).toBe(true);
    expect(afterSwap).not.toHaveBeenCalled();
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Rescan cancelled" }));
    expect(r.current.rescans.size).toBe(0);
  });

  it("ignores a second request for a catalog that is already rescanning", async () => {
    let finish!: (r: RescanResult | null) => void;
    const { result: r, client } = setup(() => new Promise((resolve) => (finish = resolve)));
    let first!: Promise<void>;
    act(() => {
      first = r.current.rescanCatalog(1);
      void r.current.rescanCatalog(1);
    });
    expect(client.rescan).toHaveBeenCalledOnce();
    await act(async () => {
      finish(result);
      await first;
    });
  });

  it("raises the unreachable-root modal for an inaccessible root and skips the refresh", async () => {
    const { result: r, afterSwap } = setup(async () => {
      throw new RescanFailure({ code: "root-not-accessible", message: "no", rootPath: "/gone" } as never);
    });
    await act(() => r.current.rescanCatalog(1));
    expect(r.current.unreachableRoot).toBe("/gone");
    expect(afterSwap).not.toHaveBeenCalled();
    expect(r.current.rescans.size).toBe(0);
    act(() => r.current.clearUnreachable());
    expect(r.current.unreachableRoot).toBeNull();
  });

  it("reports a failed view refresh separately from a failed rescan", async () => {
    const { result: r, afterSwap } = setup(async () => result);
    afterSwap.mockRejectedValueOnce(new Error("boom"));
    await act(() => r.current.rescanCatalog(1));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ title: "Rescan complete, view not refreshed" }));
  });
});
