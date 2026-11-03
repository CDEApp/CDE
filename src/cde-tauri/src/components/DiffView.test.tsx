// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { CatalogInfo, DirectoryNode, EntryRef, SubtreeSummary } from "../api/types";
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
  compareSummaries: vi.fn(async () => []),
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

  it("labels the catalog and root fields on each side", () => {
    render(<DiffView client={client} catalogs={[catalog(1, "C:\left")]} resolveStyle={() => undefined} onExplore={onExplore} />);
    expect(screen.getAllByText("Catalog")).toHaveLength(2);
    const roots = screen.getAllByText("Root");
    expect(roots).toHaveLength(2);
    for (const root of roots) expect(root.tagName).toBe("LABEL");
    const sides = document.querySelectorAll(".diff-side");
    expect(sides).toHaveLength(2);
    for (const side of sides) {
      expect(side.querySelectorAll(".diff-field-label")).toHaveLength(2);
      expect(side.querySelector("label")!.getAttribute("for")).toBe(side.querySelector("input")!.id);
    }
    expect(screen.getByLabelText("Left catalog")).toBeTruthy();
    expect(screen.getByLabelText("Right path")).toBeTruthy();
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
    expect(dataRows()).toHaveLength(2);
    expect(rowOf("docs").className).toContain("different");
    expect(within(rowOf("a.txt")).getAllByText("a.txt")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Same" }).getAttribute("aria-pressed")).toBe("true");
  });

  it("applies a filter picked from the Diffs menu and keeps Diffs active", async () => {
    await setup();
    fireEvent.click(screen.getByRole("button", { name: "Diffs options" }));
    const entries = screen.getAllByRole("listitem").map((li) => li.textContent!.replace(/^[✓ \s]+/, ""));
    expect(entries).toHaveLength(10);
    expect(entries[0]).toBe("Show Differences");
    fireEvent.click(screen.getByText("Show Left Orphans"));
    expect(dataRows()).toHaveLength(2);
    expect(rowOf("docs")).toBeTruthy();
    expect(rowOf("only-left.txt").className).toContain("left-only");
    const diffs = screen.getByRole("button", { name: "Diffs" });
    expect(diffs.getAttribute("aria-pressed")).toBe("true");
    expect(diffs.title).toBe("Show Left Orphans");
    expect(screen.queryAllByRole("listitem")).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    expect(dataRows()).toHaveLength(4);
    expect(diffs.getAttribute("aria-pressed")).toBe("false");
    fireEvent.click(diffs);
    expect(dataRows()).toHaveLength(2);
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

  describe("exact subtree summaries", () => {
    const summary = (over: Partial<SubtreeSummary> = {}): SubtreeSummary => ({
      sameFiles: 0,
      differentFiles: 0,
      leftNewerFiles: 0,
      rightNewerFiles: 0,
      leftOnly: 0,
      rightOnly: 0,
      ...over,
    });
    type Pair = { left: EntryRef; right: EntryRef };

    // Folders a (equal size, identical inside) and b (equal size, one file differs) on both sides.
    const folderListing = (catalogId: number, names: string[]) =>
      names.map((n, i) => node(catalogId, i + 1, n, { isDirectory: true, size: 5 }));
    function mount(names: string[], compareSummaries: (pairs: Pair[]) => Promise<SubtreeSummary[]>) {
      const sub = {
        children: vi.fn(async (r: EntryRef) => (r.entryIndex === 0 ? folderListing(r.catalogId, names) : [])),
        compareSummaries: vi.fn(compareSummaries),
      };
      render(<DiffView client={sub as unknown as CdeApiClient} catalogs={[catalog(1, "C:\\left"), catalog(2, "D:\\right")]} resolveStyle={() => undefined} onExplore={onExplore} />);
      pick("Left", 1);
      pick("Right", 2);
      return sub;
    }
    const byName = (pairs: Pair[]) => pairs.map((p) => (p.left.entryIndex === 1 ? summary({ sameFiles: 2 }) : summary({ differentFiles: 1 })));
    const visibleNames = () => dataRows().map((r) => within(r).getAllByText(/^[ab]$/)[0].textContent);

    it("hides the identical folder under Diffs once its summary arrives and shows both under All", async () => {
      const sub = mount(["a", "b"], async (pairs) => byName(pairs));
      await waitFor(() => expect(sub.compareSummaries).toHaveBeenCalledTimes(1));
      expect(sub.compareSummaries.mock.calls[0][0]).toEqual([
        { left: { catalogId: 1, entryIndex: 1 }, right: { catalogId: 2, entryIndex: 1 } },
        { left: { catalogId: 1, entryIndex: 2 }, right: { catalogId: 2, entryIndex: 2 } },
      ]);
      fireEvent.click(screen.getByRole("button", { name: "Diffs" }));
      await waitFor(() => expect(visibleNames()).toEqual(["b"]));
      expect(rowOf("b").className).toContain("different");
      fireEvent.click(screen.getByRole("button", { name: "All" }));
      expect(visibleNames()).toEqual(["a", "b"]);
      expect(rowOf("a").className).toContain("same");
    });

    it("keeps every folder visible when the summary request fails", async () => {
      const error = vi.spyOn(console, "error").mockImplementation(() => {});
      const sub = mount(["a", "b"], async () => {
        throw new Error("boom");
      });
      await waitFor(() => expect(error).toHaveBeenCalled());
      fireEvent.click(screen.getByRole("button", { name: "Diffs" }));
      expect(visibleNames()).toEqual(["a", "b"]);
      expect(sub.compareSummaries).toHaveBeenCalledTimes(1);
      error.mockRestore();
    });

    it("requests summaries in batches of at most 100 pairs", async () => {
      const names = Array.from({ length: 250 }, (_, i) => `f${String(i).padStart(3, "0")}`);
      const sub = mount(names, async (pairs) => pairs.map(() => summary({ sameFiles: 1 })));
      await waitFor(() => expect(sub.compareSummaries).toHaveBeenCalledTimes(3));
      expect(sub.compareSummaries.mock.calls.map((c) => c[0].length)).toEqual([100, 100, 50]);
    });
  });
});
