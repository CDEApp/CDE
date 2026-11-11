import { describe, expect, it } from "vitest";
import type { SearchQuery, SearchResultRow } from "./api/types";
import { combineQuery, compileFilter, countText } from "./resultsFilter";

const row = (name: string, fullPath: string): SearchResultRow => ({
  ref: { catalogId: 1, entryIndex: 0 },
  name,
  size: 1,
  modified: "",
  isModifiedBad: false,
  isDirectory: false,
  isReparsePoint: false,
  fullPath,
  parentPath: "",
  catalogName: "cat",
});
const A = row("Report.TXT", "C:\\docs\\Report.TXT");
const B = row("notes.md", "C:\\reports\\notes.md");

function matches(text: string, mode: "substring" | "regex", field: "name" | "path") {
  const f = compileFilter(text, mode, field);
  if (f.kind !== "ok") throw new Error(f.kind);
  return [A, B].filter(f.test).map((r) => r.name);
}

describe("compileFilter", () => {
  it("blank text is no filter", () => {
    expect(compileFilter("", "substring", "name").kind).toBe("none");
    expect(compileFilter("", "regex", "path").kind).toBe("none");
  });

  it("matches a substring of the name regardless of case", () => {
    expect(matches("report", "substring", "name")).toEqual(["Report.TXT"]);
    expect(matches("TXT", "substring", "name")).toEqual(["Report.TXT"]);
  });

  it("matches the full path in path mode, and not the path in name mode", () => {
    expect(matches("c:\\docs", "substring", "path")).toEqual(["Report.TXT"]);
    expect(matches("reports", "substring", "path")).toEqual(["notes.md"]);
    expect(matches("docs", "substring", "name")).toEqual([]);
  });

  it("treats regex metacharacters as literal in substring mode", () => {
    expect(matches(".md", "substring", "name")).toEqual(["notes.md"]);
    expect(matches("(", "substring", "name")).toEqual([]);
  });

  it("matches a regex, ignoring case", () => {
    expect(matches("^report\\.(txt|doc)$", "regex", "name")).toEqual(["Report.TXT"]);
    expect(matches("\\\\reports\\\\", "regex", "path")).toEqual(["notes.md"]);
  });

  it("reports an invalid regex instead of throwing", () => {
    const f = compileFilter("(unclosed", "regex", "name");
    expect(f.kind).toBe("error");
    expect(f.kind === "error" && f.message.length).toBeGreaterThan(0);
  });
});

describe("countText", () => {
  it("says how many of the loaded rows are shown", () => {
    expect(countText(412, 10000)).toBe(`412 of ${(10000).toLocaleString()} shown`);
    expect(countText(0, 5)).toBe("0 of 5 shown");
  });
});

describe("combineQuery", () => {
  const base: SearchQuery = { pattern: "", limitResultCount: 500, fromSizeEnable: true, fromSize: 10 };

  it("keeps the other options and switches to Everything syntax", () => {
    const q = combineQuery({ ...base, pattern: "a", regexMode: true, includePath: true }, "b", "substring", "name");
    expect(q).toMatchObject({
      everythingMode: true,
      regexMode: false,
      includePath: false,
      limitResultCount: 500,
      fromSizeEnable: true,
      fromSize: 10,
    });
  });

  it("with no previous pattern, is just the filter term", () => {
    expect(combineQuery(base, "foo", "substring", "name").pattern).toBe("foo");
    expect(combineQuery(base, "foo", "substring", "path").pattern).toBe("path:foo");
    expect(combineQuery(base, "^a.*b$", "regex", "name").pattern).toBe('regex:"^a.*b$"');
    expect(combineQuery(base, "x", "regex", "path").pattern).toBe('path:regex:"x"');
  });

  it("groups an Everything pattern so an OR in it stays inside", () => {
    const q = combineQuery({ ...base, pattern: "a | b", everythingMode: true }, "foo", "substring", "name");
    expect(q.pattern).toBe("<a | b> foo");
  });

  it("rewrites a plain pattern as a literal name or path term", () => {
    expect(combineQuery({ ...base, pattern: "my file" }, "foo", "substring", "name").pattern).toBe('"my file" foo');
    expect(combineQuery({ ...base, pattern: "tmp", includePath: true }, "foo", "substring", "name").pattern).toBe("path:tmp foo");
  });

  it("rewrites a regex pattern with regex:", () => {
    expect(combineQuery({ ...base, pattern: "a.+z", regexMode: true }, "foo", "substring", "name").pattern).toBe('regex:"a.+z" foo');
  });

  it("puts a literal filter with a wildcard or quote into regex form", () => {
    expect(combineQuery(base, "a*b", "substring", "name").pattern).toBe('regex:"a\\*b"');
    expect(combineQuery(base, 'say "hi"', "substring", "name").pattern).toBe('regex:"say \\x22hi\\x22"');
    expect(combineQuery(base, 'a"b', "regex", "name").pattern).toBe('regex:"a\\x22b"');
  });

  it("keeps a files-only or folders-only plain search as file: or folder:", () => {
    expect(combineQuery({ ...base, pattern: "a", includeFolders: false }, "b", "substring", "name").pattern).toBe("a file: b");
    expect(combineQuery({ ...base, pattern: "a", includeFiles: false }, "b", "substring", "name").pattern).toBe("a folder: b");
  });
});

describe("combineQuery quotes in a regex filter", () => {
  const last = { pattern: "", everythingMode: true } as SearchQuery;
  it("turns a plain or escaped quote into \\x22 and keeps other escapes", () => {
    expect(combineQuery(last, String.raw`a"b\"c\d`, "regex", "name").pattern).toBe(String.raw`regex:"a\x22b\x22c\d"`);
  });
});
