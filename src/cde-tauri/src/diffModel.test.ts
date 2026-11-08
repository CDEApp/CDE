import { describe, expect, it } from "vitest";
import type { DirectoryNode, SubtreeSummary } from "./api/types";
import { alignChildren, type ChildLists, DIFF_FILTERS, DIFF_MENU_FILTERS, type DiffFilterId, flattenVisible, parentBasePath, relativeSegments } from "./diffModel";

let next = 0;
function node(name: string, over: Partial<DirectoryNode> = {}): DirectoryNode {
  return {
    ref: { catalogId: 1, entryIndex: next++ },
    name,
    fullPath: `C:\\${name}`,
    isDirectory: false,
    hasChildren: false,
    size: 10,
    modified: "2024-01-01T10:00:00Z",
    isModifiedBad: false,
    isReparsePoint: false,
    ...over,
  };
}
const dir = (name: string, size = 100, modified = "2024-01-01T10:00:00Z") =>
  node(name, { isDirectory: true, hasChildren: true, size, modified });

describe("alignChildren", () => {
  it("lists folders first, then names ignoring case", () => {
    const rows = alignChildren([node("b.txt"), dir("Zed"), node("A.txt")], [dir("alpha")], 0, "");
    expect(rows.map((r) => r.name)).toEqual(["alpha", "Zed", "A.txt", "b.txt"]);
  });

  it("matches names case-insensitively and keys by parent path", () => {
    const [row] = alignChildren([node("Readme.TXT")], [node("readme.txt")], 2, "/docs");
    expect(row.key).toBe("/docs/readme.txt");
    expect(row.depth).toBe(2);
    expect(row.left?.name).toBe("Readme.TXT");
    expect(row.right?.name).toBe("readme.txt");
    expect(row.status).toBe("same");
  });

  it("marks names present on one side only", () => {
    const rows = alignChildren([node("l.txt")], [node("r.txt")], 0, "");
    expect(rows.map((r) => [r.name, r.status, r.left === null, r.right === null])).toEqual([
      ["l.txt", "left-only", false, true],
      ["r.txt", "right-only", true, false],
    ]);
  });

  it.each([
    ["equal size and time", {}, "same"],
    ["time within 2 seconds", { modified: "2024-01-01T10:00:02Z" }, "same"],
    ["time beyond 2 seconds", { modified: "2024-01-01T10:00:03Z" }, "different"],
    ["different size", { size: 11 }, "different"],
    ["one bad date", { isModifiedBad: true }, "different"],
  ])("file with %s", (_label, over, status) => {
    const [row] = alignChildren([node("f")], [node("f", over)], 0, "");
    expect(row.status).toBe(status);
  });

  it("treats two bad dates as equal", () => {
    const bad = { isModifiedBad: true };
    expect(alignChildren([node("f", bad)], [node("f", bad)], 0, "")[0].status).toBe("same");
  });

  it("compares a folder on both sides by aggregated size only", () => {
    const same = alignChildren([dir("d", 5)], [{ ...dir("d", 5), modified: "1999-01-01T00:00:00Z" }], 0, "")[0];
    expect(same.status).toBe("same");
    expect(same.isDirectory).toBe(true);
    expect(alignChildren([dir("d", 5)], [dir("d", 6)], 0, "")[0].status).toBe("different");
  });

  it("never merges two entries of the same side that differ only by case", () => {
    const rows = alignChildren([node("Foo.txt"), node("foo.txt")], [], 0, "/p");
    expect(rows.map((r) => [r.name, r.status])).toEqual([["Foo.txt", "left-only"], ["foo.txt", "left-only"]]);
    expect(new Set(rows.map((r) => r.key)).size).toBe(2);
  });

  it("keeps two right-only case-distinct entries without throwing", () => {
    const rows = alignChildren([], [node("Foo.txt"), node("foo.txt")], 0, "/p");
    expect(rows.map((r) => [r.name, r.status, r.left])).toEqual([
      ["Foo.txt", "right-only", null],
      ["foo.txt", "right-only", null],
    ]);
    expect(new Set(rows.map((r) => r.key)).size).toBe(2);
  });

  it("still pairs a single case-different entry on each side", () => {
    const rows = alignChildren([node("Foo.txt")], [node("foo.txt")], 0, "/p");
    expect(rows).toHaveLength(1);
    expect(rows[0].key).toBe("/p/foo.txt");
    expect(rows[0].status).toBe("same");
  });

  it("pairs case-distinct entries by exact name first", () => {
    const rows = alignChildren([node("Foo.txt"), node("foo.txt")], [node("foo.txt")], 0, "/p");
    const paired = rows.find((r) => r.left && r.right);
    const orphan = rows.find((r) => !r.right);
    expect(rows).toHaveLength(2);
    expect(paired?.left?.name).toBe("foo.txt");
    expect(orphan?.left?.name).toBe("Foo.txt");
    expect(orphan?.status).toBe("left-only");
    expect(rows[0].key).not.toBe(rows[1].key);
  });

  it("makes a file-versus-folder name one non-expandable different row", () => {
    const [row] = alignChildren([node("x")], [dir("x")], 0, "");
    expect(row.status).toBe("different");
    expect(row.isDirectory).toBe(false);
  });
});

