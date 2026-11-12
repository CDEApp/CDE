// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { refToString, type SearchResultRow } from "../api/types";
import { useRowSelection } from "../hooks/useRowSelection";
import { selectedInOrder, type RowSelection } from "../rowSelection";
import type { StyleResolver } from "../colourRules";
import { ResultsList } from "./ResultsList";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(cleanup);

const mk = (n: number): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: n },
  name: `r${n}.txt`,
  size: n,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: `C:\\r${n}.txt`,
  parentPath: "C:\\",
  catalogName: "cat",
});
const makeRows = (count: number) => Array.from({ length: count }, (_, i) => mk(i + 1));

interface HarnessProps {
  rows: SearchResultRow[];
  epoch?: number;
  onActivate?: (r: SearchResultRow) => void;
  onContextMenu?: (r: SearchResultRow, x: number, y: number) => void;
  resolveStyle?: StyleResolver;
  visibleRowsRef?: { current: readonly SearchResultRow[] };
  selectionRef?: { current: RowSelection | null };
}

function Harness(p: HarnessProps) {
  const { selection, dispatch } = useRowSelection(p.epoch ?? 0);
  if (p.selectionRef) p.selectionRef.current = selection;
  return (
    <ResultsList
      rows={p.rows}
      onActivate={p.onActivate ?? vi.fn()}
      onContextMenu={p.onContextMenu ?? vi.fn()}
      onColumnOrderChange={vi.fn()}
      onVisibleColumnsChange={vi.fn()}
      resolveStyle={p.resolveStyle ?? (() => undefined)}
      selection={selection}
      dispatchSelection={dispatch}
      visibleRowsRef={p.visibleRowsRef}
    />
  );
}

type User = ReturnType<typeof userEvent.setup>;
const rowOf = (name: string) => screen.getByRole("row", { name: new RegExp(`^${name.replace(".", "\\.")}`) });
const dataRows = () => screen.getAllByRole("row").slice(1);
const selectedNames = () =>
  dataRows()
    .filter((r) => r.getAttribute("aria-selected") === "true")
    .map((r) => within(r).getAllByRole("gridcell")[0].textContent);
const nameSortButton = () => within(screen.getByRole("columnheader", { name: /Name/ })).getByRole("button");
const sortNameDescending = () => {
  fireEvent.click(nameSortButton());
  fireEvent.click(nameSortButton());
};
async function clickWith(user: User, modifier: "Control" | "Meta" | "Shift", name: string) {
  await user.keyboard(`{${modifier}>}`);
  await user.click(rowOf(name));
  await user.keyboard(`{/${modifier}}`);
}

