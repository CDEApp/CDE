// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { TreemapResponse } from "../api/types";
import { TreemapView } from "./TreemapView";

// The real chart paints to canvas; stand in a focusable host that reports a focused tile, as the
// library does when arrow keys move between points.
vi.mock("@tanstack/charts/react", async () => {
  const { useEffect } = await vi.importActual<typeof import("react")>("react");
  // Tiles too small for labels, so no text is measured.
  const tile = (id: string, row: number, x: number) => ({
    kind: "rect",
    x,
    y: 1,
    width: 6,
    height: 5,
    interaction: { point: { datum: { id, data: response.rows[row] } } },
  });
  return {
    Chart: ({
      onFocusChange,
      onRender,
    }: {
      onFocusChange?: (p: { datum: unknown } | null) => void;
      onRender?: (ctx: unknown) => void;
    }) => {
      useEffect(() => {
        onRender?.({
          interaction: { resolvePointer: () => null },
          scene: { width: 20, height: 10, nodes: [tile("d1", 1, 2), tile("f1", 2, 10)] },
        });
      }, [onRender]);
      return (
        <div
          data-testid="chart"
          tabIndex={0}
          onFocus={() => onFocusChange?.({ datum: { id: "d1", depth: 1, ancestorIds: ["root"], data: response.rows[1] } })}
          onBlur={() => onFocusChange?.(null)}
        />
      );
    },
  };
});

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

describe("TreemapView folder marks", () => {
  it("the legend ends folder names with the catalog's separator and leaves file names alone", async () => {
    setup();
    const legend = await screen.findByRole("list", { name: "Top-level folders" });
    await waitFor(() => expect(legend.querySelectorAll(".legend-name")).toHaveLength(2));
    const names = [...legend.querySelectorAll(".legend-name")].map((n) => n.textContent);
    expect(names).toEqual(["Users\\", "a.bin"]);
  });

  it("the tooltip ends a folder's path with the catalog's separator", async () => {
    setup();
    fireEvent.focus(await screen.findByTestId("chart"));
    await waitFor(() => expect(document.querySelector(".tt-path")?.textContent).toBe("C:\\Users\\"));
  });
});

describe("TreemapView hovered tile", () => {
  it("lightens the hovered or focused tile and gives it an inner shadow within its drawn bounds, and drops both when it leaves", async () => {
    setup();
    const chart = await screen.findByTestId("chart");
    await waitFor(() => expect(document.querySelector(".treemap-labels")).not.toBeNull());
    expect(document.querySelector(".treemap-hover")).toBeNull();

    fireEvent.focus(chart);
    const outline = await waitFor(() => {
      const o = document.querySelector(".treemap-hover");
      expect(o).not.toBeNull();
      return o!;
    });
    const rects = [...outline.querySelectorAll("rect")];
    expect(rects.length).toBeGreaterThan(0);
    const lighten = rects.find((r) => r.getAttribute("fill") === "#fff" && Number(r.getAttribute("fill-opacity")) > 0);
    expect(lighten, "the tile is lightened").toBeDefined();
    const shadow = rects.find((r) => r.getAttribute("filter")?.startsWith("url(#"));
    expect(shadow, "the tile has an inner shadow").toBeDefined();
    const refId = (attr: string) => shadow!.getAttribute(attr)!.slice("url(#".length, -1);
    expect(document.getElementById(refId("filter"))?.tagName.toLowerCase()).toBe("filter");
    expect(document.getElementById(refId("clip-path"))?.tagName.toLowerCase()).toBe("clippath");
    expect(document.querySelector("pattern"), "no cross-hatch").toBeNull();
    for (const r of rects) {
      expect(Number(r.getAttribute("x"))).toBeGreaterThanOrEqual(2);
      expect(Number(r.getAttribute("x")) + Number(r.getAttribute("width"))).toBeLessThanOrEqual(8);
    }

    fireEvent.blur(chart);
    await waitFor(() => expect(document.querySelector(".treemap-hover")).toBeNull());
  });
});
