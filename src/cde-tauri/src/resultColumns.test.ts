import { describe, expect, it } from "vitest";
import { entryKind, fileExtension, RESULT_COLUMN_IDS, resolveVisibleColumns, visibleColumnIds } from "./resultColumns";

describe("fileExtension", () => {
  it.each([
    ["report.pdf", "pdf"],
    ["archive.tar.gz", "gz"],
    ["Photo.JPG", "JPG"],
    [".gitignore", ""],
    ["Makefile", ""],
    ["trailing.", ""],
  ])("%s -> %j", (name, ext) => {
    expect(fileExtension({ name, isDirectory: false })).toBe(ext);
  });

  it("is empty for a folder, dots or not", () => {
    expect(fileExtension({ name: "v1.2", isDirectory: true })).toBe("");
  });
});

describe("entryKind", () => {
  it("is link for a reparse point, even a folder one, then folder, then file", () => {
    expect(entryKind({ isDirectory: true, isReparsePoint: true })).toBe("link");
    expect(entryKind({ isDirectory: true, isReparsePoint: false })).toBe("folder");
    expect(entryKind({ isDirectory: false, isReparsePoint: false })).toBe("file");
  });
});

describe("visible columns", () => {
  const DEFAULT = ["name", "size", "modified", "catalog", "path"];

  it("defaults to the original five columns, with the new ones hidden", () => {
    expect([...resolveVisibleColumns(undefined)]).toEqual(DEFAULT);
    expect(RESULT_COLUMN_IDS).toEqual([...DEFAULT, "extension", "type"]);
  });

  it("applies an old saved order unchanged to the default columns", () => {
    expect(visibleColumnIds(["path", "name", "size", "modified", "catalog"], undefined)).toEqual([
      "path", "name", "size", "modified", "catalog",
    ]);
  });

  it("ignores unknown ids and falls back to the defaults when none is left", () => {
    expect([...resolveVisibleColumns(["size", "gone", 4])]).toEqual(["size"]);
    expect([...resolveVisibleColumns(["gone"])]).toEqual(DEFAULT);
    expect([...resolveVisibleColumns([])]).toEqual(DEFAULT);
    expect([...resolveVisibleColumns("name")]).toEqual(DEFAULT);
  });

  it("orders the shown columns by the saved order", () => {
    expect(visibleColumnIds(["type", "name"], ["name", "type", "path"])).toEqual(["type", "name", "path"]);
  });
});
