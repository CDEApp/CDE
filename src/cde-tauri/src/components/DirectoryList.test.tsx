// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { DirectoryNode } from "../api/types";
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

const sizeResizer = () => screen.getAllByRole("separator")[1];
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
    fireEvent.doubleClick(screen.getAllByRole("separator")[0]);
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

describe("DirectoryList parent row", () => {
  const entry = (entryIndex: number, name: string, isDirectory: boolean, size: number) =>
    ({
      ref: { catalogId: 1, entryIndex },
      name,
      fullPath: `/r/a/${name}`,
      isDirectory,
      hasChildren: false,
      size,
      modified: "2024-01-01T00:00:00",
      isModifiedBad: false,
      isReparsePoint: false,
    }) as DirectoryNode;
  const nodes = [entry(2, "sub", true, 10), entry(3, "z.txt", false, 99), entry(4, "aaa", true, 5)];

  function renderList(onUp?: () => void, sort = DEFAULT_SORT) {
    const onActivate = vi.fn();
    const onContextMenu = vi.fn();
    render(
      <DirectoryList
        path="/r/a"
        nodes={nodes}
        sort={sort}
        onSort={vi.fn()}
        onActivate={onActivate}
        onContextMenu={onContextMenu}
        onUp={onUp}
        resolveStyle={() => undefined}
        onColumnWidthsChange={vi.fn()}
      />,
    );
    return { onActivate, onContextMenu };
  }

  const rowNames = () => [...document.querySelectorAll(".vlist-row")].map((r) => r.firstElementChild?.textContent);
  const rowNamed = (name: string) =>
    [...document.querySelectorAll<HTMLElement>(".vlist-row")].find((r) => r.firstElementChild?.textContent === name)!;

  it("is absent when there is no parent to go to", () => {
    renderList();
    expect(rowNames()).toEqual(["aaa", "sub", "z.txt"]);
  });

  it("comes first whatever the sort", () => {
    renderList(vi.fn(), { column: "name", direction: "desc" });
    expect(rowNames()).toEqual(["..", "sub", "aaa", "z.txt"]);
  });

  it.each([
    ["double-click", (el: HTMLElement) => fireEvent.doubleClick(el)],
    ["Enter", (el: HTMLElement) => fireEvent.keyDown(el, { key: "Enter" })],
  ])("%s goes up", (_how, act) => {
    const onUp = vi.fn();
    const { onActivate } = renderList(onUp);
    act(rowNamed(".."));
    expect(onUp).toHaveBeenCalledOnce();
    expect(onActivate).not.toHaveBeenCalled();
  });

  it("has no context menu", () => {
    const { onContextMenu } = renderList(vi.fn());
    fireEvent.contextMenu(rowNamed(".."));
    expect(onContextMenu).not.toHaveBeenCalled();
  });

  it("Enter on an entry row activates it", () => {
    const { onActivate } = renderList(vi.fn());
    fireEvent.keyDown(rowNamed("sub"), { key: "Enter" });
    expect(onActivate).toHaveBeenCalledWith(nodes[0]);
  });
});