describe("flattenVisible", () => {
  const roots = alignChildren(
    [dir("same-dir", 5), dir("diff-dir", 5), node("only-left"), node("same-file")],
    [dir("same-dir", 5), dir("diff-dir", 9), node("same-file")],
    0,
    "",
  );
  const children = new Map<string, ChildLists>([
    ["/diff-dir", { left: [node("a"), node("b")], right: [node("a", { size: 99 }), node("c")] }],
    ["/same-dir", { left: [node("k")], right: [node("k")] }],
  ]);
  const names = (rows: { name: string }[]) => rows.map((r) => r.name);
  const flat = (
    expanded: Set<string>,
    kids: Map<string, ChildLists>,
    from: typeof roots,
    filter: DiffFilterId,
    summaries = new Map<string, SubtreeSummary>(),
  ) => flattenVisible(expanded, kids, from, filter, summaries);

  it("shows only the roots while nothing is expanded", () => {
    expect(names(flat(new Set(), children, roots, "all"))).toEqual([
      "diff-dir", "same-dir", "only-left", "same-file",
    ]);
  });

  it("inserts the children of an expanded folder after it", () => {
    const rows = flat(new Set(["/diff-dir"]), children, roots, "all");
    expect(names(rows)).toEqual(["diff-dir", "a", "b", "c", "same-dir", "only-left", "same-file"]);
    expect(rows.find((r) => r.name === "a")?.depth).toBe(1);
  });

  it("filters diffs, keeping both-sides folders but hiding their same children", () => {
    const rows = flat(new Set(["/diff-dir", "/same-dir"]), children, roots, "differences");
    expect(names(rows)).toEqual(["diff-dir", "a", "b", "c", "same-dir", "only-left"]);
  });

  it("filters same, keeping both-sides folders but hiding their different children", () => {
    const rows = flat(new Set(["/diff-dir", "/same-dir"]), children, roots, "same");
    expect(names(rows)).toEqual(["diff-dir", "same-dir", "k", "same-file"]);
  });

  it("keeps a both-sides folder under Left Orphans so its left-only file is reachable", () => {
    const rows = flat(new Set(["/diff-dir"]), children, roots, "leftOrphans");
    expect(names(rows)).toEqual(["diff-dir", "b", "same-dir", "only-left"]);
  });

  it("keeps a both-sides folder under Left Newer when the folder is not itself newer", () => {
    const newer = new Map<string, ChildLists>([
      ["/diff-dir", { left: [node("n", { modified: "2024-01-01T12:00:00Z" })], right: [node("n")] }],
    ]);
    const rows = flat(new Set(["/diff-dir"]), newer, roots, "leftNewer");
    expect(names(rows)).toEqual(["diff-dir", "n", "same-dir"]);
  });

  it("still filters a one-sided folder by its own status", () => {
    const r = alignChildren([dir("lonely")], [], 0, "");
    expect(names(flat(new Set(), new Map(), r, "rightOrphans"))).toEqual([]);
    expect(names(flat(new Set(), new Map(), r, "leftOrphans"))).toEqual(["lonely"]);
  });

  it("adds no children for an expanded folder that is not loaded yet", () => {
    expect(names(flat(new Set(["/diff-dir"]), new Map(), roots, "all"))).toEqual([
      "diff-dir", "same-dir", "only-left", "same-file",
    ]);
  });

  describe("with exact subtree summaries", () => {
    const summary = (over: Partial<SubtreeSummary> = {}): SubtreeSummary => ({
      sameFiles: 0,
      differentFiles: 0,
      leftNewerFiles: 0,
      rightNewerFiles: 0,
      leftOnly: 0,
      rightOnly: 0,
      sameFolders: 0,
      differentFolders: 0,
      leftNewerFolders: 0,
      rightNewerFolders: 0,
      ...over,
    });
    const identical = summary({ sameFiles: 3 });
    const pair = alignChildren([dir("a", 5), dir("b", 5)], [dir("a", 5), dir("b", 5)], 0, "");

    it("hides an identical folder under Diffs but shows it under All and while its summary is missing", () => {
      const sums = new Map([["/a", identical], ["/b", summary({ differentFiles: 1 })]]);
      expect(names(flat(new Set(), new Map(), pair, "differences", sums))).toEqual(["b"]);
      expect(names(flat(new Set(), new Map(), pair, "all", sums))).toEqual(["a", "b"]);
      expect(names(flat(new Set(), new Map(), pair, "differences"))).toEqual(["a", "b"]);
      expect(names(flat(new Set(), new Map(), pair, "differences", new Map([["/b", summary({ differentFiles: 1 })]])))).toEqual(["a", "b"]);
    });

    it("keeps a folder with one-sided entries under its side's orphan filter only", () => {
      const sums = new Map([["/a", summary({ sameFiles: 2, leftOnly: 1 })], ["/b", identical]]);
      expect(names(flat(new Set(), new Map(), pair, "leftOrphans", sums))).toEqual(["a"]);
      expect(names(flat(new Set(), new Map(), pair, "rightOrphans", sums))).toEqual([]);
    });

    it("treats a folder of equal size but a differing file as different and shows it under Diffs", () => {
      const sums = new Map([["/a", summary({ sameFiles: 4, differentFiles: 1 })], ["/b", identical]]);
      const rows = flat(new Set(), new Map(), pair, "differencesNoOrphans", sums);
      expect(rows.map((r) => [r.name, r.status])).toEqual([["a", "different"]]);
    });

    it("keeps a parent whose only match is a nested matching folder, and hides one with none", () => {
      const sums = new Map([
        ["/a", summary({ differentFiles: 1, sameFolders: 1 })],
        ["/b", summary({ differentFiles: 1 })],
      ]);
      expect(names(flat(new Set(), new Map(), pair, "same", sums))).toEqual(["a"]);
      expect(names(flat(new Set(), new Map(), pair, "noOrphans", sums))).toEqual(["a", "b"]);
    });

    it("emits the effective status without mutating the input row", () => {
      const sums = new Map([["/a", summary({ leftOnly: 1 })], ["/b", identical]]);
      const rows = flat(new Set(), new Map(), pair, "all", sums);
      expect(rows.map((r) => r.status)).toEqual(["different", "same"]);
      expect(pair.map((r) => r.status)).toEqual(["same", "same"]);
      expect(rows[1]).toBe(pair[1]);
    });

    it("keeps an expanded container whose descendants match and filters its children as before", () => {
      const sums = new Map([["/diff-dir", summary({ differentFiles: 1, leftOnly: 1 })], ["/same-dir", identical]]);
      const rows = flat(new Set(["/diff-dir", "/same-dir"]), children, roots, "differences", sums);
      expect(names(rows)).toEqual(["diff-dir", "a", "b", "c", "only-left"]);
    });
  });
});

