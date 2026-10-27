import { describe, expect, it } from "vitest";
import {
  BRANCH_PALETTE,
  MERGED_COLOR,
  branchColors,
  branchIdOf,
  capturingTiler,
  drillTargetId,
  fullPathOf,
  joinPath,
  labelLines,
  LABEL_PADDING,
  minFractionFor,
  mix,
  tileColor,
} from "./treemapModel";

describe("minFractionFor", () => {
  it("is the minimum tile area as a fraction of the chart area", () => {
    expect(minFractionFor(1000, 600)).toBeCloseTo(36 / 600_000);
    expect(minFractionFor(100, 100, 10)).toBeCloseTo(0.01);
  });

  it("falls back to a 1000x600 area before the chart is measured", () => {
    expect(minFractionFor(0, 0)).toBe(minFractionFor(1000, 600));
  });

  it("never exceeds one half", () => {
    expect(minFractionFor(2, 2)).toBe(0.5);
  });
});

describe("joinPath", () => {
  it.each([
    ["", "a", "a"],
    ["C:\\", "Users", "C:\\Users"],
    ["C:\\Users", "jason", "C:\\Users\\jason"],
    ["/mnt/", "data", "/mnt/data"],
  ])("joinPath(%j, %j) -> %j", (base, name, expected) => {
    expect(joinPath(base, name)).toBe(expected);
  });
});

describe("branchIdOf", () => {
  it("is null for the mapped root", () => {
    expect(branchIdOf({ id: "r", depth: 0, ancestorIds: [] })).toBeNull();
  });

  it("is the node itself at depth 1", () => {
    expect(branchIdOf({ id: "a", depth: 1, ancestorIds: ["r"] })).toBe("a");
  });

  it("is the depth-1 ancestor for deeper nodes", () => {
    expect(branchIdOf({ id: "c", depth: 3, ancestorIds: ["r", "a", "b"] })).toBe("a");
  });
});

describe("branchColors", () => {
  it("gives the largest branch the first palette colour", () => {
    const colors = branchColors([
      { nodeId: "small", size: 10, mergedCount: 0 },
      { nodeId: "big", size: 100, mergedCount: 0 },
    ]);
    expect(colors.get("big")).toBe(BRANCH_PALETTE[0]);
    expect(colors.get("small")).toBe(BRANCH_PALETTE[1]);
  });

  it("colours merged rows grey without using up a palette colour", () => {
    const colors = branchColors([
      { nodeId: "merged", size: 1000, mergedCount: 5 },
      { nodeId: "a", size: 10, mergedCount: 0 },
    ]);
    expect(colors.get("merged")).toBe(MERGED_COLOR);
    expect(colors.get("a")).toBe(BRANCH_PALETTE[0]);
  });

  it("cycles the palette past its length", () => {
    const branches = Array.from({ length: BRANCH_PALETTE.length + 1 }, (_, i) => ({
      nodeId: `b${i}`,
      size: 100 - i,
      mergedCount: 0,
    }));
    expect(branchColors(branches).get(`b${BRANCH_PALETTE.length}`)).toBe(BRANCH_PALETTE[0]);
  });
});

describe("mix", () => {
  it("blends toward the target by the given amount", () => {
    expect(mix("#000000", "#ffffff", 0)).toBe("#000000");
    expect(mix("#000000", "#ffffff", 0.5)).toBe("#808080");
    expect(mix("#000000", "#ffffff", 1)).toBe("#ffffff");
  });

  it("clamps the amount to 0..1", () => {
    expect(mix("#102030", "#ffffff", -1)).toBe("#102030");
    expect(mix("#102030", "#ffffff", 2)).toBe("#ffffff");
  });
});

describe("tileColor", () => {
  it("leaves depth-1 tiles at the branch colour", () => {
    expect(tileColor("#5b9bd5", 1)).toBe("#5b9bd5");
  });

  it("lightens each deeper level", () => {
    expect(tileColor("#000000", 2)).toBe(mix("#000000", "#ffffff", 0.14));
    expect(tileColor("#000000", 3)).toBe(mix("#000000", "#ffffff", 0.28));
  });
});

describe("drillTargetId", () => {
  const rows = new Map<string, { isDirectory: boolean; ref: unknown }>([
    ["dir", { isDirectory: true, ref: { catalogId: 1, entryIndex: 5 } }],
    ["file", { isDirectory: false, ref: { catalogId: 1, entryIndex: 6 } }],
    ["merged", { isDirectory: true, ref: null }],
  ]);

  it("drills into the top-level directory branch of a deep node", () => {
    expect(drillTargetId({ id: "x", depth: 3, ancestorIds: ["r", "dir", "y"] }, rows)).toBe("dir");
  });

  it("goes nowhere for the root, files, merged rows and unknown ids", () => {
    expect(drillTargetId({ id: "r", depth: 0, ancestorIds: [] }, rows)).toBeNull();
    expect(drillTargetId({ id: "file", depth: 1, ancestorIds: ["r"] }, rows)).toBeNull();
    expect(drillTargetId({ id: "merged", depth: 1, ancestorIds: ["r"] }, rows)).toBeNull();
    expect(drillTargetId({ id: "nope", depth: 1, ancestorIds: ["r"] }, rows)).toBeNull();
  });
});

