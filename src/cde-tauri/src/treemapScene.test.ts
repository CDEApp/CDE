import { describe, expect, it } from "vitest";
import { createChartScene, defineChart } from "@tanstack/charts";
import { treemap } from "@tanstack/charts/hierarchy/treemap";
import { sceneLeaves } from "./treemapModel";

// Guards the assumption behind the tile-size labels: the rendered scene exposes each leaf's final
// drawn rectangle (after paddingInner/paddingOuter and the mark's inset), tagged with its row.
const rows = [
  { id: "root", parent: null, value: 0 },
  { id: "a", parent: "root", value: 0 },
  { id: "a1", parent: "a", value: 50 },
  { id: "a2", parent: "a", value: 30 },
  { id: "b", parent: "root", value: 70 },
  { id: "c", parent: "root", value: 10 },
];
const PADDING_INNER = 1;
const INSET = 0.5;

function build() {
  return defineChart({
    marks: [
      treemap(rows, {
        nodeId: "id",
        parentId: "parent",
        value: "value",
        paddingOuter: 2,
        paddingInner: PADDING_INNER,
        inset: INSET,
      }),
    ],
    scales: { x: null, y: null },
    guides: false,
    margin: 0,
    tooltip: false,
  });
}

function drawnRects(nodes: readonly any[], out: any[] = []): any[] {
  for (const n of nodes) {
    if (n.kind === "group") drawnRects(n.children, out);
    else if (n.kind === "rect") out.push(n);
  }
  return out;
}

describe("scene leaves", () => {
  it.each([
    [400, 300],
    [1000, 640],
  ])("expose the exact drawn extents and rows at %ix%i", (width, height) => {
    const scene = createChartScene(build() as never, { width, height });
    const leaves = sceneLeaves(scene.nodes as never);
    const drawn = drawnRects(scene.nodes as never);

    expect(leaves.map((l) => l.id).sort()).toEqual(["a1", "a2", "b", "c"]);
    expect(leaves.length).toBe(drawn.length);
    // Extents equal the rect nodes the renderer paints (x, y, width, height), not just centres.
    leaves.forEach((l, i) => {
      expect(l.rect.x0).toBe(drawn[i].x);
      expect(l.rect.y0).toBe(drawn[i].y);
      expect(l.rect.x1 - l.rect.x0).toBeCloseTo(drawn[i].width, 9);
      expect(l.rect.y1 - l.rect.y0).toBeCloseTo(drawn[i].height, 9);
      expect(l.row).toMatchObject({ id: l.id }); // the source row travels with the tile
    });
    // Drawn tiles stay inside the chart and are separated by at least the paddings and insets.
    for (const l of leaves) {
      expect(l.rect.x0).toBeGreaterThanOrEqual(0);
      expect(l.rect.y0).toBeGreaterThanOrEqual(0);
      expect(l.rect.x1).toBeLessThanOrEqual(width);
      expect(l.rect.y1).toBeLessThanOrEqual(height);
    }
    const gap = PADDING_INNER + 2 * INSET - 1e-6;
    for (const p of leaves) {
      for (const q of leaves) {
        if (p === q) continue;
        const sepX = Math.max(q.rect.x0 - p.rect.x1, p.rect.x0 - q.rect.x1);
        const sepY = Math.max(q.rect.y0 - p.rect.y1, p.rect.y0 - q.rect.y1);
        expect(Math.max(sepX, sepY)).toBeGreaterThanOrEqual(Math.min(gap, 2 * INSET) - 1e-6);
      }
    }
  });

  it("re-lays out when the chart size changes", () => {
    const def = build();
    const small = sceneLeaves(createChartScene(def as never, { width: 400, height: 300 }).nodes as never);
    const big = sceneLeaves(createChartScene(def as never, { width: 800, height: 300 }).nodes as never);
    const b = (ls: typeof small) => ls.find((l) => l.id === "b")!.rect;
    expect(b(big).x1).not.toBe(b(small).x1);
  });
});
