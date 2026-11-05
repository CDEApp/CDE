// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { CatalogInfo } from "../api/types";
import { AppMantineProvider } from "../ui/mantine";
import { EditDescriptionDialog } from "./EditDescriptionDialog";

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

const catalog = { catalogId: 4, rootPath: "C:/photos", description: "old text" } as CatalogInfo;

function open(onSave = vi.fn().mockResolvedValue(undefined), onClose = vi.fn()) {
  render(
    <AppMantineProvider>
      <EditDescriptionDialog catalog={catalog} onSave={onSave} onClose={onClose} />
    </AppMantineProvider>,
  );
  return { onSave, onClose, box: screen.getByPlaceholderText("Describe this catalog…") as HTMLTextAreaElement };
}

describe("EditDescriptionDialog", () => {
  it("starts with the current description and saves the typed text for that catalog", async () => {
    const { onSave, onClose, box } = open();
    expect(box.value).toBe("old text");

    fireEvent.change(box, { target: { value: "new text" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    expect(onSave).toHaveBeenCalledWith(4, "new text");
  });

  it("saves with Ctrl+Enter", async () => {
    const { onSave, box } = open();
    fireEvent.keyDown(box, { key: "Enter", ctrlKey: true });
    await waitFor(() => expect(onSave).toHaveBeenCalledWith(4, "old text"));
  });

  it("stays open and shows the reason when the save is refused", async () => {
    const { onClose, box } = open(vi.fn().mockRejectedValue(new Error("A rescan of catalog 4 is already running.")));
    fireEvent.change(box, { target: { value: "x" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect((await screen.findByRole("alert")).textContent).toContain("already running");
    expect(onClose).not.toHaveBeenCalled();
  });

  it("closes without saving on Cancel", () => {
    const { onSave, onClose } = open();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(onSave).not.toHaveBeenCalled();
  });
});
