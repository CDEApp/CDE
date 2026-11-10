// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
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