describe("DIFF_FILTERS.belowMatches", () => {
  const T0 = "2024-01-01T10:00:00Z";
  const T1 = "2024-01-01T11:00:00Z";
  const kinds = [
    "same",
    "leftNewerFiles",
    "rightNewerFiles",
    "otherDifferent",
    "leftOnly",
    "rightOnly",
    "sameFolder",
    "otherDifferentFolder",
    "leftNewerFolder",
    "rightNewerFolder",
  ] as const;
  type Case = Record<(typeof kinds)[number], number>;

  // Every combination of "is there at least one entry of this kind below the folder" (2^10 of them).
  const cases: Case[] = Array.from({ length: 1 << kinds.length }, (_, mask) =>
    Object.fromEntries(kinds.map((k, bit) => [k, (mask >> bit) & 1])) as Case,
  );

  // A nested both-sides folder row as flattenVisible emits it once its summary is known: identical
  // subtree means "same", anything else "different", and its own modified times drive the newer filters.
  function folderPair(name: string, status: "same" | "different", leftModified: string, rightModified: string) {
    const [row] = alignChildren([dir(name, 5, leftModified)], [dir(name, 5, rightModified)], 1, "/f");
    return { ...row, status };
  }

  function descendants(c: Case) {
    const lefts: DirectoryNode[] = [];
    const rights: DirectoryNode[] = [];
    if (c.same) { lefts.push(node("same")); rights.push(node("same")); }
    if (c.leftNewerFiles) { lefts.push(node("ln", { modified: T1 })); rights.push(node("ln", { modified: T0 })); }
    if (c.rightNewerFiles) { lefts.push(node("rn", { modified: T0 })); rights.push(node("rn", { modified: T1 })); }
    if (c.otherDifferent) { lefts.push(node("od", { size: 11 })); rights.push(node("od", { size: 12 })); }
    if (c.leftOnly) lefts.push(node("lo"));
    if (c.rightOnly) rights.push(node("ro"));
    const rows = alignChildren(lefts, rights, 1, "/f");
    if (c.sameFolder) rows.push(folderPair("sf", "same", T0, T0));
    if (c.otherDifferentFolder) rows.push(folderPair("df", "different", T0, T0));
    if (c.leftNewerFolder) rows.push(folderPair("lnf", "different", T1, T0));
    if (c.rightNewerFolder) rows.push(folderPair("rnf", "different", T0, T1));
    return rows;
  }

  function summaryOf(c: Case): SubtreeSummary {
    return {
      sameFiles: c.same,
      differentFiles: c.leftNewerFiles + c.rightNewerFiles + c.otherDifferent,
      leftNewerFiles: c.leftNewerFiles,
      rightNewerFiles: c.rightNewerFiles,
      leftOnly: c.leftOnly,
      rightOnly: c.rightOnly,
      sameFolders: c.sameFolder,
      differentFolders: c.otherDifferentFolder + c.leftNewerFolder + c.rightNewerFolder,
      leftNewerFolders: c.leftNewerFolder,
      rightNewerFolders: c.rightNewerFolder,
    };
  }

  it.each(Object.keys(DIFF_FILTERS) as DiffFilterId[])("%s agrees with shows on synthesized descendants", (id) => {
    // An empty folder has no descendant row at all; "all" keeps it anyway, so it is outside the comparison.
    for (const c of cases.filter((k) => Object.values(k).some(Boolean))) {
      const expected = descendants(c).some(DIFF_FILTERS[id].shows);
      expect(DIFF_FILTERS[id].belowMatches(summaryOf(c)), JSON.stringify(c)).toBe(expected);
    }
  });
});

