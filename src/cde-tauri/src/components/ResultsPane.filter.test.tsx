// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { SearchResultRow } from "../api/types";
import { useResultsFilter } from "../hooks/useResultsFilter";
import { useRowSelection } from "../hooks/useRowSelection";
import { countText } from "../resultsFilter";
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
const ROWS = [
  mk(1, "alpha.txt", "C:\\one\\"),
  mk(2, "beta.txt", "C:\\two\\"),
  mk(3, "alpine.md", "C:\\one\\"),
  mk(4, "gamma.md", "C:\\three\\"),
];

function Harness({ onSearchWithFilter = vi.fn(), busy = false }: { onSearchWithFilter?: () => void; busy?: boolean }) {
  const rowSelection = useRowSelection(0);
  const filter = useResultsFilter(ROWS, 0);
  return (
    <ResultsPane
      rows={ROWS}
      rowSelection={rowSelection}
      filter={filter}
      canSearchWithFilter
      onSearchWithFilter={onSearchWithFilter}
      busy={busy}
      staleBanner={null}
      onSearchAgain={vi.fn()}
      resolveStyle={() => undefined}
      getColumnOrder={() => undefined}
      onColumnOrderChange={vi.fn()}
      getVisibleColumns={() => undefined}
      onVisibleColumnsChange={vi.fn()}
      onActivate={vi.fn()}
      onContextMenu={vi.fn()}
    />
  );
}

const box = () => screen.getByRole("textbox", { name: "Filter loaded results" });
const rowOf = (name: string) => screen.getByRole("row", { name: new RegExp(`^${name.replace(".", "\\.")}`) });
const searchWithFilter = () => screen.getByRole("button", { name: "Search again with this filter" });
const rowCount = () => screen.getAllByRole("row").length - 1;

describe("ResultsPane filter", () => {
  it("narrows the rows as you type and says how many of the loaded rows are shown", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    expect(rowCount()).toBe(4);
    await user.type(box(), "alp");
    await waitFor(() => expect(rowCount()).toBe(2));
    expect(screen.getByText(countText(2, 4))).toBeTruthy();
    expect(screen.queryByRole("row", { name: /^beta/ })).toBeNull();
  });

  it("filters by full path when the field is Full path", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.selectOptions(screen.getByRole("combobox", { name: "Filter field" }), "path");
    await user.type(box(), "three");
    await waitFor(() => expect(rowCount()).toBe(1));
    expect(rowOf("gamma.md")).toBeTruthy();
  });

  it("the Regex option applies a regular expression", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole("checkbox", { name: "Regex" }));
    fireEvent.change(box(), { target: { value: "\\.md$" } });
    await waitFor(() => expect(rowCount()).toBe(2));
    expect(screen.getByText(countText(2, 4))).toBeTruthy();
  });

  it("an invalid regex shows a message and leaves the list as it was", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole("checkbox", { name: "Regex" }));
    fireEvent.change(box(), { target: { value: "alp" } });
    await waitFor(() => expect(rowCount()).toBe(2));
    fireEvent.change(box(), { target: { value: "alp(" } });
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(rowCount()).toBe(2);
    expect(searchWithFilter()).toHaveProperty("disabled", true);
  });

  it("Ctrl+A selects only the shown rows, and Export selected writes only those", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.type(box(), ".md");
    await waitFor(() => expect(rowCount()).toBe(2));
    await user.click(rowOf("alpine.md"));
    await user.keyboard("{Control>}a{/Control}");
    fireEvent.click(screen.getByRole("button", { name: "Export selected…" }));
    await waitFor(() => expect(h.writeFile).toHaveBeenCalled());
    const csv = new TextDecoder().decode(h.writeFile.mock.calls[0][1] as Uint8Array).trim().split("\r\n");
    expect(csv.slice(1).map((l) => l.split(",")[0]).sort()).toEqual(["alpine.md", "gamma.md"]);
  });

  it("Ctrl+C copies only the selected rows that the filter still shows", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(rowOf("alpha.txt"));
    await user.keyboard("{Control>}");
    await user.click(rowOf("beta.txt"));
    await user.keyboard("{/Control}");
    await user.type(box(), "beta");
    await waitFor(() => expect(rowCount()).toBe(1));
    await user.click(screen.getByRole("row", { name: /^beta/ }));
    await user.keyboard("{Control>}c{/Control}");
    expect(await navigator.clipboard.readText()).toBe("C:\\two\\beta.txt");
  });

  it("Export results still writes every loaded row while a filter is on", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.type(box(), "alp");
    await waitFor(() => expect(rowCount()).toBe(2));
    fireEvent.click(screen.getByRole("button", { name: "Export results…" }));
    await waitFor(() => expect(h.writeFile).toHaveBeenCalled());
    expect(new TextDecoder().decode(h.writeFile.mock.calls[0][1] as Uint8Array).trim().split("\r\n")).toHaveLength(5);
  });

  it("Search again with this filter needs filter text and an idle search, then runs the callback", async () => {
    const user = userEvent.setup();
    const onSearchWithFilter = vi.fn();
    render(<Harness onSearchWithFilter={onSearchWithFilter} />);
    expect(searchWithFilter()).toHaveProperty("disabled", true);
    await user.type(box(), "alp");
    fireEvent.click(searchWithFilter());
    expect(onSearchWithFilter).toHaveBeenCalledOnce();
    cleanup();
    render(<Harness busy />);
    await user.type(box(), "alp");
    expect(searchWithFilter()).toHaveProperty("disabled", true);
  });
});