describe("ResultsList row selection", () => {
  it("marks the grid multiselectable and every row unselected to start with", () => {
    render(<Harness rows={makeRows(3)} />);
    expect(screen.getByRole("grid").getAttribute("aria-multiselectable")).toBe("true");
    expect(dataRows().map((r) => r.getAttribute("aria-selected"))).toEqual(["false", "false", "false"]);
  });

  it("a click selects one row and replaces the previous selection", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(4)} />);
    await user.click(rowOf("r2.txt"));
    expect(selectedNames()).toEqual(["r2.txt"]);
    await user.click(rowOf("r3.txt"));
    expect(selectedNames()).toEqual(["r3.txt"]);
  });

  it("Ctrl+click and Cmd+click toggle a row", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(4)} />);
    await user.click(rowOf("r1.txt"));
    await clickWith(user, "Control", "r3.txt");
    await clickWith(user, "Meta", "r4.txt");
    await clickWith(user, "Control", "r1.txt");
    expect(selectedNames()).toEqual(["r3.txt", "r4.txt"]);
  });

  it("Shift+click selects the range from the last plain click, in the order shown", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(6)} />);
    await user.click(rowOf("r2.txt"));
    await clickWith(user, "Shift", "r5.txt");
    expect(selectedNames()).toEqual(["r2.txt", "r3.txt", "r4.txt", "r5.txt"]);
    await clickWith(user, "Shift", "r1.txt");
    expect(selectedNames()).toEqual(["r1.txt", "r2.txt"]);
  });

  it("a Shift+click range follows the sorted order, not the arrival order", async () => {
    const user = userEvent.setup();
    const [r1, r2, r3, r4] = makeRows(4);
    render(<Harness rows={[r1, r3, r2, r4]} />);
    fireEvent.click(nameSortButton());
    await user.click(rowOf("r1.txt"));
    await clickWith(user, "Shift", "r2.txt");
    expect(selectedNames()).toEqual(["r1.txt", "r2.txt"]);
  });

  it("arrow keys move the active row and select it; Shift+arrows extend", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(5)} />);
    await user.click(rowOf("r2.txt"));
    await user.keyboard("{ArrowDown}");
    expect(selectedNames()).toEqual(["r3.txt"]);
    expect(screen.getByRole("grid").getAttribute("aria-activedescendant")).toBe(rowOf("r3.txt").id);
    await user.keyboard("{Shift>}{ArrowDown}{ArrowDown}{/Shift}");
    expect(selectedNames()).toEqual(["r3.txt", "r4.txt", "r5.txt"]);
    await user.keyboard("{Shift>}{ArrowUp}{/Shift}");
    expect(selectedNames()).toEqual(["r3.txt", "r4.txt"]);
  });

  it("Home and End jump to the first and last row, and Shift extends to them", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(5)} />);
    await user.click(rowOf("r3.txt"));
    await user.keyboard("{End}");
    expect(selectedNames()).toEqual(["r5.txt"]);
    await user.keyboard("{Shift>}{Home}{/Shift}");
    expect(selectedNames()).toHaveLength(5);
  });

  it("Ctrl+A and Cmd+A select every row", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(5)} />);
    await user.click(rowOf("r1.txt"));
    await user.keyboard("{Control>}a{/Control}");
    expect(selectedNames()).toHaveLength(5);
    await user.keyboard("{Escape}{ArrowDown}{Meta>}a{/Meta}");
    expect(selectedNames()).toHaveLength(5);
  });

  it("Space toggles the active row and Escape clears the selection", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(4)} />);
    await user.click(rowOf("r2.txt"));
    await user.keyboard(" ");
    expect(selectedNames()).toEqual([]);
    await user.keyboard(" ");
    expect(selectedNames()).toEqual(["r2.txt"]);
    await user.keyboard("{Escape}");
    expect(selectedNames()).toEqual([]);
  });

  it("keeps the selection through a re-sort", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(4)} />);
    await user.click(rowOf("r1.txt"));
    await clickWith(user, "Control", "r3.txt");
    sortNameDescending();
    expect(dataRows()[0].textContent).toContain("r4.txt");
    expect(selectedNames()).toEqual(["r3.txt", "r1.txt"]);
  });

  it("keeps the selection through a column resize", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(3)} />);
    await user.click(rowOf("r2.txt"));
    const handle = within(screen.getByRole("columnheader", { name: /Name/ })).getByRole("slider");
    fireEvent.keyDown(handle, { key: "ArrowRight" });
    expect(handle.getAttribute("aria-valuenow")).toBe("270");
    expect(selectedNames()).toEqual(["r2.txt"]);
  });

  it("clears the selection when the search epoch changes, but not when only the rows array changes", async () => {
    const user = userEvent.setup();
    const rows = makeRows(3);
    const { rerender } = render(<Harness rows={rows} epoch={1} />);
    await user.click(rowOf("r2.txt"));
    rerender(<Harness rows={[...rows]} epoch={1} />);
    expect(selectedNames()).toEqual(["r2.txt"]);
    rerender(<Harness rows={makeRows(3)} epoch={2} />);
    expect(selectedNames()).toEqual([]);
  });

  it("a double click still activates the row", async () => {
    const user = userEvent.setup();
    const onActivate = vi.fn();
    const rows = makeRows(2);
    render(<Harness rows={rows} onActivate={onActivate} />);
    await user.dblClick(rowOf("r2.txt"));
    expect(onActivate).toHaveBeenCalledOnce();
    expect(onActivate).toHaveBeenCalledWith(rows[1]);
  });

  it("the context menu still opens for the clicked row at the pointer, and keeps an existing multi-selection", async () => {
    const user = userEvent.setup();
    const onContextMenu = vi.fn();
    const rows = makeRows(3);
    render(<Harness rows={rows} onContextMenu={onContextMenu} />);
    fireEvent.contextMenu(rowOf("r2.txt"), { clientX: 10, clientY: 20 });
    expect(onContextMenu).toHaveBeenCalledWith(rows[1], 10, 20);
    expect(selectedNames()).toEqual(["r2.txt"]);

    await user.click(rowOf("r1.txt"));
    await clickWith(user, "Control", "r3.txt");
    fireEvent.contextMenu(rowOf("r3.txt"), { clientX: 1, clientY: 2 });
    expect(onContextMenu).toHaveBeenLastCalledWith(rows[2], 1, 2);
    expect(selectedNames()).toEqual(["r1.txt", "r3.txt"]);
  });

  it("selection keys are ignored while a header control has focus", async () => {
    const user = userEvent.setup();
    render(<Harness rows={makeRows(3)} />);
    await user.click(rowOf("r1.txt"));
    nameSortButton().focus();
    await user.keyboard("{Control>}a{/Control}{Escape}");
    expect(selectedNames()).toEqual(["r1.txt"]);
  });

  it("publishes the rows in sorted order so a follow-up can read the selection in visible order", async () => {
    const user = userEvent.setup();
    const visibleRowsRef = { current: [] as readonly SearchResultRow[] };
    const selectionRef = { current: null as RowSelection | null };
    render(<Harness rows={makeRows(3)} visibleRowsRef={visibleRowsRef} selectionRef={selectionRef} />);
    sortNameDescending();
    await user.click(rowOf("r1.txt"));
    await clickWith(user, "Control", "r3.txt");
    const picked = selectedInOrder(visibleRowsRef.current, (r) => refToString(r.ref), selectionRef.current!);
    expect(picked.map((r) => r.name)).toEqual(["r3.txt", "r1.txt"]);
  });
});

