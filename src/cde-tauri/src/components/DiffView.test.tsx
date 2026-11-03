// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { CatalogInfo, DirectoryNode, EntryRef } from "../api/types";
import { DiffView } from "./DiffView";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
  Object.defineProperty(HTMLElement.prototype, "clientHeight", { configurable: true, value: 400 });
});
afterEach(cleanup);

const onExplore = vi.fn();
beforeEach(() => onExplore.mockClear());

const catalog = (catalogId: number, rootPath: string) => ({ catalogId, rootPath }) as CatalogInfo;

function node(catalogId: number, entryIndex: number, name: string, over: Partial<DirectoryNode> = {}): DirectoryNode {
  return {
    ref: { catalogId, entryIndex },
    name,
    fullPath: name,
    isDirectory: false,
    hasChildren: false,
    size: 10,
    modified: "2024-01-01T10:00:00Z",
    isModifiedBad: false,
    isReparsePoint: false,
    ...over,
  };
}

// Catalog 1 (left) and 2 (right): "docs" differs in size, "a.txt" is the same, "only-left.txt" and
// "only-right.txt" are unique, "docs/inner.txt" exists on both sides.
const listings: Record<string, DirectoryNode[]> = {
  "1-0": [
    node(1, 1, "docs", { isDirectory: true, size: 5, fullPath: "C:\\left\\docs" }),
    node(1, 2, "a.txt"),
    node(1, 3, "only-left.txt"),
  ],
  "2-0": [
    node(2, 1, "docs", { isDirectory: true, size: 9, fullPath: "D:\\right\\docs" }),
    node(2, 2, "a.txt"),
    node(2, 3, "only-right.txt"),
  ],
  "1-1": [node(1, 4, "inner.txt")],
  "2-1": [node(2, 4, "inner.txt", { size: 77 })],
};

const key = (r: EntryRef) => `${r.catalogId}-${r.entryIndex}`;
const client = {
  children: vi.fn(async (r: EntryRef) => listings[key(r)] ?? []),
} as unknown as CdeApiClient;

/** Opens the side's catalog dropdown and returns its option rows' text (the placeholder row first). */
const openOptions = (side: string) => {
  fireEvent.click(screen.getByLabelText(`${side} catalog`));
  return within(screen.getByRole("listbox")).getAllByRole("option");
};
const pick = (side: string, index: number) => fireEvent.click(openOptions(side)[index]);
const optionTexts = (side: string) => openOptions(side).map((o) => o.textContent);

async function setup() {
  render(<DiffView client={client} catalogs={[catalog(1, "C:\\left"), catalog(2, "D:\\right")]} resolveStyle={() => undefined} onExplore={onExplore} />);
  pick("Left", 1);
  pick("Right", 2);
  await screen.findByText("only-left.txt");
}

const dataRows = () => screen.getAllByRole("row").slice(1);
const rowOf = (name: string) => dataRows().find((r) => within(r).queryAllByText(name).length > 0)!;

