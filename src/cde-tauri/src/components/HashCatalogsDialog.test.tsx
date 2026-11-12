// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { JobRefusal, type CdeApiClient } from "../api/client";
import type { CatalogInfo, HashPreflightRequest, HashPreflightResult } from "../api/types";
import { HASH_PREVIEW_DEBOUNCE_MS } from "../hooks/useHashPreview";
import { applyJob, emptyJobs } from "../jobsModel";
import { AppMantineProvider } from "../ui/mantine";
import { HashCatalogsDialog } from "./HashCatalogsDialog";

beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
  window.matchMedia ??= ((query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
    addListener() {},
    removeListener() {},
    dispatchEvent: () => false,
    onchange: null,
  })) as typeof window.matchMedia;
});

const catalog = (catalogId: number, sourcePath = `C:\\cats\\c${catalogId}.cdex`) =>
  ({ catalogId, rootPath: `R${catalogId}:\\`, sourcePath }) as unknown as CatalogInfo;

const answer = (files: number, bytes = files * 1024, alreadyHashed = 0): HashPreflightResult => ({ files, bytes, alreadyHashed });

function deferred<T>() {
  let resolve!: (v: T) => void;
  let reject!: (e: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function setup(over: { catalogs?: CatalogInfo[]; initial?: number[]; jobs?: typeof emptyJobs; preflight?: CdeApiClient["preflightHash"] } = {}) {
  const preflightHash = vi.fn(over.preflight ?? (async (_r: HashPreflightRequest) => answer(3, 3 * 1024 ** 3, 1)));
  const client = { preflightHash } as unknown as CdeApiClient;
  const onStart = vi.fn();
  const onClose = vi.fn();
  render(
    <AppMantineProvider>
      <HashCatalogsDialog
        initialCatalogIds={over.initial ?? [1]}
        catalogs={over.catalogs ?? [catalog(1), catalog(2)]}
        jobs={over.jobs ?? emptyJobs}
        client={client}
        onStart={onStart}
        onClose={onClose}
      />
    </AppMantineProvider>,
  );
  return { preflightHash, onStart, onClose };
}

const settle = () => act(async () => void (await vi.advanceTimersByTimeAsync(HASH_PREVIEW_DEBOUNCE_MS + 1)));
const startButton = () => screen.getByRole("button", { name: "Start" }) as HTMLButtonElement;
const filterBox = () => screen.getByLabelText("Filter (optional)") as HTMLInputElement;

beforeEach(() => vi.useFakeTimers());
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("HashCatalogsDialog", () => {
  it("says files are compared by size across the picked catalogs", () => {
    setup();
    expect(screen.getByText("Files are compared by size across the catalogs you pick.")).toBeTruthy();
  });

  it("shows the preview once the debounce has passed, and no request before it", async () => {
    const t = setup();
    expect(screen.getByText("Counting files…")).toBeTruthy();
    expect(startButton().disabled).toBe(true);
    expect(t.preflightHash).not.toHaveBeenCalled();
    await settle();
    expect(screen.getByText("3 files, 3 GB will be hashed (1 already hashed)")).toBeTruthy();
    expect(startButton().disabled).toBe(false);
    expect(t.preflightHash).toHaveBeenCalledTimes(1);
    expect(t.preflightHash.mock.calls[0][0]).toEqual({ catalogIds: [1], hash: null });
  });

  it("asks once for a burst of typing, with the final filter", async () => {
    const t = setup();
    await settle();
    t.preflightHash.mockClear();
    for (const text of ["s", "si", "size:>2gb"]) {
      fireEvent.change(filterBox(), { target: { value: text } });
      await act(async () => void (await vi.advanceTimersByTimeAsync(HASH_PREVIEW_DEBOUNCE_MS - 50)));
    }
    expect(t.preflightHash).not.toHaveBeenCalled();
    await settle();
    expect(t.preflightHash).toHaveBeenCalledTimes(1);
    expect(t.preflightHash.mock.calls[0][0]).toEqual({
      catalogIds: [1],
      hash: { filter: "size:>2gb", includeUniqueSizes: false },
    });
  });

  it("ignores an answer that arrives after a newer request was made", async () => {
    const first = deferred<HashPreflightResult>();
    const second = deferred<HashPreflightResult>();
    const replies = [first, second];
    const t = setup({ preflight: () => replies.shift()!.promise });
    await settle();
    fireEvent.change(filterBox(), { target: { value: "ext:iso" } });
    await settle();
    expect(t.preflightHash).toHaveBeenCalledTimes(2);
    await act(async () => second.resolve(answer(2, 2048)));
    await act(async () => first.resolve(answer(99, 99 * 1024)));
    expect(screen.getByText("2 files, 2 KB will be hashed (0 already hashed)")).toBeTruthy();
    expect(screen.queryByText(/99 files/)).toBeNull();
  });

  it("shows the parser's message inline for an invalid filter and disables Start", async () => {
    setup({
      preflight: async () => {
        throw new JobRefusal({ code: "invalid-filter", message: "Unknown function: sizee" });
      },
    });
    fireEvent.change(filterBox(), { target: { value: "sizee:1" } });
    await settle();
    expect(screen.getByRole("alert").textContent).toBe("Unknown function: sizee");
    expect(startButton().disabled).toBe(true);
    expect(filterBox().getAttribute("aria-invalid")).toBe("true");
  });

  it("starts the job with the picked catalogs, the filter and the unique-size flag", async () => {
    const t = setup({ initial: [1] });
    fireEvent.click(screen.getByLabelText("R2:\\"));
    fireEvent.change(filterBox(), { target: { value: " size:>2gb " } });
    fireEvent.click(screen.getByLabelText("Also hash files with a unique size"));
    await settle();
    fireEvent.click(startButton());
    expect(t.onStart).toHaveBeenCalledWith({
      catalogIds: [1, 2],
      hash: { filter: "size:>2gb", includeUniqueSizes: true },
    });
  });

  it("starts with no options for the default selection", async () => {
    const t = setup();
    await settle();
    fireEvent.click(startButton());
    expect(t.onStart).toHaveBeenCalledWith({ catalogIds: [1], hash: null });
  });

  it("disables a legacy catalog and one a job holds, with the reason, and never submits them", async () => {
    const held = applyJob(emptyJobs, {
      id: 1,
      kind: "hash",
      title: "Hashing",
      catalogId: null,
      catalogIds: [2],
      catalogName: "c2",
      state: "running",
      queuedAtUtc: "",
      startedAtUtc: null,
      endedAtUtc: null,
      phase: "hashing",
      entriesScanned: 0,
      note: "",
      result: null,
      error: null,
    });
    const t = setup({
      catalogs: [catalog(1), catalog(2), catalog(3, "C:\\cats\\old.cde")],
      initial: [1, 2, 3],
      jobs: held,
    });
    expect((screen.getByLabelText("R2:\\") as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByLabelText("R3:\\") as HTMLInputElement).disabled).toBe(true);
    expect(screen.getByText(/already queued or running/)).toBeTruthy();
    expect(screen.getByText(/Legacy .cde catalogs/)).toBeTruthy();
    await settle();
    expect(t.preflightHash.mock.calls[0][0].catalogIds).toEqual([1]);
  });

  it("lets the user drop the catalog the sidecar refused, and then start", async () => {
    const t = setup({
      initial: [1, 2],
      preflight: async (r) => {
        if (r.catalogIds.includes(2)) {
          throw new JobRefusal({ code: "root-not-accessible", message: "R2:\\ cannot be reached", rootPath: "R2:\\" });
        }
        return answer(4);
      },
    });
    await settle();
    expect(screen.getByRole("alert").textContent).toBe("R2:\\ cannot be reached");
    expect(startButton().disabled).toBe(true);
    fireEvent.click(screen.getByLabelText("R2:\\"));
    await settle();
    expect(startButton().disabled).toBe(false);
    fireEvent.click(startButton());
    expect(t.onStart).toHaveBeenCalledWith({ catalogIds: [1], hash: null });
  });

  it("asks for at least one catalog and sends no request when none is ticked", async () => {
    const t = setup({ initial: [] });
    await settle();
    expect(screen.getByText("Pick at least one catalog.")).toBeTruthy();
    expect(t.preflightHash).not.toHaveBeenCalled();
    expect(startButton().disabled).toBe(true);
  });
});
