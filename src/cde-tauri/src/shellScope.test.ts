import { describe, expect, it } from "vitest";
import { REPO_URL } from "./buildInfo";
import confText from "../src-tauri/tauri.conf.json?raw";

// The About link is opened through plugin-shell, whose scope is this regex; keep the two in sync.
describe("tauri shell open scope", () => {
  it("allows the repository URL", () => {
    const conf = JSON.parse(confText);
    const pattern: string = conf.plugins.shell.open;
    expect(typeof pattern).toBe("string");
    expect(new RegExp(pattern).test(REPO_URL)).toBe(true);
    expect(new RegExp(pattern).test("https://example.com/")).toBe(false);
  });
});
