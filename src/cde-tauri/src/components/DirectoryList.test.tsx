// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { DEFAULT_SORT } from "../directorySort";
import { DirectoryList } from "./DirectoryList";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(cleanup);

function setup(columnWidths?: unknown) {
  const onSort = vi.fn();
  const onColumnWidthsChange = vi.fn();
  render(
    <DirectoryList
      path="C:\\"
      nodes={[]}
      sort={DEFAULT_SORT}
      onSort={onSort}
      onActivate={vi.fn()}
      onContextMenu={vi.fn()}
      resolveStyle={() => undefined}
      columnWidths={columnWidths}
      onColumnWidthsChange={onColumnWidthsChange}
    />,
  );
  return { onSort, onColumnWidthsChange };
}

const sizeResizer = () => screen.getAllByRole("slider")[1];
const header = () => screen.getAllByRole("row")[0];

describe("DirectoryList column resize", () => {
  it("starts from the default grid template", () => {
    setup();
    expect(header().style.gridTemplateColumns).toBe("minmax(200px, 1fr) 100px 160px");
  });

  it("applies saved widths and ignores invalid ones", () => {
    setup({ name: 300, size: "wide", modified: 1, bogus: 5 });
    expect(header().style.gridTemplateColumns).toBe("300px 100px 90px");
  });

  it("resizes by dragging, persists on release, and does not sort", () => {
    const { onSort, onColumnWidthsChange } = setup();
    const resizer = sizeResizer();
    // jsdom has no layout: report the Size cell as its default 100px wide.
    resizer.parentElement!.getBoundingClientRect = () => ({ width: 100 }) as DOMRect;
    fireEvent.mouseDown(resizer, { button: 0, clientX: 500 });
    fireEvent.mouseMove(window, { clientX: 540 });
    expect(header().style.gridTemplateColumns).toBe("minmax(200px, 1fr) 140px 160px");
    expect(onColumnWidthsChange).not.toHaveBeenCalled(); // only on release
    fireEvent.mouseUp(window, { clientX: 540 });
    fireEvent.click(resizer);
    expect(onColumnWidthsChange).toHaveBeenCalledWith({ size: 140 });
    expect(onSort).not.toHaveBeenCalled();
  });

  it("will not drag a column below its minimum", () => {
    setup();
    const resizer = sizeResizer();
    resizer.parentElement!.getBoundingClientRect = () => ({ width: 100 }) as DOMRect;
    fireEvent.mouseDown(resizer, { button: 0, clientX: 500 });
    fireEvent.mouseMove(window, { clientX: 0 });
    expect(header().style.gridTemplateColumns).toBe("minmax(200px, 1fr) 60px 160px");
    fireEvent.mouseUp(window);
  });

  it("double-click resets a column to its default", () => {
    const { onColumnWidthsChange } = setup({ name: 300, size: 140 });
    fireEvent.doubleClick(screen.getAllByRole("slider")[0]);
    expect(onColumnWidthsChange).toHaveBeenCalledWith({ size: 140 });
    expect(header().style.gridTemplateColumns).toBe("minmax(200px, 1fr) 140px 160px");
  });

  it("resizes from the keyboard and resets with Home", () => {
    const { onColumnWidthsChange } = setup();
    const resizer = sizeResizer();
    fireEvent.keyDown(resizer, { key: "ArrowRight" });
    expect(onColumnWidthsChange).toHaveBeenLastCalledWith({ size: 110 });
    fireEvent.keyDown(resizer, { key: "ArrowLeft", shiftKey: true });
    expect(onColumnWidthsChange).toHaveBeenLastCalledWith({ size: 60 });
    fireEvent.keyDown(resizer, { key: "Home" });
    expect(onColumnWidthsChange).toHaveBeenLastCalledWith({});
  });

  it("still sorts when a header is clicked", () => {
    const { onSort } = setup();
    fireEvent.click(screen.getByRole("button", { name: /Size/ }));
    expect(onSort).toHaveBeenCalledWith("size");
  });
});
