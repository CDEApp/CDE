// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import type { CreatePreflightResult } from "../api/types";
import { AppMantineProvider } from "../ui/mantine";
import { NewCatalogDialog, type NewCatalogForm } from "./NewCatalogDialog";

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

afterEach(cleanup);

const ready: CreatePreflightResult = {
  defaultFileName: "D-data",
  fileName: "D-data",
  fileNameError: null,
  catalogDir: "C:\\cats",
  targetPath: "C:\\cats\\D-data.cdex",
  exists: false,
  existingCatalogId: null,
  writable: true,
  sameRootCatalogIds: [],
  rootAccessible: true,
  busy: false,
};

async function setup(onCreate: (form: NewCatalogForm) => Promise<string | null> = async () => null) {
  const client = { preflightCreate: vi.fn(async () => ready) } as unknown as CdeApiClient;
  const onCreateFn = vi.fn(onCreate);
  render(
    <AppMantineProvider>
      <NewCatalogDialog
        rootPath={"D:\\data"}
        client={client}
        catalogs={[]}
        onChangeRoot={vi.fn()}
        onCreate={onCreateFn}
        onRescanExisting={vi.fn()}
        onClose={vi.fn()}
      />
    </AppMantineProvider>,
  );
  await act(async () => {});
  return onCreateFn;
}

const hashBox = () => screen.getByLabelText("Hash files after scanning") as HTMLInputElement;
const filterBox = () => screen.getByLabelText("Filter (optional)") as HTMLInputElement;
const createButton = () => screen.getByRole("button", { name: "Create" }) as HTMLButtonElement;

describe("NewCatalogDialog hashing", () => {
  it("shows the filter fields and the size-scope sentence only once the checkbox is ticked", async () => {
    await setup();
    expect(hashBox().checked).toBe(false);
    expect(screen.queryByLabelText("Filter (optional)")).toBeNull();

    fireEvent.click(hashBox());

    expect(filterBox()).toBeTruthy();
    expect(screen.getByLabelText("Also hash files with a unique size")).toBeTruthy();
    expect(screen.getByText(/Sizes are compared inside this new catalog only/)).toBeTruthy();
    expect(screen.queryByText(/Counting files/)).toBeNull();
  });

  it("submits no hash when the box is unticked, even with a filter typed before", async () => {
    const onCreate = await setup();
    fireEvent.click(hashBox());
    fireEvent.change(filterBox(), { target: { value: "size:>1gb" } });
    fireEvent.click(hashBox());

    await act(async () => void fireEvent.click(createButton()));

    expect(onCreate.mock.calls[0][0].hash).toBeNull();
  });

  it("submits the trimmed filter and the unique-size flag when ticked", async () => {
    const onCreate = await setup();
    fireEvent.click(hashBox());
    fireEvent.change(filterBox(), { target: { value: " size:>1gb " } });
    fireEvent.click(screen.getByLabelText("Also hash files with a unique size"));

    await act(async () => void fireEvent.click(createButton()));

    expect(onCreate.mock.calls[0][0]).toMatchObject({
      rootPath: "D:\\data",
      fileName: "D-data",
      hash: { filter: "size:>1gb", includeUniqueSizes: true },
    });
  });

  it("submits an empty filter as null", async () => {
    const onCreate = await setup();
    fireEvent.click(hashBox());

    await act(async () => void fireEvent.click(createButton()));

    expect(onCreate.mock.calls[0][0].hash).toEqual({ filter: null, includeUniqueSizes: false });
  });

  it("shows a refused filter inline, keeps Create available, and clears the message when the filter changes", async () => {
    await setup(async () => "Unknown function: sizee");
    fireEvent.click(hashBox());
    fireEvent.change(filterBox(), { target: { value: "sizee:1" } });

    await act(async () => void fireEvent.click(createButton()));

    expect(screen.getByRole("alert").textContent).toBe("Unknown function: sizee");
    expect(filterBox().getAttribute("aria-invalid")).toBe("true");
    expect(createButton().disabled).toBe(false);

    fireEvent.change(filterBox(), { target: { value: "size:>1gb" } });
    expect(screen.queryByRole("alert")).toBeNull();
  });
});

describe("NewCatalogDialog while a create is being submitted", () => {
  it("cannot be closed or changed until the answer arrives, so the answer always belongs to the form on screen", async () => {
    let answer: (refusal: string | null) => void = () => {};
    const onClose = vi.fn();
    const onChangeRoot = vi.fn();
    const client = { preflightCreate: vi.fn(async () => ready) } as unknown as CdeApiClient;
    render(
      <AppMantineProvider>
        <NewCatalogDialog
          rootPath={"D:\data"}
          client={client}
          catalogs={[]}
          onChangeRoot={onChangeRoot}
          onCreate={() => new Promise((resolve) => (answer = resolve))}
          onRescanExisting={vi.fn()}
          onClose={onClose}
        />
      </AppMantineProvider>,
    );
    await act(async () => {});

    fireEvent.click(screen.getByRole("button", { name: "Create" }));
    await act(async () => {});

    expect(screen.getByRole("button", { name: "Cancel" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Change…" })).toHaveProperty("disabled", true);
    fireEvent.keyDown(document.activeElement ?? document.body, { key: "Escape" });
    expect(onClose).not.toHaveBeenCalled();

    await act(async () => answer(null));
    expect(screen.getByRole("button", { name: "Cancel" })).toHaveProperty("disabled", false);
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
