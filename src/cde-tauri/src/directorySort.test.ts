import { describe, expect, it } from "vitest";
import type { DirectoryNode } from "./api/types";
import { DEFAULT_SORT, nextSort, sortDirectoryNodes, type DirectorySort } from "./directorySort";

let seq = 0;
function node(over: Partial<DirectoryNode>): DirectoryNode {
  return {
    ref: { catalogId: "c", entryIndex: seq++ },
    name: "x",
    fullPath: "",
    isDirectory: false,
    hasChildren: false,
    size: 0,
    modified: "2020-01-01T00:00:00",
    isModifiedBad: false,
    isReparsePoint: false,
    ...over,
  } as DirectoryNode;
}
const names = (ns: DirectoryNode[]) => ns.map((n) => n.name);
const by = (column: DirectorySort["column"], direction: DirectorySort["direction"]): DirectorySort => ({
  column,
  direction,
});

describe("nextSort", () => {
  it("starts ascending on a new column", () => {
    expect(nextSort(by("name", "desc"), "size")).toEqual(by("size", "asc"));
    expect(nextSort(by("size", "desc"), "modified")).toEqual(by("modified", "asc"));
  });
  it("toggles direction on the same column", () => {
    expect(nextSort(by("size", "asc"), "size")).toEqual(by("size", "desc"));
    expect(nextSort(by("size", "desc"), "size")).toEqual(by("size", "asc"));
  });
  it("default is Name ascending", () => {
    expect(DEFAULT_SORT).toEqual(by("name", "asc"));
    expect(nextSort(DEFAULT_SORT, "name")).toEqual(by("name", "desc"));
  });
});

describe("sortDirectoryNodes", () => {
  it("does not mutate the input", () => {
    const input = [node({ name: "b" }), node({ name: "a" })];
    sortDirectoryNodes(input, DEFAULT_SORT);
    expect(names(input)).toEqual(["b", "a"]);
  });

  it("sorts names case-insensitively (ordinal, like the server), folders first", () => {
    const input = [
      node({ name: "b.txt" }),
      node({ name: "Zdir", isDirectory: true }),
      node({ name: "A.txt" }),
      node({ name: "adir", isDirectory: true }),
    ];
    expect(names(sortDirectoryNodes(input, by("name", "asc")))).toEqual(["adir", "Zdir", "A.txt", "b.txt"]);
  });

  it("does not use natural ordering for names", () => {
    const input = [node({ name: "f10" }), node({ name: "f2" })];
    expect(names(sortDirectoryNodes(input, by("name", "asc")))).toEqual(["f10", "f2"]);
  });

  it("name descending reverses within each group, folders still first", () => {
    const input = [
      node({ name: "a" }),
      node({ name: "b" }),
      node({ name: "d1", isDirectory: true }),
      node({ name: "d2", isDirectory: true }),
    ];
    expect(names(sortDirectoryNodes(input, by("name", "desc")))).toEqual(["d2", "d1", "b", "a"]);
  });

  it("sorts size numerically, not as text", () => {
    const input = [node({ name: "a", size: 100 }), node({ name: "b", size: 9 }), node({ name: "c", size: 1000 })];
    expect(names(sortDirectoryNodes(input, by("size", "asc")))).toEqual(["b", "a", "c"]);
    expect(names(sortDirectoryNodes(input, by("size", "desc")))).toEqual(["c", "a", "b"]);
  });

  it("sorts folders by size within the folder group", () => {
    const input = [
      node({ name: "f", size: 1 }),
      node({ name: "big", isDirectory: true, size: 500 }),
      node({ name: "small", isDirectory: true, size: 5 }),
    ];
    expect(names(sortDirectoryNodes(input, by("size", "desc")))).toEqual(["big", "small", "f"]);
    expect(names(sortDirectoryNodes(input, by("size", "asc")))).toEqual(["small", "big", "f"]);
  });

  it("breaks ties by name ascending regardless of direction", () => {
    const input = [node({ name: "c", size: 1 }), node({ name: "a", size: 1 }), node({ name: "b", size: 1 })];
    expect(names(sortDirectoryNodes(input, by("size", "asc")))).toEqual(["a", "b", "c"]);
    expect(names(sortDirectoryNodes(input, by("size", "desc")))).toEqual(["a", "b", "c"]);
  });

  it("is stable for identical names", () => {
    const first = node({ name: "same" });
    const second = node({ name: "same" });
    expect(sortDirectoryNodes([first, second], by("name", "desc"))[0]).toBe(first);
  });

  it("sorts modified by date, not text", () => {
    const input = [
      node({ name: "new", modified: "2024-06-01T10:00:00" }),
      node({ name: "old", modified: "1999-12-31T23:59:59" }),
      node({ name: "mid", modified: "2010-01-01T00:00:00" }),
    ];
    expect(names(sortDirectoryNodes(input, by("modified", "asc")))).toEqual(["old", "mid", "new"]);
    expect(names(sortDirectoryNodes(input, by("modified", "desc")))).toEqual(["new", "mid", "old"]);
  });

  it("keeps bad dates last in both directions, within their group", () => {
    const input = [
      node({ name: "bad", isModifiedBad: true, modified: "0001-01-01T00:00:00" }),
      node({ name: "old", modified: "2000-01-01T00:00:00" }),
      node({ name: "new", modified: "2020-01-01T00:00:00" }),
      node({ name: "unparseable", modified: "garbage" }),
      node({ name: "dbad", isDirectory: true, isModifiedBad: true }),
      node({ name: "dok", isDirectory: true, modified: "2001-01-01T00:00:00" }),
    ];
    expect(names(sortDirectoryNodes(input, by("modified", "asc")))).toEqual(["dok", "dbad", "old", "new", "bad", "unparseable"]);
    expect(names(sortDirectoryNodes(input, by("modified", "desc")))).toEqual(["dok", "dbad", "new", "old", "bad", "unparseable"]);
  });
});