describe("DIFF_FILTERS", () => {
  it("gives every Diffs menu mode its own short name", () => {
    const shorts = DIFF_MENU_FILTERS.map((id) => DIFF_FILTERS[id].short);
    expect(new Set(shorts).size).toBe(DIFF_MENU_FILTERS.length);
    expect(DIFF_FILTERS.differences.short).toBe("Differences");
    expect(DIFF_FILTERS.differencesNoOrphans.short).toBe("Differences, No Orphans");
  });

  const T0 = "2024-01-01T10:00:00Z";
  const T1 = "2024-01-01T11:00:00Z";
  const rows = alignChildren(
    [
      node("same"),
      node("leftNewer", { modified: T1 }),
      node("rightNewer"),
      node("sizeOnly", { size: 11 }),
      node("badDate", { isModifiedBad: true }),
      node("leftOnly"),
    ],
    [
      node("same"),
      node("leftNewer", { modified: T0 }),
      node("rightNewer", { modified: T1 }),
      node("sizeOnly", { size: 12 }),
      node("badDate"),
      node("rightOnly"),
    ],
    0,
    "",
  );

  const shown = (id: DiffFilterId) =>
    rows.filter(DIFF_FILTERS[id].shows).map((r) => r.name).sort();

  it.each<[DiffFilterId, string[]]>([
    ["all", ["badDate", "leftNewer", "leftOnly", "rightNewer", "rightOnly", "same", "sizeOnly"]],
    ["same", ["same"]],
    ["differences", ["badDate", "leftNewer", "leftOnly", "rightNewer", "rightOnly", "sizeOnly"]],
    ["noOrphans", ["badDate", "leftNewer", "rightNewer", "same", "sizeOnly"]],
    ["differencesNoOrphans", ["badDate", "leftNewer", "rightNewer", "sizeOnly"]],
    ["orphans", ["leftOnly", "rightOnly"]],
    ["leftNewer", ["leftNewer"]],
    ["rightNewer", ["rightNewer"]],
    ["leftNewerLeftOrphans", ["leftNewer", "leftOnly"]],
    ["rightNewerRightOrphans", ["rightNewer", "rightOnly"]],
    ["leftOrphans", ["leftOnly"]],
    ["rightOrphans", ["rightOnly"]],
  ])("%s", (id, expected) => {
    expect(shown(id)).toEqual(expected);
  });

  it("lists the ten menu entries in order with their labels", () => {
    expect(DIFF_MENU_FILTERS.map((id) => DIFF_FILTERS[id].label)).toEqual([
      "Show Differences",
      "Show No Orphans",
      "Show Differences but No Orphans",
      "Show Orphans",
      "Show Left Newer",
      "Show Right Newer",
      "Show Left Newer and Left Orphans",
      "Show Right Newer and Right Orphans",
      "Show Left Orphans",
      "Show Right Orphans",
    ]);
  });
});

