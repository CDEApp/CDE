import { describe, expect, it } from "vitest";
import { parseVersion } from "./release-version.mjs";
import { assertVersion, stampCargoLock, stampCargoToml, stampJson } from "./stamp-version.mjs";

describe("assertVersion", () => {
  it.each(["2.1.0", "0.0.1", "10.20.30", "2.1.0-rc.1", "2.1.0-beta.12"])("accepts %s", (v) => {
    expect(assertVersion(v)).toBe(v);
  });
  it.each(["v2.1.0", "2.1", "2.1.0.1", "", "01.2.3", undefined])("rejects %s", (v) => {
    expect(() => assertVersion(v)).toThrow();
  });
  // The rules are release-version.mjs's: nothing it rejects may be stamped.
  it.each(["2.1.0-beta.0", "2.1.0-gamma.1", "2.1.0-beta", "2.1.0-rc.1.2", "2.1.0-1", "2.1.0+build"])(
    "rejects %s, as release-version does",
    (v) => {
      expect(parseVersion(v)).toBeNull();
      expect(() => assertVersion(v)).toThrow();
    },
  );
});

describe("stampJson", () => {
  it("replaces the top-level version", () => {
    const text = '{\n  "name": "x",\n  "version": "0.1.0"\n}\n';
    expect(stampJson(text, "2.1.0")).toBe('{\n  "name": "x",\n  "version": "2.1.0"\n}\n');
  });
  it("leaves a nested version key alone, even when it comes first", () => {
    const text = '{ "bundle": { "version": "9" }, "version": "0.1.0" }';
    const out = JSON.parse(stampJson(text, "2.1.0"));
    expect(out.version).toBe("2.1.0");
    expect(out.bundle.version).toBe("9");
  });
  it("ends with a newline", () => {
    expect(stampJson('{"version":"0.1.0"}', "2.1.0").endsWith("}\n")).toBe(true);
  });
  it("fails without a top-level version, even if a nested one exists", () => {
    expect(() => stampJson('{ "name": "x" }', "2.1.0")).toThrow();
    expect(() => stampJson('{ "bundle": { "version": "9" } }', "2.1.0")).toThrow();
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
  it("copes with an array before version", () => {
    const text = '[package]\nname = "x"\nauthors = ["a", "b"]\nversion = "0.1.0" # keep\nedition = "2021"\n';
    expect(stampCargoToml(text, "2.1.0")).toBe(
      '[package]\nname = "x"\nauthors = ["a", "b"]\nversion = "2.1.0" # keep\nedition = "2021"\n',
    );
  });
  it("copes with inline tables and does not touch later tables", () => {
    const text =
      '[package]\nname = "x"\nmetadata = { a = [1, 2], b = "c" }\nversion = "0.1.0"\n\n[dependencies]\nversion = "7"\n';
    const out = stampCargoToml(text, "2.1.0");
    expect(out).toContain('metadata = { a = [1, 2], b = "c" }\nversion = "2.1.0"');
    expect(out).toContain('[dependencies]\nversion = "7"');
  });
  it("keeps CRLF line endings", () => {
    expect(stampCargoToml('[package]\r\nversion = "0.1.0"\r\n', "2.1.0")).toBe('[package]\r\nversion = "2.1.0"\r\n');
  });
  it("does not take a version from another table when [package] has none", () => {
    expect(() => stampCargoToml('[package]\nname = "x"\n\n[dependencies]\nversion = "7"\n', "2.1.0")).toThrow();
  });
  it("fails without a package table", () => {
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
