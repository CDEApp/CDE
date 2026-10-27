import { describe, expect, it } from "vitest";
import { createChartScene, defineChart } from "@tanstack/charts";
import { treemap } from "@tanstack/charts/hierarchy/treemap";
import { treemapSquarify } from "d3-hierarchy";
import { capturingTiler } from "./treemapModel";

// Guards the assumption behind the tile-size labels: rectangles captured by wrapping the squarify
// tiler are in the same coordinate space as the points the chart renders, at every size.
const rows = [
  { id: "root", parent: null, value: 0 },
  { id: "a", parent: "root", value: 0 },
  { id: "a1", parent: "a", value: 50 },
  { id: "a2", parent: "a", value: 30 },
  { id: "b", parent: "root", value: 70 },
  { id: "c", parent: "root", value: 10 },
];
const PHI = (1 + Math.sqrt(5)) / 2;

function build() {
  const capture = capturingTiler<any>(treemapSquarify.ratio(PHI) as never, (n) => String(n.data.id));
  const definition = defineChart({
    marks: [
      treemap(rows, {
        nodeId: "id",
        parentId: "parent",
        value: "value",
        method: capture.tile as never,
        paddingOuter: 2,
        paddingInner: 1,
        inset: 0.5,
      }),
    ],
    scales: { x: null, y: null },
    guides: false,
    margin: 0,
    tooltip: false,
  });
  return { capture, definition };
}

describe("tile rectangle capture", () => {
  it.each([
    [400, 300],
    [1000, 640],
  ])("matches the rendered chart's coordinates at %ix%i", (width, height) => {
    const { capture, definition } = build();
    const scene = createChartScene(definition as never, { width, height });
    const rects = capture.current();
    expect([...rects.keys()].sort()).toEqual(["a1", "a2", "b", "c"]);
    expect(scene.points.length).toBe(rects.size);
    for (const p of scene.points) {
      const r = rects.get(String((p.datum as { id: string }).id))!;
      expect(p.x).toBeCloseTo((r.x0 + r.x1) / 2, 6);
      expect(p.y).toBeCloseTo((r.y0 + r.y1) / 2, 6);
    }
  });

  it("produces a new generation and new rectangles on re-layout at another size", () => {
    const { capture, definition } = build();
    createChartScene(definition as never, { width: 400, height: 300 });
    const g1 = capture.generation();
    const before = capture.current().get("b")!;
    createChartScene(definition as never, { width: 800, height: 300 });
    expect(capture.generation()).toBeGreaterThan(g1);
    expect(capture.current().get("b")!.x1).not.toBe(before.x1);
  });
});