describe("relativeSegments", () => {
  it.each([
    ["C:\\", "C:\\Users\\Bob", ["Users", "Bob"]],
    ["C:\\data", "c:/DATA/x\\y/", ["x", "y"]],
    ["/", "/usr/lib", ["usr", "lib"]],
    ["/home/me", "/home/me", []],
    ["C:\\data", "C:\\other", null],
    ["C:\\data\\deep", "C:\\data", null],
  ])("%s -> %s", (root, path, expected) => {
    expect(relativeSegments(root, path)).toEqual(expected);
  });
});

describe("parentBasePath", () => {
  it("goes up one folder, ending at the catalog root as written", () => {
    expect(parentBasePath("C:\\", "C:\\Users\\me")).toBe("C:\\Users");
    expect(parentBasePath("C:\\", "C:\\Users")).toBe("C:\\");
    expect(parentBasePath("D:\\data", "D:\\data\\a\\b\\")).toBe("D:\\data\\a");
    expect(parentBasePath("/mnt/data", "/mnt/data/a/b")).toBe("/mnt/data/a");
    expect(parentBasePath("/", "/home")).toBe("/");
  });

  it("is null at the catalog root or outside it", () => {
    expect(parentBasePath("C:\\", "C:\\")).toBeNull();
    expect(parentBasePath("D:\\data", "d:\\DATA")).toBeNull();
    expect(parentBasePath("D:\\data", "E:\\other")).toBeNull();
  });
});
