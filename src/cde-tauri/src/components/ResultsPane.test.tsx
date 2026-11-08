// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { SearchResultRow } from "../api/types";
import { ResultsPane } from "./ResultsPane";

const h = vi.hoisted(() => ({ save: vi.fn(), writeFile: vi.fn(), show: vi.fn() }));
vi.mock("@tauri-apps/plugin-dialog", () => ({ save: h.save }));
vi.mock("@tauri-apps/plugin-fs", () => ({ writeFile: h.writeFile }));
vi.mock("@mantine/notifications", () => ({ notifications: { show: h.show } }));
vi.mock("./ResultsList", () => ({ ResultsList: () => <div data-testid="results-list" /> }));

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
beforeEach(() => {
  h.save.mockReset();
  h.writeFile.mockReset().mockResolvedValue(undefined);
  h.show.mockClear();
});
afterEach(cleanup);

const row = { name: "a.txt", fullPath: "C:\\a.txt", size: 1, modified: "", catalogName: "c.cdex" } as unknown as SearchResultRow;

function setup(over: Partial<Parameters<typeof ResultsPane>[0]> = {}) {
  const props = {
    rows: [row],
    busy: false,
    staleBanner: null,
    onSearchAgain: vi.fn(),
    resolveStyle: () => undefined,
    onColumnOrderChange: vi.fn(),
    onActivate: vi.fn(),
    onContextMenu: vi.fn(),
    ...over,
  };
  render(<ResultsPane {...props} />);
  return props;
}

const exportButton = () => screen.getByRole("button", { name: "Export results…" });

describe("ResultsPane", () => {
  it("shows the stale-results banner with Search again, which waits for a running search", () => {
    const p = setup({ staleBanner: "C:\\ was rescanned" });
    fireEvent.click(screen.getByRole("button", { name: "Search again" }));
    expect(p.onSearchAgain).toHaveBeenCalledOnce();
    cleanup();
    setup({ staleBanner: "C:\\ was rescanned", busy: true });
    expect(screen.getByRole("button", { name: "Search again" })).toHaveProperty("disabled", true);
  });

  it("Export is disabled with no rows or while searching", () => {
    setup({ rows: [] });
    expect(exportButton().title).toBe("No results to export");
    cleanup();
    setup({ busy: true });
    expect(exportButton().title).toBe("Wait for the search to finish");
  });

  it("exports the rows as CSV to the chosen file and says so", async () => {
    h.save.mockResolvedValueOnce("D:\\out.csv");
    setup();
    fireEvent.click(exportButton());
    await waitFor(() => expect(h.writeFile).toHaveBeenCalledOnce());
    expect(h.writeFile.mock.calls[0][0]).toBe("D:\\out.csv");
    expect(new TextDecoder().decode(h.writeFile.mock.calls[0][1])).toContain("a.txt");
    await waitFor(() => expect(h.show).toHaveBeenCalledWith(expect.objectContaining({ title: "Results exported" })));
  });

  it("cancelling the save writes nothing; a failed write is reported", async () => {
    h.save.mockResolvedValueOnce(null).mockResolvedValueOnce("D:\\out.csv");
    h.writeFile.mockRejectedValueOnce(new Error("read-only"));
    setup();
    fireEvent.click(exportButton());
    await waitFor(() => expect(h.save).toHaveBeenCalledOnce());
    expect(h.writeFile).not.toHaveBeenCalled();
    fireEvent.click(exportButton());
    await waitFor(() => expect(h.show).toHaveBeenCalledWith(expect.objectContaining({ title: "Export failed", message: "read-only" })));
  });
});
