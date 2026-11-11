import { describe, expect, it } from "vitest";
import {
  EMPTY_SELECTION,
  selectedInOrder,
  selectionReducer,
  type RowSelection,
  type SelectionAction,
} from "./rowSelection";

const order = ["a", "b", "c", "d", "e"];
const run = (actions: SelectionAction[], from: RowSelection = EMPTY_SELECTION) =>
  actions.reduce(selectionReducer, from);
const click = (key: string, mode: "replace" | "toggle" | "range" = "replace"): SelectionAction => ({
  type: "click",
  key,
  mode,
  order,
});
const move = (delta: number, extend = false): SelectionAction => ({ type: "move", to: { delta }, extend, order });
const keys = (s: RowSelection) => [...s.selected].sort();

describe("selectionReducer", () => {
  it("a plain click replaces the selection and sets active and anchor", () => {
    const s = run([click("b"), click("d")]);
    expect(keys(s)).toEqual(["d"]);
    expect(s.active).toBe("d");
    expect(s.anchor).toBe("d");
  });

  it("a toggle click adds and removes a row without touching the others", () => {
    const s = run([click("a"), click("c", "toggle")]);
    expect(keys(s)).toEqual(["a", "c"]);
    expect(keys(run([click("a", "toggle")], s))).toEqual(["c"]);
  });

  it("a range click selects from the anchor to the row in either direction and keeps the anchor", () => {
    const down = run([click("b"), click("d", "range")]);
    expect(keys(down)).toEqual(["b", "c", "d"]);
    expect(down.anchor).toBe("b");
    expect(keys(run([click("d"), click("b", "range")]))).toEqual(["b", "c", "d"]);
    expect(keys(run([click("b"), click("e", "range"), click("c", "range")]))).toEqual(["b", "c"]);
  });

  it("a range click with no usable anchor acts like a plain click", () => {
    expect(keys(run([click("c", "range")]))).toEqual(["c"]);
  });

  it("moving activates and selects a single row, clamped at both ends", () => {
    expect(run([move(1)]).active).toBe("a");
    const s = run([click("b"), move(1)]);
    expect(keys(s)).toEqual(["c"]);
    expect(run([click("e"), move(1)]).active).toBe("e");
    expect(run([click("a"), move(-1)]).active).toBe("a");
    expect(run([move(-1)]).active).toBe("e");
  });

  it("Home and End jump to the ends, and extending selects the span from the anchor", () => {
    expect(run([click("c"), { type: "move", to: "end", extend: false, order }]).active).toBe("e");
    const s = run([click("c"), { type: "move", to: "start", extend: true, order }]);
    expect(keys(s)).toEqual(["a", "b", "c"]);
    expect(s.anchor).toBe("c");
  });

  it("extending with the arrows grows and shrinks around the anchor", () => {
    const grown = run([click("b"), move(1, true), move(1, true)]);
    expect(keys(grown)).toEqual(["b", "c", "d"]);
    expect(keys(run([move(-1, true)], grown))).toEqual(["b", "c"]);
  });

  it("moving over an empty list changes nothing", () => {
    const s = run([click("a")]);
    expect(selectionReducer(s, { type: "move", to: { delta: 1 }, extend: false, order: [] })).toBe(s);
  });

  it("select all takes every visible key and keeps the active row", () => {
    const s = run([click("c"), { type: "selectAll", order }]);
    expect(keys(s)).toEqual(order);
    expect(s.active).toBe("c");
  });

  it("Space toggles the active row, and does nothing without one", () => {
    const on = run([click("a"), click("b", "toggle"), { type: "toggleActive" }]);
    expect(keys(on)).toEqual(["a"]);
    expect(selectionReducer(EMPTY_SELECTION, { type: "toggleActive" })).toBe(EMPTY_SELECTION);
  });

  it("clear empties everything, and returns the same state when already empty", () => {
    expect(run([click("a"), { type: "clear" }])).toBe(EMPTY_SELECTION);
    expect(selectionReducer(EMPTY_SELECTION, { type: "clear" })).toBe(EMPTY_SELECTION);
  });
});

describe("selectedInOrder", () => {
  it("returns the selected items in the order given, not the order they were selected", () => {
    const sel = run([click("d"), click("a", "toggle"), click("c", "toggle")]);
    expect(selectedInOrder(["e", "c", "a", "b", "d"], (x) => x, sel)).toEqual(["c", "a", "d"]);
  });

  it("returns nothing for an empty selection", () => {
    expect(selectedInOrder(order, (x) => x, EMPTY_SELECTION)).toEqual([]);
  });
});
