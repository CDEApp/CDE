// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { SearchResultRow } from "../api/types";
import { useResultsFilter } from "../hooks/useResultsFilter";
import { useRowSelection } from "../hooks/useRowSelection";
import { ResultsPane } from "./ResultsPane";

const h = vi.hoisted(() => ({ save: vi.fn(), writeFile: vi.fn(), show: vi.fn() }));
vi.mock("@tauri-apps/plugin-dialog", () => ({ save: h.save }));
vi.mock("@tauri-apps/plugin-fs", () => ({ writeFile: h.writeFile }));
vi.mock("@mantine/notifications", () => ({ notifications: { show: h.show } }));

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
beforeEach(() => {
  h.save.mockReset().mockResolvedValue("out.csv");
  h.writeFile.mockReset().mockResolvedValue(undefined);
});
afterEach(cleanup);

const mk = (n: number, name: string, parentPath: string): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: n },
  name,
  size: n,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: `${parentPath}${name}`,
  parentPath,
  catalogName: "cat",
});
const ROWS = [mk(1, "a.txt", "C:\\one\\"), mk(2, "b.txt", "C:\\two\\"), mk(3, "=c.txt", "C:\\three\\")];

function Harness({ onContextMenu, busy = false }: { onContextMenu?: (...a: unknown[]) => void; busy?: boolean }) {
  const rowSelection = useRowSelection(0);
  const filter = useResultsFilter(ROWS, 0);
  return (
    <ResultsPane
      rows={ROWS}
      rowSelection={rowSelection}
      filter={filter}
      canSearchWithFilter
      onSearchWithFilter={vi.fn()}
      busy={busy}
      staleBanner={null}
      onSearchAgain={vi.fn()}
      resolveStyle={() => undefined}
      getColumnOrder={() => undefined}
      onColumnOrderChange={vi.fn()}
      onActivate={vi.fn()}
      onContextMenu={(onContextMenu as never) ?? vi.fn()}
    />
  );
}

const NL = String.fromCharCode(10);
const rowOf = (name: string) => screen.getByRole("row", { name: new RegExp(`^${name.replace(".", "\\.")}`) });
const nameSortButton = () => within(screen.getByRole("columnheader", { name: /Name/ })).getByRole("button");
const sortNameDescending = () => {
  fireEvent.click(nameSortButton());
  fireEvent.click(nameSortButton());
};
const exportedCsv = () => new TextDecoder().decode(h.writeFile.mock.calls[0][1] as Uint8Array);

describe("ResultsPane copy and export of the selected rows", () => {
  it("Ctrl+C copies the selected full paths in the on-screen order after a re-sort", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    sortNameDescending();
    await user.click(rowOf("a.txt"));
    await user.keyboard("{Control>}");
    await user.click(rowOf("b.txt"));
    await user.keyboard("{/Control}");
    await user.keyboard("{Control>}c{/Control}");
    expect(await navigator.clipboard.readText()).toBe(`C:\\two\\b.txt${NL}C:\\one\\a.txt`);
  });

  it("Cmd+C copies too, and copies nothing when no row is selected", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await navigator.clipboard.writeText("untouched");
    screen.getByRole("grid", { name: "Search results" }).focus();
    await user.keyboard("{Meta>}c{/Meta}");
    expect(await navigator.clipboard.readText()).toBe("untouched");
    await user.click(rowOf("a.txt"));
    await user.keyboard("{Meta>}c{/Meta}");
    expect(await navigator.clipboard.readText()).toBe("C:\\one\\a.txt");
  });

  it("right-clicking a row in the selection hands the whole selection to the menu in on-screen order", async () => {
    const user = userEvent.setup();
    const onContextMenu = vi.fn();
    render(<Harness onContextMenu={onContextMenu} />);
    sortNameDescending();
    await user.click(rowOf("a.txt"));
    await user.keyboard("{Control>}");
    await user.click(rowOf("b.txt"));
    await user.keyboard("{/Control}");
    fireEvent.contextMenu(rowOf("a.txt"));
    const [row, selection] = onContextMenu.mock.calls[0];
    expect((row as SearchResultRow).name).toBe("a.txt");
    expect((selection as SearchResultRow[]).map((r) => r.name)).toEqual(["b.txt", "a.txt"]);
  });

  it("right-clicking a row outside the selection acts on that row alone", async () => {
    const user = userEvent.setup();
    const onContextMenu = vi.fn();
    render(<Harness onContextMenu={onContextMenu} />);
    await user.click(rowOf("a.txt"));
    fireEvent.contextMenu(rowOf("b.txt"));
    expect((onContextMenu.mock.calls[0][1] as SearchResultRow[]).map((r) => r.name)).toEqual(["b.txt"]);
  });

  it("Export selected writes the selected rows in on-screen order, with the formula guard", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    sortNameDescending();
    await user.click(rowOf("a.txt"));
    await user.keyboard("{Control>}a{/Control}");
    fireEvent.click(screen.getByRole("button", { name: "Export selected…" }));
    await waitFor(() => expect(h.writeFile).toHaveBeenCalled());
    const lines = exportedCsv().trim().split("\r\n");
    expect(lines[0]).toBe("Name,Size,Modified,Catalog,Path");
    expect(lines.slice(1)).toEqual([
      "b.txt,2,2020-01-01T00:00:00Z,cat,C:\\two\\",
      "a.txt,1,2020-01-01T00:00:00Z,cat,C:\\one\\",
      "'=c.txt,3,2020-01-01T00:00:00Z,cat,C:\\three\\",
    ]);
  });

  it("Export results still writes every row", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(rowOf("a.txt"));
    fireEvent.click(screen.getByRole("button", { name: "Export results…" }));
    await waitFor(() => expect(h.writeFile).toHaveBeenCalled());
    expect(exportedCsv().trim().split("\r\n")).toHaveLength(4);
  });

  it("Export selected is disabled with a reason when nothing is selected or while searching", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    const button = screen.getByRole("button", { name: "Export selected…" });
    expect(button).toHaveProperty("disabled", true);
    expect(button.title).toBe("Select results to export");
    await user.click(rowOf("a.txt"));
    expect(button).toHaveProperty("disabled", false);
    cleanup();
    render(<Harness busy />);
    expect(screen.getByRole("button", { name: "Export selected…" })).toHaveProperty("title", "Wait for the search to finish");
  });
});
