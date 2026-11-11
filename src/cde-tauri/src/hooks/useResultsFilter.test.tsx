// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { SearchResultRow } from "../api/types";
import { useResultsFilter } from "./useResultsFilter";

const row = (n: number, name: string): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: n },
  name,
  size: 1,
  modified: "",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: "C:\\" + name,
  parentPath: "C:\\",
  catalogName: "cat",
});
const ROWS = [row(1, "alpha.txt"), row(2, "beta.txt"), row(3, "alpine.md")];

describe("useResultsFilter", () => {
  it("shows every row until text is typed, then only the matches", () => {
    const { result } = renderHook(() => useResultsFilter(ROWS, 0));
    expect(result.current.shown).toHaveLength(3);
    expect(result.current.active).toBe(false);
    act(() => result.current.setText("alp"));
    expect(result.current.shown.map((r) => r.name)).toEqual(["alpha.txt", "alpine.md"]);
    expect(result.current.total).toBe(3);
    expect(result.current.active).toBe(true);
  });

  it("an invalid regex reports an error and keeps the last valid filter applied", () => {
    const { result } = renderHook(() => useResultsFilter(ROWS, 0));
    act(() => {
      result.current.setMode("regex");
      result.current.setText("^a");
    });
    expect(result.current.shown).toHaveLength(2);
    act(() => result.current.setText("^a("));
    expect(result.current.error).toBeTruthy();
    expect(result.current.searchable).toBe(false);
    expect(result.current.shown).toHaveLength(2);
    act(() => result.current.setText("^b"));
    expect(result.current.error).toBeNull();
    expect(result.current.shown.map((r) => r.name)).toEqual(["beta.txt"]);
  });

  it("a new search clears the text but keeps the mode and field", () => {
    const { result, rerender } = renderHook(({ key }) => useResultsFilter(ROWS, key), { initialProps: { key: 0 } });
    act(() => {
      result.current.setMode("regex");
      result.current.setField("path");
      result.current.setText("alp");
    });
    expect(result.current.shown).toHaveLength(2);
    rerender({ key: 1 });
    expect(result.current.text).toBe("");
    expect(result.current.shown).toHaveLength(3);
    expect(result.current.mode).toBe("regex");
    expect(result.current.field).toBe("path");
  });
});
