import { describe, expect, it } from "vitest";
import { closeMenus, hoverMenu, keyMenu, toggleMenu, type MenuState } from "./menuModel";

const closed: MenuState = { openId: null };

describe("menuModel", () => {
  it("toggle opens a closed menu and closes the same open menu", () => {
    const opened = toggleMenu(closed, "file");
    expect(opened.openId).toBe("file");
    expect(toggleMenu(opened, "file").openId).toBeNull();
  });

  it("toggling another title switches menus (only one open)", () => {
    expect(toggleMenu({ openId: "file" }, "help").openId).toBe("help");
  });

  it("hover switches only while a menu is open", () => {
    expect(hoverMenu(closed, "help").openId).toBeNull();
    expect(hoverMenu({ openId: "file" }, "help").openId).toBe("help");
    expect(hoverMenu({ openId: "file" }, "file").openId).toBe("file");
  });

  it("Escape closes; other keys do nothing", () => {
    expect(keyMenu({ openId: "file" }, "Escape").openId).toBeNull();
    const s = { openId: "file" };
    expect(keyMenu(s, "a")).toBe(s);
  });

  it("closeMenus closes", () => {
    expect(closeMenus().openId).toBeNull();
  });

  it("arrow keys move between menus while open, wrapping", () => {
    const ids = ["file", "help"];
    expect(keyMenu({ openId: "file" }, "ArrowRight", ids).openId).toBe("help");
    expect(keyMenu({ openId: "help" }, "ArrowRight", ids).openId).toBe("file");
    expect(keyMenu({ openId: "file" }, "ArrowLeft", ids).openId).toBe("help");
    expect(keyMenu(closed, "ArrowRight", ids).openId).toBeNull();
  });
});
