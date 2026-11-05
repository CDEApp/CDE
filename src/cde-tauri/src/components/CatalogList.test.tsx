// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { CatalogInfo } from "../api/types";
import { type CatalogSort, nextCatalogSort } from "../catalogSort";
import { CatalogList } from "./CatalogList";

afterEach(cleanup);

function cat(id: number, rootPath: string, rootSize: number, dirs = 0): CatalogInfo {
  return {
    catalogId: id,
    rootPath,
    volumeName: "",
    dirEntryCount: dirs,
    fileEntryCount: 0,
    rootSize,
    driveLetterHint: "",
    sourcePath: null,
    actualFileName: "",
  } as CatalogInfo;
}

const catalogs = [cat(1, "C:\\one", 30), cat(2, "C:\\two", 10, 5), cat(3, "C:\\three", 20)];

function Harness({ onActivate = () => {} }: { onActivate?: (c: CatalogInfo) => void }) {
  const [sort, setSort] = useState<CatalogSort>(null);
  return (
    <CatalogList
      catalogs={catalogs}
      sort={sort}
      onSort={(c) => setSort((s) => nextCatalogSort(s, c))}
      onActivate={onActivate}
    />
  );
}

const roots = () => screen.getAllByRole("row").slice(1).map((r) => within(r).getAllByRole("cell")[0].textContent);
const header = (name: string) => screen.getByRole("columnheader", { name: new RegExp(`^${name}`) });

describe("CatalogList sorting", () => {
  it("renders every header as a button and keeps incoming order until a click", () => {
    render(<Harness />);
    expect(screen.getAllByRole("columnheader")).toHaveLength(9);
    for (const th of screen.getAllByRole("columnheader")) {
      expect(within(th).getByRole("button")).toBeTruthy();
      expect(th.getAttribute("aria-sort")).toBe("none");
    }
    expect(roots()).toEqual(["C:\\one", "C:\\two", "C:\\three"]);
  });

  it("sorts ascending, then descending on the second click, with aria-sort following", () => {
    render(<Harness />);
    fireEvent.click(within(header("Size")).getByRole("button"));
    expect(roots()).toEqual(["C:\\two", "C:\\three", "C:\\one"]);
    expect(header("Size").getAttribute("aria-sort")).toBe("ascending");
    expect(header("Size").textContent).toContain("▲");

    fireEvent.click(within(header("Size")).getByRole("button"));
    expect(roots()).toEqual(["C:\\one", "C:\\three", "C:\\two"]);
    expect(header("Size").getAttribute("aria-sort")).toBe("descending");
    expect(header("Size").textContent).toContain("▼");
  });

  it("starts another column ascending and clears the previous one", () => {
    render(<Harness />);
    fireEvent.click(within(header("Size")).getByRole("button"));
    fireEvent.click(within(header("Size")).getByRole("button"));
    fireEvent.click(within(header("Root")).getByRole("button"));
    expect(roots()).toEqual(["C:\\one", "C:\\three", "C:\\two"]);
    expect(header("Root").getAttribute("aria-sort")).toBe("ascending");
    expect(header("Size").getAttribute("aria-sort")).toBe("none");
    expect(header("Size").textContent).not.toMatch(/[▲▼]/);
  });

  it("right-aligns only the Dirs, Files and Size headers", () => {
    render(<Harness />);
    for (const name of ["Dirs", "Files", "Size"]) expect(header(name).classList.contains("num")).toBe(true);
    for (const name of ["Root", "Volume", "Drive", "Catalog"]) expect(header(name).classList.contains("num")).toBe(false);
  });

  it("double-click in the sorted view activates that row's catalog", () => {
    const onActivate = vi.fn();
    render(<Harness onActivate={onActivate} />);
    fireEvent.click(within(header("Size")).getByRole("button"));
    fireEvent.doubleClick(screen.getAllByRole("row")[1]);
    expect(onActivate).toHaveBeenCalledWith(catalogs[1]);
  });
});

describe("CatalogList date column and resize", () => {
  const EPOCH = 621_355_968_000_000_000;
  const ticks = (iso: string) => EPOCH + Date.parse(iso) * 10_000;

  it("shows Created and, when known, Modified in their own columns", () => {
    const scan = ticks("2026-03-04T05:06:07Z");
    const withMod = { ...cat(1, "C:\a", 1), scanEndUtcTicks: scan, scanStartUtcTicks: scan, fileModifiedUtcTicks: scan + 6_000_000_000 };
    const noMod = { ...cat(2, "C:\b", 1), scanEndUtcTicks: scan, scanStartUtcTicks: scan, fileModifiedUtcTicks: 0 };
    render(<CatalogList catalogs={[withMod, noMod]} sort={null} onSort={() => {}} onActivate={() => {}} />);
    const cell = (row: number, col: number) =>
      within(screen.getAllByRole("row")[row + 1]).getAllByRole("cell")[col].textContent;
    expect(cell(0, 7)).toMatch(/^\d{4}\/\d\d\/\d\d /);
    expect(cell(0, 8)).toMatch(/^\d{4}\/\d\d\/\d\d /);
    expect(cell(1, 7)).toMatch(/^\d{4}\/\d\d\/\d\d /);
    expect(cell(1, 8)).toBe("");
  });

  it("resizes by dragging, saves on release and does not sort", () => {
    const onSort = vi.fn();
    const onColumnWidthsChange = vi.fn();
    render(<CatalogList catalogs={catalogs} sort={null} onSort={onSort} onActivate={() => {}} onColumnWidthsChange={onColumnWidthsChange} />);
    const resizer = screen.getAllByRole("separator")[2]; // Dirs, default 80
    fireEvent.mouseDown(resizer, { button: 0, clientX: 100 });
    fireEvent.mouseMove(window, { clientX: 150 });
    expect(onColumnWidthsChange).not.toHaveBeenCalled();
    fireEvent.mouseUp(window);
    fireEvent.click(resizer);
    expect(onColumnWidthsChange).toHaveBeenCalledWith({ dirs: 130 });
    expect(onSort).not.toHaveBeenCalled();
  });

  it("resizes from the keyboard and resets with Home", () => {
    const onColumnWidthsChange = vi.fn();
    render(<CatalogList catalogs={catalogs} sort={null} onSort={() => {}} onActivate={() => {}} onColumnWidthsChange={onColumnWidthsChange} />);
    const resizer = screen.getAllByRole("separator")[2];
    fireEvent.keyDown(resizer, { key: "ArrowRight" });
    expect(onColumnWidthsChange).toHaveBeenLastCalledWith({ dirs: 90 });
    fireEvent.keyDown(resizer, { key: "Home" });
    expect(onColumnWidthsChange).toHaveBeenLastCalledWith({});
  });
});
