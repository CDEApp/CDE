import { describe, expect, it } from "vitest";
import { scrollTopFor, visibleRange } from "./visibleRange";

describe("scrollTopFor", () => {
  const base = { rowHeight: 20, viewport: 100 };

  it("nearest: leaves the scroll alone when the row is already fully visible", () => {
    expect(scrollTopFor({ ...base, index: 52, scrollTop: 1000, align: "nearest" })).toBe(1000);
  });

  it("nearest: scrolls just enough to show a row above or below the viewport", () => {
    expect(scrollTopFor({ ...base, index: 10, scrollTop: 1000, align: "nearest" })).toBe(200); // row top at the top
    expect(scrollTopFor({ ...base, index: 60, scrollTop: 1000, align: "nearest" })).toBe(1120); // row bottom at the bottom
    expect(scrollTopFor({ ...base, index: 54, scrollTop: 1010, align: "nearest" })).toBe(1010); // last full row
    expect(scrollTopFor({ ...base, index: 50, scrollTop: 1010, align: "nearest" })).toBe(1000); // half hidden at top
  });

  it("center: puts the row in the middle, never above the top of the list", () => {
    expect(scrollTopFor({ ...base, index: 100, scrollTop: 0, align: "center" })).toBe(1960);
    expect(scrollTopFor({ ...base, index: 1, scrollTop: 500, align: "center" })).toBe(0);
  });
});

describe("visibleRange", () => {
  it("mounts the rows in the viewport plus overscan, not the whole list", () => {
    // 20px rows, 100px viewport scrolled to row 50 => rows 50..54 visible, +/- 8 overscan.
    expect(visibleRange({ scrollTop: 1000, viewport: 100, rowHeight: 20, total: 16159, overscan: 8 })).toEqual({
      start: 42,
      end: 63,
    });
  });

  it("clamps at the top and bottom of the list", () => {
    expect(visibleRange({ scrollTop: 0, viewport: 100, rowHeight: 20, total: 1000, overscan: 8 })).toEqual({
      start: 0,
      end: 13,
    });
    expect(visibleRange({ scrollTop: 19900, viewport: 100, rowHeight: 20, total: 1000, overscan: 8 })).toEqual({
      start: 987,
      end: 1000,
    });
  });

  it("covers a partly visible row at either edge", () => {
    // scrollTop 30 => row 1 half visible at the top; bottom edge 130 => row 6 partly visible.
    expect(visibleRange({ scrollTop: 30, viewport: 100, rowHeight: 20, total: 100, overscan: 0 })).toEqual({
      start: 1,
      end: 7,
    });
  });

  it("is empty for an empty list", () => {
    expect(visibleRange({ scrollTop: 0, viewport: 100, rowHeight: 20, total: 0, overscan: 8 })).toEqual({
      start: 0,
      end: 0,
    });
  });
});
