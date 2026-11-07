// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { JobDto } from "../api/types";
import { JobsView } from "./JobsView";

afterEach(cleanup);

function job(id: number, over: Partial<JobDto>): JobDto {
  return {
    id,
    kind: "rescan",
    title: `Task ${id}`,
    catalogId: id,
    catalogName: `c${id}`,
    state: "queued",
    queuedAtUtc: "2026-01-01T00:00:00Z",
    startedAtUtc: null,
    endedAtUtc: null,
    phase: "queued",
    entriesScanned: 0,
    note: "",
    result: null,
    error: null,
    ...over,
  };
}

function setup(jobs: JobDto[]) {
  const h = { onCancel: vi.fn(), onDismiss: vi.fn(), onClearFinished: vi.fn() };
  render(<JobsView jobs={jobs} {...h} />);
  return h;
}

const rowOf = (title: string) => screen.getByText(title).closest("tr") as HTMLElement;

describe("JobsView", () => {
  it("lists title, started, progress and status per job", () => {
    setup([
      job(1, { state: "running", startedAtUtc: "2026-01-01T02:00:00Z", phase: "scanning", entriesScanned: 5, note: "C:\\x" }),
      job(2, {}),
    ]);
    const running = within(rowOf("Task 1"));
    expect(running.getByText("scanning · 5 entries · C:\\x")).toBeTruthy();
    expect(running.getByText("Running")).toBeTruthy();
    const queued = within(rowOf("Task 2"));
    expect(queued.getByText("queued")).toBeTruthy();
    expect(queued.getByText("Queued")).toBeTruthy();
  });

  it("offers Cancel on active rows and Dismiss on finished rows", () => {
    const h = setup([job(1, { state: "running" }), job(2, {}), job(3, { state: "succeeded" })]);
    fireEvent.click(within(rowOf("Task 1")).getByText("Cancel"));
    fireEvent.click(within(rowOf("Task 2")).getByText("Cancel"));
    fireEvent.click(within(rowOf("Task 3")).getByText("Dismiss"));
    expect(h.onCancel.mock.calls).toEqual([[1], [2]]);
    expect(h.onDismiss.mock.calls).toEqual([[3]]);
    expect(within(rowOf("Task 3")).queryByText("Cancel")).toBeNull();
  });

  it("enables Clear finished only when a finished job exists", () => {
    setup([job(1, { state: "running" })]);
    expect((screen.getByText("Clear finished") as HTMLButtonElement).disabled).toBe(true);
    cleanup();
    const h = setup([job(1, { state: "failed" })]);
    fireEvent.click(screen.getByText("Clear finished"));
    expect(h.onClearFinished).toHaveBeenCalledOnce();
  });

  it("says so when there are no jobs", () => {
    setup([]);
    expect(screen.getByText(/No jobs/)).toBeTruthy();
  });
});
