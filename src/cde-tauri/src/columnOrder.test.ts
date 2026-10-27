import { describe, expect, it } from "vitest";
import { moveColumn, normalizeColumnOrder } from "./columnOrder";

const DEFAULTS = ["name", "size", "modified", "path"] as const;

describe("normalizeColumnOrder", () => {
  it("returns the default order when nothing valid was saved", () => {
    expect(normalizeColumnOrder(undefined, DEFAULTS)).toEqual([...DEFAULTS]);
    expect(normalizeColumnOrder("name,size", DEFAULTS)).toEqual([...DEFAULTS]);
    expect(normalizeColumnOrder([], DEFAULTS)).toEqual([...DEFAULTS]);
  });

  it("keeps a valid saved order", () => {
    expect(normalizeColumnOrder(["path", "modified", "size", "name"], DEFAULTS)).toEqual([
      "path",
      "modified",
      "size",
      "name",
    ]);
  });

  it("drops unknown, duplicate and non-string ids", () => {
    expect(normalizeColumnOrder(["size", "gone", "size", 7, "name"], DEFAULTS)).toEqual([
      "size",
      "name",
      "modified",
      "path",
    ]);
  });

  it("appends columns missing from the saved order in default order", () => {
    expect(normalizeColumnOrder(["path"], DEFAULTS)).toEqual(["path", "name", "size", "modified"]);
  });
});

describe("moveColumn", () => {
  const order = ["a", "b", "c"] as const;

  it("moves a column right to the insertion gap", () => {
    expect(moveColumn(order, "a", 2)).toEqual(["b", "a", "c"]);
    expect(moveColumn(order, "a", 3)).toEqual(["b", "c", "a"]);
  });

  it("moves a column left to the insertion gap", () => {
    expect(moveColumn(order, "c", 0)).toEqual(["c", "a", "b"]);
    expect(moveColumn(order, "c", 1)).toEqual(["a", "c", "b"]);
  });

  it("clamps an out-of-range target", () => {
    expect(moveColumn(order, "a", 99)).toEqual(["b", "c", "a"]);
    expect(moveColumn(order, "c", -5)).toEqual(["c", "a", "b"]);
  });

  it("returns the same array for a no-op move or an unknown id", () => {
    expect(moveColumn(order, "b", 1)).toBe(order);
    expect(moveColumn(order, "b", 2)).toBe(order);
    expect(moveColumn(order, "zzz", 0)).toBe(order);
  });
});
