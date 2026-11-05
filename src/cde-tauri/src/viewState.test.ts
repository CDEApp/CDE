import { describe, expect, it } from "vitest";
import {
  dropTreemap,
  initialView,
  selectTreemap,
  setTreemapDepth,
  showTreemap,
  showView,
  type ViewState,
} from "./viewState";

const ref = { catalogId: 1, entryIndex: 7 };
const other = { catalogId: 2, entryIndex: 0 };

describe("viewState", () => {
  it("starts on Directory with no TreeMap tab and no chosen depth", () => {
    expect(initialView).toEqual({ kind: "directory", treemap: null, treemapDepth: null });
  });

  it("showTreemap opens the tab with nonce 1 and keeps the depth unset", () => {
    expect(showTreemap(initialView, ref)).toEqual({
      kind: "treemap",
      treemap: { ref, nonce: 1 },
      treemapDepth: null,
    });
  });

  it("mapping again bumps the nonce, even for the same folder, and keeps the chosen depth", () => {
    let s = showTreemap(initialView, ref);
    s = setTreemapDepth(s, 5);
    s = showTreemap(s, ref);
    expect(s.treemap?.nonce).toBe(2);
    expect(s.treemapDepth).toBe(5);
    expect(showTreemap(s, other).treemap?.ref).toBe(other);
  });

  it("the TreeMap tab survives switching to another view and back", () => {
    const away = showView(showTreemap(initialView, ref), "catalogs");
    expect(away.kind).toBe("catalogs");
    expect(away.treemap?.ref).toBe(ref);
    expect(showView(away, "directory").treemap).toEqual(away.treemap);
  });

  it("setTreemapDepth works from another view", () => {
    const away = showView(showTreemap(initialView, ref), "diff");
    expect(setTreemapDepth(away, 4).treemapDepth).toBe(4);
  });

  it("dropTreemap removes the tab and moves a treemap view to Directory", () => {
    expect(dropTreemap(showTreemap(initialView, ref))).toEqual({
      kind: "directory",
      treemap: null,
      treemapDepth: null,
    });
  });

  it("dropTreemap keeps any other active view", () => {
    const s: ViewState = showView(showTreemap(initialView, ref), "results");
    expect(dropTreemap(s)).toEqual({ kind: "results", treemap: null, treemapDepth: null });
  });

  it("the chosen depth survives dropTreemap and applies to the next Show TreeMap", () => {
    const chosen = setTreemapDepth(showTreemap(initialView, ref), 6);
    const dropped = dropTreemap(chosen);
    expect(dropped.treemapDepth).toBe(6);
    expect(showTreemap(dropped, other).treemapDepth).toBe(6);
  });

  it("selectTreemap returns to the tab from another view and is a no-op without one", () => {
    const away = showView(showTreemap(initialView, ref), "results");
    expect(selectTreemap(away).kind).toBe("treemap");
    expect(selectTreemap(away).treemap).toBe(away.treemap);
    expect(selectTreemap(initialView)).toBe(initialView);
  });
});
