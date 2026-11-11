// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useMemo } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { refToString, type SearchResultRow } from "../api/types";
import { useRowSelection } from "../hooks/useRowSelection";
import { summarizeRows } from "../selectionSummary";
import { StatusBar } from "./StatusBar";

afterEach(cleanup);

const bar = (activeJobCount: number, onShowJobs = vi.fn()) =>
  render(<StatusBar catalogs={null} resultCount={0} searchTiming="" activeJobCount={activeJobCount} onShowJobs={onShowJobs} />);

describe("StatusBar jobs", () => {
  it("shows the job count with no Cancel button, and the count opens the Jobs tab", () => {
    const onShowJobs = vi.fn();
    bar(3, onShowJobs);
    expect(screen.getByText("Jobs running")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /cancel/i })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "3" }));
    expect(onShowJobs).toHaveBeenCalledOnce();
  });

  it("shows nothing about jobs when none are queued or running", () => {
    bar(0);
    expect(screen.queryByText("Jobs running")).toBeNull();
  });
});

describe("StatusBar stale catalogs", () => {
  it("shows the stale count and threshold, and Rescan stale triggers the action", () => {
    const onRescanStale = vi.fn();
    render(<StatusBar catalogs={null} resultCount={0} searchTiming="" staleCount={3} staleAfterDays={30} onRescanStale={onRescanStale} />);
    expect(screen.getByText(/3 catalogs older than 30 days/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Rescan stale" }));
    expect(onRescanStale).toHaveBeenCalledOnce();
  });

  it("shows nothing when no catalog is stale", () => {
    render(<StatusBar catalogs={null} resultCount={0} searchTiming="" staleCount={0} staleAfterDays={30} />);
    expect(screen.queryByRole("button", { name: "Rescan stale" })).toBeNull();
  });
});

function Harness({ rows }: { rows: SearchResultRow[] }) {
  const { selection, dispatch } = useRowSelection(0);
  const summary = useMemo(() => summarizeRows(rows, selection.selected), [rows, selection.selected]);
  const order = rows.map((r) => refToString(r.ref));
  return (
    <>
      <button onClick={() => dispatch({ type: "click", key: order[0], mode: "replace", order })}>first</button>
      <button onClick={() => dispatch({ type: "click", key: order[1], mode: "toggle", order })}>add folder</button>
      <button onClick={() => dispatch({ type: "selectAll", order })}>all</button>
      <button onClick={() => dispatch({ type: "clear" })}>clear</button>
      <StatusBar catalogs={null} resultCount={rows.length} summary={summary} searchTiming="" />
    </>
  );
}

describe("StatusBar selection summary", () => {
  const mkRow = (n: number, size: number, isDirectory: boolean): SearchResultRow => ({
    ref: { catalogId: 1, entryIndex: n },
    name: `r${n}`,
    size,
    modified: "",
    isModifiedBad: false,
    isDirectory,
    isReparsePoint: false,
    fullPath: `/r${n}`,
    parentPath: "/",
    catalogName: "c",
  });
  const rows = [mkRow(0, 1024, false), mkRow(1, 9999, true), mkRow(2, 2048, false)];

  it("follows the selection and falls back to all results when it is cleared", () => {
    render(<Harness rows={rows} />);
    expect(screen.getByText("All 3 (1 folder), 3 KB in 2 files")).toBeTruthy();
    fireEvent.click(screen.getByText("first"));
    expect(screen.getByText("1 selected, 1 KB in 1 file")).toBeTruthy();
    fireEvent.click(screen.getByText("add folder"));
    expect(screen.getByText("2 selected (1 folder), 1 KB in 1 file")).toBeTruthy();
    fireEvent.click(screen.getByText("all"));
    expect(screen.getByText("3 selected (1 folder), 3 KB in 2 files")).toBeTruthy();
    fireEvent.click(screen.getByText("clear"));
    expect(screen.getByText("All 3 (1 folder), 3 KB in 2 files")).toBeTruthy();
  });

  it("shows no summary without results", () => {
    render(<StatusBar catalogs={null} resultCount={0} summary={summarizeRows([], new Set())} searchTiming="" />);
    expect(screen.queryByText(/in 0 files/)).toBeNull();
  });
});
