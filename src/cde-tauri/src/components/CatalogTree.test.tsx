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
