import { describe, expect, it } from "vitest";
import { assertVersion, stampCargoLock, stampCargoToml, stampJson } from "./stamp-version.mjs";

describe("assertVersion", () => {
  it.each(["2.1.0", "0.0.1", "10.20.30", "2.1.0-rc.1"])("accepts %s", (v) => {
    expect(assertVersion(v)).toBe(v);
  });
  it.each(["v2.1.0", "2.1", "2.1.0.1", "", "01.2.3", undefined])("rejects %s", (v) => {
    expect(() => assertVersion(v)).toThrow();
  });
});

describe("stampJson", () => {
  it("replaces only the top-level version and keeps formatting", () => {
    const text = '{\n  "name": "x",\n  "version": "0.1.0",\n  "bundle": { "version": "9" }\n}\n';
    expect(stampJson(text, "2.1.0")).toBe(
      '{\n  "name": "x",\n  "version": "2.1.0",\n  "bundle": { "version": "9" }\n}\n',
    );
  });
  it("fails without a version field", () => {
    expect(() => stampJson('{ "name": "x" }', "2.1.0")).toThrow();
  });
});

describe("stampCargoToml", () => {
  const toml =
    '[package]\nname = "cde-tauri"\nversion = "0.1.0"\n\n[dependencies]\ntauri = { version = "2" }\nserde_json = "1"\n';
  it("replaces the package version only", () => {
    const out = stampCargoToml(toml, "2.1.0");
    expect(out).toContain('[package]\nname = "cde-tauri"\nversion = "2.1.0"');
    expect(out).toContain('tauri = { version = "2" }');
  });
  it("fails without a package version", () => {
    expect(() => stampCargoToml('[dependencies]\nx = "1"\n', "2.1.0")).toThrow();
  });
});

describe("stampCargoLock", () => {
  const lock =
    '[[package]]\nname = "cde-tauri"\nversion = "0.1.0"\ndependencies = []\n\n[[package]]\nname = "serde"\nversion = "1.0.0"\n';
  it("replaces the named package only", () => {
    const out = stampCargoLock(lock, "cde-tauri", "2.1.0");
    expect(out).toContain('name = "cde-tauri"\nversion = "2.1.0"');
    expect(out).toContain('name = "serde"\nversion = "1.0.0"');
  });
  it("leaves a lock without the package alone", () => {
    const other = '[[package]]\nname = "a"\nversion = "1"\n';
    expect(stampCargoLock(other, "cde-tauri", "2.1.0")).toBe(other);
  });
});
