// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, renderHook, screen, within } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { CdeApiClient } from "./api/client";
import { ResultsPane } from "./components/ResultsPane";
import { useSearch } from "./hooks/useSearch";
import { EMPTY_SELECTION } from "./rowSelection";
import type { UiStateManager } from "./state/uiState";

vi.mock("@tauri-apps/plugin-dialog", () => ({ save: vi.fn() }));
vi.mock("@tauri-apps/plugin-fs", () => ({ writeFile: vi.fn() }));
vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const resultRow = {
  ref: { catalogId: 1, entryIndex: 1 },
  name: "a.txt",
  size: 1,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: "C:\\a.txt",
  parentPath: "C:\\",
  catalogName: "c",
};

const sseBody = (limitReached: boolean) =>
  [
    ["result", resultRow],
    ["progress", { count: 5, total: 5, elapsed: "00:00:00", limitReached }],
    ["done", { count: 1 }],
  ]
    .map(([e, d]) => `event: ${e as string}\ndata: ${JSON.stringify(d)}\n\n`)
    .join("");

describe("limit reached, from the stream to the list", () => {
  it("the final progress flag becomes the notice and the sorted-header hint, and a clean search clears them", async () => {
    let limitReached = true;
    vi.stubGlobal("fetch", vi.fn(async () => new Response(sseBody(limitReached), { status: 200 })));
    const client = new CdeApiClient("http://127.0.0.1:5000", "tok");
    const ui = { pushSearchHistory: vi.fn(), update: vi.fn() } as unknown as UiStateManager;
    const { result } = renderHook(() => useSearch({ getClient: () => client, getUi: () => ui, onStart: vi.fn(), wantsDuplicateCounts: () => false }));

    const show = () =>
      render(
        <ResultsPane
          rows={result.current.results}
          filter={{
            text: "", setText: vi.fn(), mode: "substring", setMode: vi.fn(), field: "name", setField: vi.fn(),
            shown: result.current.results, total: result.current.results.length, active: false, error: null, searchable: false,
          }}
          canSearchWithFilter={false}
          onSearchWithFilter={vi.fn()}
          getVisibleColumns={() => undefined}
          onVisibleColumnsChange={vi.fn()}
          busy={result.current.busy}
          staleBanner={null}
          onSearchAgain={vi.fn()}
          limitReachedCount={result.current.limitReachedCount}
          onRaiseLimit={() => void result.current.raiseLimit()}
          resolveStyle={() => undefined}
          getColumnOrder={() => undefined}
          onColumnOrderChange={vi.fn()}
          onActivate={vi.fn()}
          onContextMenu={vi.fn()}
          rowSelection={{ selection: EMPTY_SELECTION, dispatch: vi.fn() }}
        />,
      );
    const sizeButton = () => within(screen.getByRole("columnheader", { name: /Size/ })).getByRole("button");

    await act(() => result.current.runSearch({ pattern: "a", limitResultCount: 1 } as never));
    const first = show();
    expect(screen.getByRole("status").textContent).toContain("1 results (limit reached, more exist)");
    fireEvent.click(sizeButton());
    expect(sizeButton().title).toBe("sorted within the first 1 matches");

    first.unmount();
    limitReached = false;
    await act(() => result.current.runSearch({ pattern: "a", limitResultCount: 1 } as never));
    show();
    expect(screen.queryByText(/limit reached/)).toBeNull();
    expect(screen.queryByRole("button", { name: "Raise limit" })).toBeNull();
  });
});
