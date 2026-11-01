// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { CdeApiClient, SearchHandlers } from "../api/client";
import type { SearchQuery, SearchResultRow } from "../api/types";
import type { UiStateManager } from "../state/uiState";
import { useSearch } from "./useSearch";

const query = { pattern: "a" } as SearchQuery;
const row = (catalogId: number, entryIndex = 1) =>
  ({ ref: { catalogId, entryIndex }, fullPath: `/c${catalogId}/f${entryIndex}` }) as SearchResultRow;
const nameOf = (id: number) => `cat${id}`;

function setup(search: (q: SearchQuery, h: SearchHandlers) => Promise<void>) {
  const client = { search: vi.fn(search) } as unknown as CdeApiClient;
  const ui = { pushSearchHistory: vi.fn(), update: vi.fn() } as unknown as UiStateManager;
  const onStart = vi.fn();
  const hook = renderHook(() => useSearch({ getClient: () => client, getUi: () => ui, onStart }));
  return { ...hook, client, ui, onStart };
}

describe("useSearch", () => {
  it("shows the stale banner once a catalog with results is rescanned, and clears it on the next search", async () => {
    const { result } = setup(async (_q, h) => {
      h.onResult(row(1));
      h.onDone?.(1);
    });
    await act(() => result.current.runSearch(query));
    expect(result.current.staleBanner(nameOf)).toBeNull();

    act(() => result.current.markRescanned(1));
    expect(result.current.staleBanner(nameOf)).toContain("cat1");

    await act(() => result.current.runSearch(query));
    expect(result.current.staleBanner(nameOf)).toBeNull();
  });

  it("does not flag a rescan of a catalog that has no results", async () => {
    const { result } = setup(async (_q, h) => {
      h.onResult(row(1));
      h.onDone?.(1);
    });
    await act(() => result.current.runSearch(query));
    act(() => result.current.markRescanned(2));
    expect(result.current.staleBanner(nameOf)).toBeNull();
  });

  it("switches to the results view, records history, and reports timing when done", async () => {
    const { result, ui, onStart } = setup(async (_q, h) => {
      h.onResult(row(1));
      h.onDone?.(1);
    });
    await act(() => result.current.runSearch(query));
    expect(onStart).toHaveBeenCalledOnce();
    expect(ui.pushSearchHistory).toHaveBeenCalledWith("a");
    expect(result.current.results).toHaveLength(1);
    expect(result.current.timing).toMatch(/^1 results in \d+ ms$/);
    expect(result.current.busy).toBe(false);
  });

  it("keeps the previous results when the query is refused before any result", async () => {
    let fail = false;
    const { result } = setup(async (_q, h) => {
      if (fail) throw new Error("bad query");
      h.onResult(row(1));
      h.onDone?.(1);
    });
    await act(() => result.current.runSearch(query));
    fail = true;
    await act(() => result.current.runSearch(query));
    expect(result.current.results).toHaveLength(1);
    expect(result.current.busy).toBe(false);
  });

  it("cancelSearch aborts the running search and busy is cleared on completion", async () => {
    let signal: AbortSignal | undefined;
    let finish!: () => void;
    const { result } = setup((_q, h) => {
      signal = h.signal;
      return new Promise<void>((resolve) => (finish = resolve));
    });
    let running!: Promise<void>;
    act(() => {
      running = result.current.runSearch(query);
    });
    expect(result.current.busy).toBe(true);
    act(() => result.current.cancelSearch());
    expect(signal?.aborted).toBe(true);
    await act(async () => {
      finish();
      await running;
    });
    expect(result.current.busy).toBe(false);
  });

  it("searchAgain re-runs the last query", async () => {
    const { result, client } = setup(async () => {});
    await act(() => result.current.searchAgain());
    expect(client.search).not.toHaveBeenCalled();
    await act(() => result.current.runSearch(query));
    await act(() => result.current.searchAgain());
    expect(client.search).toHaveBeenCalledTimes(2);
    expect((client.search as ReturnType<typeof vi.fn>).mock.calls[1][0]).toBe(query);
  });
});
