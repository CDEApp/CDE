import { describe, expect, it } from "vitest";
import { viewPaneClass } from "./viewPane";

describe("viewPaneClass", () => {
  it("is just the base class while the view is shown", () => {
    expect(viewPaneClass("directory", "directory")).toBe("view-pane");
  });

  it("adds the hidden class (visibility, not display:none) for any other active view", () => {
    for (const active of ["results", "catalogs", "colours", "treemap"] as const) {
      expect(viewPaneClass("directory", active)).toBe("view-pane view-pane-hidden");
    }
  });
});
