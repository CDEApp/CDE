import { describe, expect, it } from "vitest";
import {
  clampWidth,
  gridTemplate,
  isFillActive,
  MAX_COLUMN_WIDTH,
  normalizeColumnWidths,
  totalWidth,
} from "./columnWidths";

describe("normalizeColumnWidths", () => {
  it("keeps valid widths", () => {
    expect(normalizeColumnWidths({ name: 300, size: 120, modified: 200 })).toEqual({ name: 300, size: 120, modified: 200 });
  });

  it("drops unknown ids and invalid values", () => {
    expect(normalizeColumnWidths({ name: "wide", size: NaN, modified: -5, path: 400, extra: 1 })).toEqual({});
    expect(normalizeColumnWidths({ size: Infinity })).toEqual({});
  });

  it("clamps to the minimum and maximum", () => {
    expect(normalizeColumnWidths({ size: 1, modified: 99999 })).toEqual({ size: 60, modified: MAX_COLUMN_WIDTH });
  });

  it("rounds fractional widths", () => {
    expect(normalizeColumnWidths({ size: 120.6 })).toEqual({ size: 121 });
  });

  it.each([null, undefined, 5, "x", [1, 2]])("treats %j as no saved widths", (saved) => {
    expect(normalizeColumnWidths(saved)).toEqual({});
  });
});

describe("clampWidth", () => {
  it("never goes below a column's minimum", () => {
    expect(clampWidth("name", 0)).toBe(80);
    expect(clampWidth("size", -10)).toBe(60);
  });
});

describe("gridTemplate", () => {
  it("lets Name fill spare width until it is resized", () => {
    expect(isFillActive({})).toBe(true);
    expect(gridTemplate({})).toBe("minmax(200px, 1fr) 100px 160px");
  });

  it("pins Name once the user sets a width", () => {
    expect(isFillActive({ name: 250 })).toBe(false);
    expect(gridTemplate({ name: 250, size: 90 })).toBe("250px 90px 160px");
  });
});

describe("totalWidth", () => {
  it("sums defaults and overrides", () => {
    expect(totalWidth({})).toBe(460);
    expect(totalWidth({ size: 140 })).toBe(500);
  });
});
