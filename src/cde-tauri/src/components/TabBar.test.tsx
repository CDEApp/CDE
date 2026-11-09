// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TabBar } from "./TabBar";

afterEach(cleanup);

function setup(over: Partial<Parameters<typeof TabBar>[0]> = {}) {
  const props = {
    active: "directory" as const,
    hasTreemap: false,
    activeJobCount: 0,
    onSelect: vi.fn(),
    onDirectoryMenu: vi.fn(),
    loadBlockedReason: null,
    onReload: vi.fn(),
    ...over,
  };
  render(<TabBar {...props} />);
  return props;
}

const tabNames = () => screen.getAllByRole("button").map((b) => b.textContent);

describe("TabBar", () => {
  it("lists the tabs, with TreeMap only once a folder has been mapped", () => {
    setup();
    expect(tabNames()).toEqual(["Directory", "Search Results", "Catalogs", "Compare", "Jobs", "Customize", "Reload"]);
    cleanup();
    setup({ hasTreemap: true });
    expect(tabNames()).toContain("TreeMap");
  });

  it("marks the active tab and counts active jobs", () => {
    setup({ active: "jobs", activeJobCount: 2 });
    expect(screen.getByRole("button", { name: "Jobs (2)" }).className).toBe("active");
    expect(screen.getByRole("button", { name: "Directory" }).className).toBe("");
  });

  it("a click selects that tab; a right-click on Directory opens its menu", () => {
    const p = setup({ hasTreemap: true });
    fireEvent.click(screen.getByRole("button", { name: "TreeMap" }));
    expect(p.onSelect).toHaveBeenCalledWith("treemap");
    fireEvent.contextMenu(screen.getByRole("button", { name: "Directory" }), { clientX: 5, clientY: 6 });
    expect(p.onDirectoryMenu).toHaveBeenCalledWith(5, 6);
  });

  it("Reload is disabled, with the reason, while loading is blocked", () => {
    setup({ loadBlockedReason: "Jobs are running" });
    const reload = screen.getByRole("button", { name: "Reload" });
    expect(reload).toHaveProperty("disabled", true);
    expect(reload.title).toBe("Jobs are running");
  });
});