describe("ResultsList selection at scale", () => {
  it("selecting all 50,000 rows renders only the on-screen rows", async () => {
    const user = userEvent.setup();
    const resolveStyle = vi.fn(() => undefined);
    render(<Harness rows={makeRows(50_000)} resolveStyle={resolveStyle} />);
    const mounted = dataRows().length;
    expect(mounted).toBeLessThan(100);

    await user.click(rowOf("r1.txt"));
    resolveStyle.mockClear();
    await user.keyboard("{Control>}a{/Control}");

    expect(dataRows()).toHaveLength(mounted);
    expect(resolveStyle.mock.calls.length).toBeLessThanOrEqual(mounted);
    expect(selectedNames()).toHaveLength(mounted);
  }, 30_000);
});

describe("ResultsList review fixes", () => {
  it("Home scrolls back to the first row when it is already the active row", async () => {
    const user = userEvent.setup();
    const { container } = render(<Harness rows={makeRows(1000)} />);
    await user.click(rowOf("r1.txt"));
    const scroller = container.querySelector(".vlist-scroll") as HTMLElement;
    scroller.scrollTop = 5000;
    fireEvent.scroll(scroller);
    await user.keyboard("{Home}");
    expect(scroller.scrollTop).toBe(0);
  });

  it("a selected row drops the colour-rule gradient so its name stays visible", async () => {
    const user = userEvent.setup();
    const gradient = { backgroundImage: "linear-gradient(red, blue)", backgroundClip: "text", WebkitTextFillColor: "transparent" };
    const { container } = render(<Harness rows={makeRows(3)} resolveStyle={() => gradient} />);
    const nameSpan = () => rowOf("r2.txt").querySelector(".name-text");
    expect(nameSpan()).not.toBeNull();
    await user.click(rowOf("r2.txt"));
    expect(rowOf("r2.txt").getAttribute("aria-selected")).toBe("true");
    expect(nameSpan()).toBeNull();
    expect(container.querySelectorAll(".name-text").length).toBe(2);
  });
});