describe("fullPathOf", () => {
  const rows = new Map<string, { parentId: string | null; name: string }>([
    ["r", { parentId: null, name: "root" }],
    ["a", { parentId: "r", name: "Users" }],
    ["b", { parentId: "a", name: "jason" }],
  ]);

  it("joins row names under the mapped root path", () => {
    expect(fullPathOf("b", rows, "C:\\")).toBe("C:\\Users\\jason");
    expect(fullPathOf("a", rows, "D:\\data")).toBe("D:\\data\\Users");
  });

  it("is the root path for the root itself or an unknown id", () => {
    expect(fullPathOf("r", rows, "C:\\")).toBe("C:\\");
    expect(fullPathOf("missing", rows, "C:\\")).toBe("C:\\");
  });
});

describe("labelLines", () => {
  const rect = (w: number, h: number) => ({ x0: 10, y0: 20, x1: 10 + w, y1: 20 + h });
  const P = 4.5;

  it("shows both lines when the name and size fit with two lines of height", () => {
    expect(labelLines(rect(100, 40), 60, 30, 11, P)).toBe("both");
  });

  it("fits exactly at the boundaries (inclusive)", () => {
    // available = 2*P less than the rect: width 60 + 9 = 69, height 22 + 9 = 31
    expect(labelLines(rect(69, 31), 60, 60, 11, P)).toBe("both");
    expect(labelLines(rect(68.9, 31), 60, 60, 11, P)).toBe("none");
    expect(labelLines(rect(69, 30.9), 60, 60, 11, P)).toBe("name");
  });

  it("falls back to the name when the size is too wide", () => {
    expect(labelLines(rect(80, 40), 70, 75, 11, P)).toBe("name");
  });

  it("falls back to the name when there is only room for one line", () => {
    expect(labelLines(rect(100, 20), 60, 30, 11, P)).toBe("name");
  });

  it("shows nothing when the name does not fit", () => {
    expect(labelLines(rect(50, 40), 60, 30, 11, P)).toBe("none");
    expect(labelLines(rect(100, 15), 60, 30, 11, P)).toBe("none");
  });

  it("shows nothing for a tile smaller than the padding", () => {
    expect(labelLines(rect(8, 8), 0, 0, 11, P)).toBe("none");
  });

  it("shows nothing for zero or negative rectangles", () => {
    expect(labelLines(rect(0, 0), 10, 10, 11, P)).toBe("none");
    expect(labelLines(rect(-5, 40), 10, 10, 11, P)).toBe("none");
    expect(labelLines(rect(100, -5), 10, 10, 11, P)).toBe("none");
  });

  it("matches the library's inset 0.5 + labelPadding 4", () => {
    expect(LABEL_PADDING).toBe(4.5);
  });
});

describe("capturingTiler", () => {
  type N = { id: string; parent?: N | null; children?: N[]; x0: number; y0: number; x1: number; y1: number };
  const n = (id: string, children?: N[]): N => {
    const node: N = { id, children, x0: 0, y0: 0, x1: 0, y1: 0 };
    children?.forEach((c) => (c.parent = node));
    return node;
  };
  // A fake tiler that splits the area into equal vertical strips.
  const strips = (node: N, x0: number, y0: number, x1: number, y1: number) => {
    const kids = node.children ?? [];
    kids.forEach((c, i) => {
      c.x0 = x0 + ((x1 - x0) * i) / kids.length;
      c.x1 = x0 + ((x1 - x0) * (i + 1)) / kids.length;
      c.y0 = y0;
      c.y1 = y1;
    });
  };
  const idOf = (node: N) => node.id;

  it("delegates to the base tiler and records leaf rectangles only", () => {
    const sink = capturingTiler<N>(strips, idOf);
    const dir = n("dir", [n("a"), n("b")]);
    const root = n("root", [dir, n("c")]);
    sink.tile(root, 0, 0, 100, 50);
    sink.tile(dir, 0, 0, 50, 50);
    expect(sink.current().get("c")).toEqual({ x0: 50, y0: 0, x1: 100, y1: 50 });
    expect(sink.current().get("a")).toEqual({ x0: 0, y0: 0, x1: 25, y1: 50 });
    expect(sink.current().has("dir")).toBe(false);
  });

  it("starts a fresh capture each time the root is tiled again (re-layout)", () => {
    const sink = capturingTiler<N>(strips, idOf);
    const root = n("root", [n("a"), n("b")]);
    sink.tile(root, 0, 0, 100, 50);
    const first = sink.current();
    sink.tile(root, 0, 0, 200, 50);
    expect(sink.current()).not.toBe(first);
    expect(first.get("b")?.x1).toBe(100);
    expect(sink.current().get("b")?.x1).toBe(200);
    expect(sink.generation()).toBe(2);
  });
});
