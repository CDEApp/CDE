// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { DirectoryNode } from "../api/types";
import { CatalogTree, type TreeRoot } from "./CatalogTree";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(cleanup);

const root: TreeRoot = {
  ref: { catalogId: 1, entryIndex: 0 },
  label: "C:",
  fullPath: "C:\\",
  catalogName: "cat",
  size: 100,
};
const child: DirectoryNode = {
  ref: { catalogId: 1, entryIndex: 1 },
  name: "Users",
  fullPath: "C:\Users",
  isDirectory: true,
  hasChildren: false,
  size: 10,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isReparsePoint: false,
};

function setup() {
  const children = vi.fn().mockResolvedValue([child]);
  const client = { children } as unknown as CdeApiClient;
  render(
    <CatalogTree
      client={client}
      roots={[root]}
      selectedKey={null}
      onSelect={() => {}}
      onContextMenu={() => {}}
      resolveStyle={() => undefined}
    />,
  );
  return { children };
}

describe("CatalogTree expander keyboard support", () => {
  it("exposes the expander as a button that Enter activates", async () => {
    const { children } = setup();
    const expander = screen.getByRole("button", { name: "Expand" });
    expect(expander.getAttribute("tabindex")).toBe("-1"); // the tree container owns focus
    fireEvent.keyDown(expander, { key: "Enter" });
    await waitFor(() => expect(screen.getByRole("treeitem", { name: /Users/ })).toBeTruthy());
    expect(children).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: "Collapse" })).toBeTruthy();
  });

  it("Space collapses an expanded folder", async () => {
    setup();
    fireEvent.keyDown(screen.getByRole("button", { name: "Expand" }), { key: " " });
    const collapse = await screen.findByRole("button", { name: "Collapse" });
    fireEvent.keyDown(collapse, { key: " " });
    await waitFor(() => expect(screen.queryByRole("treeitem", { name: /Users/ })).toBeNull());
  });
});

describe("CatalogTree collapse all", () => {
  it("closes every open folder when collapseNonce changes", async () => {
    const client = { children: vi.fn().mockResolvedValue([child]) } as unknown as CdeApiClient;
    const props = { client, roots: [root], selectedKey: null, onSelect: () => {}, onContextMenu: () => {}, resolveStyle: () => undefined };
    const { rerender } = render(<CatalogTree {...props} collapseNonce={0} />);
    fireEvent.keyDown(screen.getByRole("button", { name: "Expand" }), { key: "Enter" });
    await screen.findByRole("treeitem", { name: /Users/ });
    rerender(<CatalogTree {...props} collapseNonce={1} />);
    await waitFor(() => expect(screen.queryByRole("treeitem", { name: /Users/ })).toBeNull());
    expect(screen.getByRole("treeitem", { name: /C:/ })).toBeTruthy();
  });
});

