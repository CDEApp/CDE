// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MenuBar } from "./MenuBar";

afterEach(cleanup);

const noop = () => {};
const bar = (onRescanAll = vi.fn(), rescanAllDisabledReason: string | null = null) =>
  render(
    <MenuBar
      onNewCatalog={noop}
      onOpenFolder={noop}
      onReload={noop}
      onRescanAll={onRescanAll}
      onExit={noop}
      onAbout={noop}
      onSearchSyntax={noop}
      rescanAllDisabledReason={rescanAllDisabledReason}
    />,
  );

describe("MenuBar Rescan All Catalogs", () => {
  it("is in the File menu and runs the rescan", () => {
    const onRescanAll = vi.fn();
    bar(onRescanAll);
    fireEvent.click(screen.getByRole("button", { name: "File" }));
    fireEvent.click(screen.getByText("Rescan All Catalogs"));
    expect(onRescanAll).toHaveBeenCalledOnce();
  });

  it("does nothing when disabled, and says why", () => {
    const onRescanAll = vi.fn();
    bar(onRescanAll, "No catalogs are loaded");
    fireEvent.click(screen.getByRole("button", { name: "File" }));
    const item = screen.getByText("Rescan All Catalogs");
    expect(item.getAttribute("title")).toBe("No catalogs are loaded");
    fireEvent.click(item);
    expect(onRescanAll).not.toHaveBeenCalled();
  });
});
