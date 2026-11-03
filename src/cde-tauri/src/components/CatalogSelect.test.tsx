// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { CatalogInfo } from "../api/types";
import { CatalogSelect } from "./CatalogSelect";

afterEach(cleanup);

const named = { catalogId: 1, rootPath: "C:\\left", sourcePath: "E:\\cats\\C-System.cdex" } as CatalogInfo;
const unnamed = { catalogId: 2, rootPath: "D:\\right" } as CatalogInfo;

function setup(value: number | null = null) {
  const onChange = vi.fn();
  render(<CatalogSelect label="Left" catalogs={[named, unnamed]} value={value} onChange={onChange} />);
  return { onChange, button: screen.getByLabelText("Left catalog") };
}

describe("CatalogSelect", () => {
  it("shows the placeholder, or the selected catalog's path and name, when closed", () => {
    const { button } = setup();
    expect(button.textContent).toContain("Select a catalog…");
    cleanup();
    expect(setup(1).button.textContent).toContain("C:\\left [C-System]");
  });

  it("renders the path and the bracketed name as separate elements, the name in the right-aligned part", () => {
    const { button } = setup();
    fireEvent.click(button);
    const [, first, second] = screen.getAllByRole("option");
    expect(within(first).getByText("C:\\left").className).toBe("catalog-select-path");
    const name = within(first).getByText("[C-System]");
    expect(name.className).toBe("catalog-select-name");
    expect(name.parentElement).toBe(first);
    expect(within(second).getByText("D:\\right")).toBeTruthy();
    expect(second.querySelector(".catalog-select-name")).toBeNull();
  });

  it("calls onChange with the picked catalog and closes", () => {
    const { button, onChange } = setup();
    fireEvent.click(button);
    fireEvent.click(screen.getAllByRole("option")[2]);
    expect(onChange).toHaveBeenCalledWith(2);
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("clears the selection from the placeholder row", () => {
    const { button, onChange } = setup(1);
    fireEvent.click(button);
    fireEvent.click(screen.getAllByRole("option")[0]);
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("picks with the arrow keys and Enter", () => {
    const { button, onChange } = setup();
    fireEvent.keyDown(button, { key: "ArrowDown" });
    fireEvent.keyDown(button, { key: "ArrowDown" });
    fireEvent.keyDown(button, { key: "Enter" });
    expect(onChange).toHaveBeenCalledWith(1);
  });

  it("closes on Escape without choosing", () => {
    const { button, onChange } = setup();
    fireEvent.click(button);
    expect(screen.getByRole("listbox")).toBeTruthy();
    fireEvent.keyDown(button, { key: "Escape" });
    expect(screen.queryByRole("listbox")).toBeNull();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("closes on an outside click", () => {
    const { button } = setup();
    fireEvent.click(button);
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole("listbox")).toBeNull();
  });
});