describe("CatalogTree sort and filter of the roots", () => {
  const roots: TreeRoot[] = [
    root,
    { ref: { catalogId: 2, entryIndex: 0 }, label: "D:", fullPath: "D:\\", catalogName: "backup", size: 900 },
    { ref: { catalogId: 3, entryIndex: 0 }, label: "E:", fullPath: "E:\\", catalogName: "media", size: 500 },
  ];
  const labels = () => screen.getAllByRole("treeitem").map((r) => r.querySelector(".tree-label")?.textContent);

  function mount() {
    const client = { children: vi.fn().mockResolvedValue([child]) } as unknown as CdeApiClient;
    render(
      <CatalogTree client={client} roots={roots} selectedKey={null} onSelect={() => {}} onContextMenu={() => {}} resolveStyle={() => undefined} />,
    );
  }
  const filterBox = () => screen.getByRole("textbox", { name: "Filter catalogs" });

  it("filters by root or catalog name and restores the rows when cleared", () => {
    mount();
    expect(labels()).toEqual(["C:", "D:", "E:"]);
    fireEvent.change(filterBox(), { target: { value: "back" } });
    expect(labels()).toEqual(["D:"]);
    fireEvent.change(filterBox(), { target: { value: "e:" } });
    expect(labels()).toEqual(["E:"]);
    fireEvent.change(filterBox(), { target: { value: "zzz" } });
    expect(screen.getByText("No catalogs match")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Clear filter" }));
    expect(labels()).toEqual(["C:", "D:", "E:"]);
    expect(screen.queryByText("No catalogs match")).toBeNull();
  });

  it("Escape clears the filter", () => {
    mount();
    fireEvent.change(filterBox(), { target: { value: "d:" } });
    fireEvent.keyDown(filterBox(), { key: "Escape" });
    expect((filterBox() as HTMLInputElement).value).toBe("");
    expect(labels()).toEqual(["C:", "D:", "E:"]);
  });

  it("sorts by size largest first, and the direction toggle flips it", () => {
    mount();
    const direction = screen.getByRole("button", { name: "Sort direction" }) as HTMLButtonElement;
    expect(direction.disabled).toBe(true);
    fireEvent.change(screen.getByRole("combobox", { name: "Sort catalogs" }), { target: { value: "size" } });
    expect(labels()).toEqual(["D:", "E:", "C:"]);
    expect(direction.disabled).toBe(false);
    fireEvent.click(direction);
    expect(labels()).toEqual(["C:", "E:", "D:"]);
    fireEvent.change(screen.getByRole("combobox", { name: "Sort catalogs" }), { target: { value: "catalog" } });
    expect(labels()).toEqual(["D:", "C:", "E:"]);
    fireEvent.change(screen.getByRole("combobox", { name: "Sort catalogs" }), { target: { value: "" } });
    expect(labels()).toEqual(["C:", "D:", "E:"]);
  });

  it("keeps a catalog's expansion while the filter hides it", async () => {
    mount();
    fireEvent.click(screen.getAllByRole("button", { name: "Expand" })[0]);
    await screen.findByRole("treeitem", { name: /Users/ });
    fireEvent.change(filterBox(), { target: { value: "back" } });
    expect(screen.queryByRole("treeitem", { name: /Users/ })).toBeNull();
    fireEvent.change(filterBox(), { target: { value: "" } });
    expect(screen.getByRole("treeitem", { name: /Users/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Collapse" })).toBeTruthy();
  });

  describe("reveal into a hidden catalog", () => {
    const base = { roots, onSelect: () => {}, onContextMenu: () => {}, resolveStyle: () => undefined };
    const childOf2: DirectoryNode = { ...child, ref: { catalogId: 2, entryIndex: 1 }, name: "Backups", fullPath: "D:\Backups" };
    const reveal = { selectedKey: "2-0", revealKey: "2-0", revealNonce: 1, expandTo: [roots[1].ref] };
    const mountWith = () => {
      const client = {
        children: vi.fn((ref: { catalogId: number }) => Promise.resolve([ref.catalogId === 2 ? childOf2 : child])),
      } as unknown as CdeApiClient;
      const props = { ...base, client };
      const view = render(<CatalogTree {...props} selectedKey={null} />);
      return { props, ...view };
    };

    it("clears the filter and shows the target catalog", async () => {
      const { props, rerender } = mountWith();
      fireEvent.change(filterBox(), { target: { value: "cat" } });
      expect(labels()).toEqual(["C:"]);
      rerender(<CatalogTree {...props} {...reveal} />);
      await screen.findByRole("treeitem", { name: /Backups/ });
      expect((filterBox() as HTMLInputElement).value).toBe("");
      expect(labels()).toEqual(["C:", "D:", "Backups", "E:"]);
    });

    it("leaves the filter alone when the target catalog is still shown", async () => {
      const { props, rerender } = mountWith();
      fireEvent.change(filterBox(), { target: { value: "back" } });
      rerender(<CatalogTree {...props} {...reveal} />);
      await screen.findByRole("treeitem", { name: /Backups/ });
      expect((filterBox() as HTMLInputElement).value).toBe("back");
      expect(labels()).toEqual(["D:", "Backups"]);
    });

    it("changes nothing when no filter is set", async () => {
      const { props, rerender } = mountWith();
      rerender(<CatalogTree {...props} {...reveal} />);
      await screen.findByRole("treeitem", { name: /Backups/ });
      expect((filterBox() as HTMLInputElement).value).toBe("");
    });
  });
});

describe("CatalogTree root tooltip", () => {
  const renderRoot = (r: TreeRoot) =>
    render(
      <CatalogTree
        client={{} as CdeApiClient}
        roots={[r]}
        selectedKey={null}
        onSelect={() => {}}
        onContextMenu={() => {}}
        resolveStyle={() => undefined}
      />,
    );

  it("tells how long ago the catalog was scanned", () => {
    renderRoot({ ...root, scannedAt: new Date(Date.now() - 3 * 86_400_000 - 60_000) });
    expect(screen.getByText("C:").closest(".tree-label")?.getAttribute("title")).toBe("C:\\ [cat]\nScanned 3 days ago");
  });

  it("keeps the plain path tooltip when the scan time is unknown", () => {
    renderRoot(root);
    expect(screen.getByText("C:").closest(".tree-label")?.getAttribute("title")).toBe("C:\\ [cat]");
  });
});
