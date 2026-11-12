// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { SearchResultRow } from "../api/types";
import { emptyJobs } from "../jobsModel";
import { EMPTY_SELECTION } from "../rowSelection";
import { initialView, showView, type PlainView, type ViewState } from "../viewState";
import { MainLayout } from "./MainLayout";

vi.mock("@tauri-apps/plugin-dialog", () => ({ save: vi.fn() }));
vi.mock("@tauri-apps/plugin-fs", () => ({ writeFile: vi.fn() }));

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});
afterEach(cleanup);

const row = (n: number): SearchResultRow => ({
  ref: { catalogId: 0, entryIndex: n },
  name: `f${n}.txt`,
  size: 100 - n,
  modified: "2020-01-01T00:00:00Z",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath: `C:\\f${n}.txt`,
  parentPath: "C:\\",
  catalogName: "c",
});
const rows = [1, 2, 3].map(row);

function layout(view: ViewState) {
  const fn = vi.fn();
  const props = {
    view,
    setView: fn,
    client: {},
    ui: null,
    catalogs: [],
    roots: [],
    selection: {
      selectedKey: null, revealKey: null, revealNonce: 0, expandTo: null, dirPath: "", dirNodes: [], canGoUp: false,
      selectFromTree: fn, activateDir: fn, goUp: fn, viewInTree: fn, selectNode: fn,
    },
    search: {
      results: rows, busy: false, hasLastQuery: true, staleBanner: () => null, searchAgain: fn,
      limitReachedCount: null, raiseLimit: fn,
    },
    rowSelection: { selection: EMPTY_SELECTION, dispatch: fn },
    resultsFilter: {
      text: "", setText: fn, mode: "substring", setMode: fn, field: "name", setField: fn,
      shown: rows, total: rows.length, active: false, error: null, searchable: false,
    },
    onSearchWithFilter: fn,
    colours: { resolveStyle: () => undefined, resolveTreeStyle: () => undefined, text: "", setText: fn, save: fn, saveError: null, getDefaults: fn },
    jobs: { model: emptyJobs, cancelJob: fn, dismissJob: fn, clearFinished: fn },
    menus: new Proxy({}, { get: () => fn }),
    diffEpoch: 0,
    collapseNonce: 0,
    selectedCatalogId: null,
    staleAfterDays: 0,
    now: new Date(),
    onSelectCatalog: fn,
    onNewCatalog: fn,
    onExplore: fn,
    onStatus: fn,
  };
  return <MainLayout {...(props as unknown as Parameters<typeof MainLayout>[0])} />;
}

const at = (kind: PlainView) => showView(initialView, kind);
const resultsGrid = () => screen.getByRole("grid", { name: "Search results", hidden: true });
const sizeHeader = () => within(resultsGrid()).getByRole("columnheader", { name: /Size/, hidden: true });

describe("MainLayout keeps the Search Results tab mounted", () => {
  it("keeps the sort when the user switches to another tab and back", () => {
    const { rerender } = render(layout(at("results")));
    fireEvent.click(within(sizeHeader()).getByRole("button", { hidden: true }));
    const sorted = sizeHeader().getAttribute("aria-sort");
    expect(sorted).not.toBe("none");

    rerender(layout(at("catalogs")));
    expect(resultsGrid().closest(".view-pane-hidden")).not.toBeNull();

    rerender(layout(at("results")));
    expect(sizeHeader().getAttribute("aria-sort")).toBe(sorted);
    expect(screen.getByRole("grid", { name: "Search results" }).closest(".view-pane-hidden")).toBeNull();
  });
});
