import { describe, expect, it, vi } from "vitest";
import { canFitAnyText, createTextMeasurer, sameTileLayout, sceneLeaves, type SceneLeaf } from "./treemapModel";

const leaf = (id: string, x0: number, y0: number, x1: number, y1: number, row: unknown = { id }): SceneLeaf => ({
  id,
  row: row as never,
  rect: { x0, y0, x1, y1 },
});

describe("sceneLeaves", () => {
  const rect = (id: string, x: number, y: number, width: number, height: number) => ({
    kind: "rect",
    x,
    y,
    width,
    height,
    interaction: { point: { datum: { id, data: { nodeId: id } } } },
  });

  it("reads each leaf's drawn bounds and row from the rendered scene, through groups", () => {
    const nodes = [
      { kind: "group", children: [rect("a", 1, 2, 30, 40), { kind: "label" }, rect("b", 50, 60, 5, 6)] },
      { kind: "rect", x: 0, y: 0, width: 9, height: 9 }, // no interaction point: ignored
    ];
    const leaves = sceneLeaves(nodes as never);
    expect(leaves.map((l) => l.id)).toEqual(["a", "b"]);
    expect(leaves[0].rect).toEqual({ x0: 1, y0: 2, x1: 31, y1: 42 });
    expect(leaves[0].row).toEqual({ nodeId: "a" });
  });

  it("applies group translation", () => {
    const nodes = [{ kind: "group", translateX: 10, translateY: 20, children: [rect("a", 1, 2, 3, 4)] }];
    expect(sceneLeaves(nodes as never)[0].rect).toEqual({ x0: 11, y0: 22, x1: 14, y1: 26 });
  });
});

describe("sameTileLayout", () => {
  const a = { width: 100, height: 50, leaves: [leaf("a", 0, 0, 10, 10)] };

  it("is true for identical geometry and the same row objects", () => {
    const row = a.leaves[0].row;
    expect(sameTileLayout(a, { ...a, leaves: [leaf("a", 0, 0, 10, 10, row)] })).toBe(true);
  });

  it("is false when a rectangle, the size, the count or the row changes", () => {
    const row = a.leaves[0].row;
    expect(sameTileLayout(a, { ...a, leaves: [leaf("a", 0, 0, 11, 10, row)] })).toBe(false);
    expect(sameTileLayout(a, { ...a, width: 101, leaves: [leaf("a", 0, 0, 10, 10, row)] })).toBe(false);
    expect(sameTileLayout(a, { ...a, leaves: [] })).toBe(false);
    expect(sameTileLayout(a, a)).toBe(true);
    expect(sameTileLayout(a, { ...a, leaves: [leaf("a", 0, 0, 10, 10, { other: 1 })] })).toBe(false);
  });
});

describe("canFitAnyText", () => {
  const r = (w: number, h: number) => ({ x0: 0, y0: 0, x1: w, y1: h });
  it("needs width above 2*padding and height of at least 2*padding + lineHeight", () => {
    expect(canFitAnyText(r(9, 19), 11, 4)).toBe(true);
    expect(canFitAnyText(r(8, 19), 11, 4)).toBe(false);
    expect(canFitAnyText(r(50, 18.9), 11, 4)).toBe(false);
    expect(canFitAnyText(r(50, 19), 11, 4)).toBe(true);
    expect(canFitAnyText(r(0, 0), 11, 4)).toBe(false);
  });
});

describe("createTextMeasurer", () => {
  it("caches widths per font, text and size", () => {
    const raw = vi.fn((_font: string, text: string) => text.length * 6);
    const measure = createTextMeasurer(raw);
    expect(measure("12px x", "abc")).toBe(18);
    expect(measure("12px x", "abc")).toBe(18);
    expect(raw).toHaveBeenCalledTimes(1);
    measure("10px x", "abc");
    measure("12px x", "abcd");
    expect(raw).toHaveBeenCalledTimes(3);
  });
});
