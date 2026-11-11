// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { SearchResultRow } from "../api/types";
import { EMPTY_SELECTION } from "../rowSelection";
import { ResultsList } from "./ResultsList";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(cleanup);

const mk = (name: string, over: Partial<SearchResultRow> = {}): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: name.length * 1000 + name.charCodeAt(0) },
  name,
  size: 1,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: `C:\${name}`,
  parentPath: "C:\\",
  catalogName: "cat",
  ...over,
});

const ROWS = [
  mk("b.TXT"),
  mk("a.Zip"),
  mk("docs", { isDirectory: true }),
  mk("c.doc"),
  mk("link", { isDirectory: true, isReparsePoint: true }),
  mk("d.txt"),
  mk("sub", { isDirectory: true }),
];

function setup(props: { columnOrder?: unknown; visibleColumns?: unknown } = {}) {
  const onVisibleColumnsChange = vi.fn();
  const onColumnOrderChange = vi.fn();
  render(
    <ResultsList
      rows={ROWS}
      onActivate={vi.fn()}
      onContextMenu={vi.fn()}
      onColumnOrderChange={onColumnOrderChange}
      onVisibleColumnsChange={onVisibleColumnsChange}
      resolveStyle={() => undefined}
      selection={EMPTY_SELECTION}
      dispatchSelection={vi.fn()}
      {...props}
    />,
  );
  return { onVisibleColumnsChange, onColumnOrderChange };
}

const headers = () => screen.getAllByRole("columnheader").map((h) => within(h).getByRole("button").textContent?.replace(/ [▲▼]$/, ""));
const openMenu = () => fireEvent.contextMenu(screen.getAllByRole("columnheader")[0]);
const menuItem = (name: string) => screen.getByRole("menuitemcheckbox", { name: new RegExp(name) });
const rowCells = (column: string) => {
  const index = headers().indexOf(column);
  return screen.getAllByRole("row").slice(1).map((r) => within(r).getAllByRole("gridcell")[index].textContent);
};
const sortBy = (column: string) => fireEvent.click(screen.getByRole("button", { name: new RegExp(`^${column}`) }));

describe("column chooser", () => {
  it("shows the five default columns, unchanged, when nothing was saved", () => {
    setup();
    expect(headers()).toEqual(["Name", "Size", "Modified", "Catalog", "Path"]);
  });

  it("lists every column with a check on the shown ones", () => {
    setup();
    openMenu();
    const checked = screen.getAllByRole("menuitemcheckbox").map((i) => [i.textContent?.replace("✓", ""), i.getAttribute("aria-checked")]);
    expect(checked).toEqual([
      ["Name", "true"], ["Size", "true"], ["Modified", "true"], ["Catalog", "true"], ["Path", "true"],
      ["Extension", "false"], ["Type", "false"], ["Dupes", "false"], ["Hashed", "false"],
    ]);
  });

  it("hides a column and reports the shown ids", () => {
    const { onVisibleColumnsChange } = setup();
    openMenu();
    fireEvent.click(menuItem("Catalog"));
    expect(headers()).toEqual(["Name", "Size", "Modified", "Path"]);
    expect(onVisibleColumnsChange).toHaveBeenLastCalledWith(["name", "size", "modified", "path"]);
    expect(within(screen.getAllByRole("row")[1]).getAllByRole("gridcell")).toHaveLength(4);
  });

  it("shows a hidden column again, in its saved position", () => {
    const { onVisibleColumnsChange } = setup();
    openMenu();
    fireEvent.click(menuItem("Extension"));
    expect(headers()).toEqual(["Name", "Size", "Modified", "Catalog", "Path", "Extension"]);
    expect(onVisibleColumnsChange).toHaveBeenLastCalledWith(["name", "size", "modified", "catalog", "path", "extension"]);
    openMenu();
    fireEvent.click(menuItem("Extension"));
    expect(headers()).toEqual(["Name", "Size", "Modified", "Catalog", "Path"]);
  });

  it("does not let the last shown column be hidden", () => {
    setup({ visibleColumns: ["size"] });
    openMenu();
    expect(menuItem("Size").getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(menuItem("Size"));
    expect(headers()).toEqual(["Size"]);
  });

  it("restores a saved visible set and order", () => {
    setup({ columnOrder: ["type", "path", "name", "size", "modified", "catalog", "extension"], visibleColumns: ["name", "path", "type"] });
    expect(headers()).toEqual(["Type", "Path", "Name"]);
  });

  it("loads an old saved order (five ids) with the default columns and the new ones hidden", () => {
    setup({ columnOrder: ["path", "catalog", "modified", "size", "name"] });
    expect(headers()).toEqual(["Path", "Catalog", "Modified", "Size", "Name"]);
  });

  it("falls back to the defaults when the saved visible set holds no known column", () => {
    setup({ visibleColumns: ["gone", 3] });
    expect(headers()).toEqual(["Name", "Size", "Modified", "Catalog", "Path"]);
  });
});

describe("Extension and Type columns", () => {
  it("shows the extension without the dot, blank for folders", () => {
    setup({ visibleColumns: ["name", "extension"] });
    const byName = Object.fromEntries(rowCells("Name").map((n, i) => [n, rowCells("Extension")[i]]));
    expect(byName).toEqual({ "b.TXT": "TXT", "a.Zip": "Zip", docs: "", "c.doc": "doc", link: "", "d.txt": "txt", sub: "" });
  });

  it("sorts Extension case-insensitively", () => {
    setup({ visibleColumns: ["name", "extension"] });
    sortBy("Extension");
    expect(rowCells("Extension").filter(Boolean)).toEqual(["doc", "TXT", "txt", "Zip"]);
    expect(rowCells("Name").filter((n) => n?.endsWith("TXT") || n?.endsWith("txt"))).toEqual(["b.TXT", "d.txt"]);
  });

  it("sorts Type with the folders together", () => {
    setup({ visibleColumns: ["name", "type"] });
    sortBy("Type");
    expect(rowCells("Type")).toEqual(["file", "file", "file", "file", "folder", "folder", "link"]);
    expect(rowCells("Name").slice(4, 6).sort()).toEqual(["docs", "sub"]);
    sortBy("Type");
    expect(rowCells("Type")).toEqual(["link", "folder", "folder", "file", "file", "file", "file"]);
  });
});

describe("column chooser from the keyboard", () => {
  it("focuses the first column, moves with the arrows, toggles with Enter and Space, and returns focus on close", () => {
    const { onVisibleColumnsChange } = setup();
    const nameButton = within(screen.getAllByRole("columnheader")[0]).getByRole("button");
    nameButton.focus();
    openMenu();
    expect(document.activeElement).toBe(menuItem("Name"));

    const key = (k: string) => fireEvent.keyDown(document.activeElement!, { key: k });
    key("ArrowDown");
    key("ArrowDown");
    key("ArrowDown");
    expect(document.activeElement).toBe(menuItem("Catalog"));
    key("Enter");
    expect(headers()).toEqual(["Name", "Size", "Modified", "Path"]);
    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(nameButton);

    openMenu();
    key("End");
    key("ArrowUp");
    expect(document.activeElement?.getAttribute("role")).toBe("menuitemcheckbox");
    key("Home");
    key("ArrowUp");
    key("Home");
    key("ArrowDown");
    key(" ");
    expect(onVisibleColumnsChange).toHaveBeenLastCalledWith(["name", "modified", "path"]);
  });
});