describe("DiffView", () => {
  it("shows a hint until both sides have a catalog", () => {
    render(<DiffView client={client} catalogs={[catalog(1, "C:\\left")]} resolveStyle={() => undefined} onExplore={onExplore} />);
    expect(screen.getByText(/Choose a catalog on each side/)).toBeTruthy();
  });

  it("labels each catalog option with its root path and catalog name in brackets", () => {
    const named = { catalogId: 1, rootPath: "C:\\left", sourcePath: "E:\\cats\\C-System.cdex" } as CatalogInfo;
    render(<DiffView client={client} catalogs={[named, catalog(2, "D:\\right")]} resolveStyle={() => undefined} onExplore={onExplore} />);
    const labels = optionTexts("Left");
    expect(labels).toContain("C:\\left[C-System]");
    expect(labels).toContain("D:\\right");
  });

  it("lists the catalogs by on-disk file name, ascending", () => {
    const file = (catalogId: number, rootPath: string, sourcePath: string) =>
      ({ catalogId, rootPath, sourcePath }) as CatalogInfo;
    const cats = [
      file(1, "Z:\\first", "E:\\c\\b-second.cdex"),
      file(2, "A:\\last", "E:\\c\\C-third.cdex"),
      file(3, "M:\\mid", "E:\\c\\a-first.cdex"),
    ];
    render(<DiffView client={client} catalogs={cats} resolveStyle={() => undefined} onExplore={onExplore} />);
    expect(optionTexts("Left").slice(1)).toEqual(["M:\\mid[a-first]", "Z:\\first[b-second]", "A:\\last[C-third]"]);
  });

  it("aligns rows with an empty cell where an entry is missing", async () => {
    await setup();
    expect(dataRows()).toHaveLength(4);
    expect(rowOf("only-left.txt").className).toContain("left-only");
    expect(within(rowOf("only-left.txt")).queryAllByText("only-left.txt")).toHaveLength(1);
    expect(rowOf("only-right.txt").className).toContain("right-only");
    expect(rowOf("docs").className).toContain("different");
    expect(within(rowOf("docs")).getAllByText("docs")).toHaveLength(2);
    expect(rowOf("a.txt").className).toContain("same");
  });

  it("filters rows with Diffs and Same", async () => {
    await setup();
    fireEvent.click(screen.getByRole("button", { name: "Diffs" }));
    expect(dataRows().map((r) => r.className.match(/same|different|left-only|right-only/)![0]).sort()).toEqual([
      "different", "left-only", "right-only",
    ]);
    fireEvent.click(screen.getByRole("button", { name: "Same" }));
    expect(dataRows()).toHaveLength(1);
    expect(within(dataRows()[0]).getAllByText("a.txt")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Same" }).getAttribute("aria-pressed")).toBe("true");
  });

  it("applies a filter picked from the Diffs menu and keeps Diffs active", async () => {
    await setup();
    fireEvent.click(screen.getByRole("button", { name: "Diffs options" }));
    const entries = screen.getAllByRole("listitem").map((li) => li.textContent!.replace(/^[✓ \s]+/, ""));
    expect(entries).toHaveLength(10);
    expect(entries[0]).toBe("Show Differences");
    fireEvent.click(screen.getByText("Show Left Orphans"));
    expect(dataRows()).toHaveLength(1);
    expect(rowOf("only-left.txt").className).toContain("left-only");
    const diffs = screen.getByRole("button", { name: "Diffs" });
    expect(diffs.getAttribute("aria-pressed")).toBe("true");
    expect(diffs.title).toBe("Show Left Orphans");
    expect(screen.queryAllByRole("listitem")).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    expect(dataRows()).toHaveLength(4);
    expect(diffs.getAttribute("aria-pressed")).toBe("false");
    fireEvent.click(diffs);
    expect(dataRows()).toHaveLength(1);
  });

  it("expands a folder on both sides and collapses it again", async () => {
    await setup();
    fireEvent.click(within(rowOf("docs")).getAllByRole("button", { name: "Expand docs" })[0]);
    await waitFor(() => expect(dataRows()).toHaveLength(5));
    const child = dataRows()[1];
    expect(within(child).getAllByText("inner.txt")).toHaveLength(2);
    expect(child.className).toContain("different");
    fireEvent.click(within(rowOf("docs")).getAllByRole("button", { name: "Collapse docs" })[0]);
    expect(dataRows()).toHaveLength(4);
  });

  describe("Set as root", () => {
    const halves = (name: string) => within(rowOf(name)).getAllByText(name).map((el) => el.closest(".diff-half")!);
    const setAsRoot = () => fireEvent.click(screen.getByText("Set as root"));

    it("makes a folder the root of the left pane only", async () => {
      await setup();
      vi.mocked(client.children).mockClear();
      fireEvent.contextMenu(halves("docs")[0]);
      setAsRoot();
      await screen.findByText("inner.txt");
      expect((screen.getByLabelText("Left path") as HTMLInputElement).value).toBe("C:\\left\\docs");
      expect((screen.getByLabelText("Right path") as HTMLInputElement).value).toBe("D:\\right");
      expect(client.children).toHaveBeenCalledWith({ catalogId: 1, entryIndex: 1 }, expect.anything());
      expect(client.children).toHaveBeenCalledWith({ catalogId: 2, entryIndex: 0 }, expect.anything());
      expect(screen.queryByText("only-left.txt")).toBeNull();
      expect(screen.getByText("only-right.txt")).toBeTruthy();
      expect(screen.queryByText("Set as root")).toBeNull();
    });

    it("makes a folder the root of the right pane only", async () => {
      await setup();
      fireEvent.contextMenu(halves("docs")[1]);
      setAsRoot();
      await screen.findByText("inner.txt");
      expect((screen.getByLabelText("Right path") as HTMLInputElement).value).toBe("D:\\right\\docs");
      expect((screen.getByLabelText("Left path") as HTMLInputElement).value).toBe("C:\\left");
      expect(client.children).toHaveBeenCalledWith({ catalogId: 2, entryIndex: 1 }, expect.anything());
    });

    it("keeps the path box resolvable after Set as root", async () => {
      await setup();
      fireEvent.contextMenu(halves("docs")[0]);
      setAsRoot();
      await screen.findByText("inner.txt");
      fireEvent.keyDown(screen.getByLabelText("Left path"), { key: "Enter" });
      await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
    });

    it("Explore passes the right-clicked folder's ref and closes the menu", async () => {
      await setup();
      fireEvent.contextMenu(halves("docs")[0]);
      fireEvent.click(screen.getByText("Explore"));
      expect(onExplore).toHaveBeenCalledTimes(1);
      expect(onExplore).toHaveBeenLastCalledWith({ catalogId: 1, entryIndex: 1 });
      expect(screen.queryByText("Explore")).toBeNull();
      fireEvent.contextMenu(halves("docs")[1]);
      fireEvent.click(screen.getByText("Explore"));
      expect(onExplore).toHaveBeenCalledTimes(2);
      expect(onExplore).toHaveBeenLastCalledWith({ catalogId: 2, entryIndex: 1 });
    });

    it("opens no menu on a file or on an empty half", async () => {
      await setup();
      fireEvent.contextMenu(halves("a.txt")[0]);
      expect(screen.queryByText("Set as root")).toBeNull();
      const empty = rowOf("only-left.txt").querySelector(".diff-half.empty")!;
      fireEvent.contextMenu(empty);
      expect(screen.queryByText("Set as root")).toBeNull();
    });
  });

  it("shows an error when the typed path is not in the catalog", async () => {
    await setup();
    const input = screen.getByLabelText("Left path");
    fireEvent.change(input, { target: { value: "C:\\left\\missing" } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect((await screen.findByRole("alert")).textContent).toContain("missing");
  });
});
