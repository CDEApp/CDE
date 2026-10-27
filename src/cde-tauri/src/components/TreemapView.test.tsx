// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { TreemapResponse } from "../api/types";
import { TreemapView } from "./TreemapView";

// The real chart paints to canvas; stand in a focusable host that reports a focused tile, as the
// library does when arrow keys move between points.
vi.mock("@tanstack/charts/react", () => ({
  Chart: ({ onFocusChange }: { onFocusChange?: (p: { datum: unknown } | null) => void }) => (
    <div
      data-testid="chart"
      tabIndex={0}
      onFocus={() => onFocusChange?.({ datum: { id: "d1", depth: 1, ancestorIds: ["root"], data: {} } })}
    />
  ),
}));

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
  Object.defineProperty(HTMLElement.prototype, "clientHeight", { configurable: true, value: 400 });
  Object.defineProperty(HTMLElement.prototype, "clientWidth", { configurable: true, value: 600 });
});
afterEach(cleanup);

const rootRef = { catalogId: 1, entryIndex: 0 };
const dirRef = { catalogId: 1, entryIndex: 5 };
const response: TreemapResponse = {
  root: rootRef,
  rootFullPath: "C:\\",
  rootSize: 100,
  truncated: false,
  rows: [
    { nodeId: "root", parentId: null, name: "C:", size: 100, value: 0, isDirectory: true, mergedCount: 0, ref: rootRef },
    { nodeId: "d1", parentId: "root", name: "Users", size: 60, value: 60, isDirectory: true, mergedCount: 0, ref: dirRef },
    { nodeId: "f1", parentId: "root", name: "a.bin", size: 40, value: 40, isDirectory: false, mergedCount: 0, ref: null },
  ],
} as TreemapResponse;

function setup() {
  const treemap = vi.fn().mockResolvedValue(response);
  const client = { treemap, path: vi.fn().mockResolvedValue([]) } as unknown as CdeApiClient;
  render(
    <TreemapView client={client} request={{ ref: rootRef, nonce: 0 }} depth={2} onDepthChange={() => {}} onStatus={() => {}} onTileMenu={() => {}} />,
  );
  return { treemap };
}

describe("TreemapView tile keyboard support", () => {
  it.each(["Enter", " "])("%j on the focused tile drills into its folder", async (key) => {
    const { treemap } = setup();
    const chart = await screen.findByTestId("chart");
    await waitFor(() => expect(treemap).toHaveBeenCalledTimes(1));
    fireEvent.focus(chart);
    fireEvent.keyDown(chart, { key });
    await waitFor(() => expect(treemap).toHaveBeenCalledTimes(2));
    expect(treemap.mock.calls[1][0]).toEqual(dirRef);
  });

  it("other keys do nothing", async () => {
    const { treemap } = setup();
    const chart = await screen.findByTestId("chart");
    fireEvent.focus(chart);
    fireEvent.keyDown(chart, { key: "a" });
    expect(treemap).toHaveBeenCalledTimes(1);
  });
});
