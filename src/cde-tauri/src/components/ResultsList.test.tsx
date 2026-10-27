// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { SearchResultRow } from "../api/types";
import { ResultsList } from "./ResultsList";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(cleanup);

const mk = (name: string, size: number): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: size },
  name,
  size,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: `C:\${name}`,
  parentPath: "C:\\",
  catalogName: "cat",
});

function setup() {
  render(
    <ResultsList
      rows={[mk("b.txt", 2), mk("a.txt", 1)]}
      onActivate={vi.fn()}
      onContextMenu={vi.fn()}
      onColumnOrderChange={vi.fn()}
      resolveStyle={() => undefined}
    />,
  );
}

const nameHeader = () => screen.getByRole("columnheader", { name: /Name/ });

describe("ResultsList header accessibility", () => {
  it("renders a grid with column headers and rows", () => {
    setup();
    expect(screen.getByRole("grid")).toBeTruthy();
    expect(screen.getAllByRole("columnheader")).toHaveLength(5);
    const firstRow = screen.getAllByRole("row")[1];
    expect(within(firstRow).getAllByRole("gridcell").length).toBe(5);
  });

  it("Enter on the focused sort button sorts ascending, then descending", async () => {
    setup();
    const user = userEvent.setup();
    expect(nameHeader().getAttribute("aria-sort")).toBe("none");
    await user.tab();
    const button = within(nameHeader()).getByRole("button");
    expect(document.activeElement).toBe(button);
    await user.keyboard("{Enter}");
    expect(nameHeader().getAttribute("aria-sort")).toBe("ascending");
    await user.keyboard(" ");
    expect(nameHeader().getAttribute("aria-sort")).toBe("descending");
  });

  it("a mouse click still sorts", () => {
    setup();
    fireEvent.click(within(nameHeader()).getByRole("button"));
    expect(nameHeader().getAttribute("aria-sort")).toBe("ascending");
  });

  it("the resize handle is a focusable separator that arrow keys resize", () => {
    setup();
    const handle = within(nameHeader()).getByRole("slider");
    expect(handle.tabIndex).toBe(0);
    expect(handle.getAttribute("aria-valuenow")).toBe("260");
    fireEvent.keyDown(handle, { key: "ArrowRight" });
    expect(handle.getAttribute("aria-valuenow")).toBe("270");
    fireEvent.keyDown(handle, { key: "ArrowLeft", shiftKey: true });
    expect(handle.getAttribute("aria-valuenow")).toBe("220");
    fireEvent.keyDown(handle, { key: "Home" });
    expect(handle.getAttribute("aria-valuenow")).toBe("260");
  });
});
