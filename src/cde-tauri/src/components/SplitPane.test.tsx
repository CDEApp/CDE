// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SplitPane } from "./SplitPane";

afterEach(cleanup);

function setup(props: Partial<React.ComponentProps<typeof SplitPane>> = {}) {
  const onResizeEnd = vi.fn();
  render(<SplitPane left="L" right="R" initialLeftWidth={300} minLeft={150} minRight={200} onResizeEnd={onResizeEnd} {...props} />);
  return { onResizeEnd, sep: screen.getByRole("separator") };
}

describe("SplitPane splitter keyboard support", () => {
  it("is a focusable vertical separator exposing its width", () => {
    const { sep } = setup();
    expect(sep.getAttribute("aria-orientation")).toBe("vertical");
    expect(sep.tabIndex).toBe(0);
    expect(sep.getAttribute("aria-valuenow")).toBe("300");
    expect(sep.getAttribute("aria-valuemin")).toBe("150");
  });

  it("ArrowRight widens and ArrowLeft narrows the left pane, reporting each change", () => {
    const { sep, onResizeEnd } = setup();
    fireEvent.keyDown(sep, { key: "ArrowRight" });
    expect(sep.getAttribute("aria-valuenow")).toBe("316");
    expect(onResizeEnd).toHaveBeenLastCalledWith(316);
    fireEvent.keyDown(sep, { key: "ArrowLeft" });
    fireEvent.keyDown(sep, { key: "ArrowLeft" });
    expect(sep.getAttribute("aria-valuenow")).toBe("284");
    expect(onResizeEnd).toHaveBeenLastCalledWith(284);
  });

  it("Shift uses a larger step", () => {
    const { sep } = setup();
    fireEvent.keyDown(sep, { key: "ArrowRight", shiftKey: true });
    expect(sep.getAttribute("aria-valuenow")).toBe("364");
  });

  it("never goes below the minimum, and Home jumps to it", () => {
    const { sep } = setup({ initialLeftWidth: 155 });
    fireEvent.keyDown(sep, { key: "ArrowLeft" });
    expect(sep.getAttribute("aria-valuenow")).toBe("150");
    fireEvent.keyDown(sep, { key: "ArrowRight" });
    fireEvent.keyDown(sep, { key: "Home" });
    expect(sep.getAttribute("aria-valuenow")).toBe("150");
  });

  it("ignores other keys", () => {
    const { sep, onResizeEnd } = setup();
    fireEvent.keyDown(sep, { key: "a" });
    expect(onResizeEnd).not.toHaveBeenCalled();
  });
});
