import { describe, expect, it } from "vitest";
import type { SearchResultRow } from "./api/types";
import { CSV_COLUMN_IDS, csvField, encodeCsv, exportFileName, resultsToCsv } from "./csvExport";

function row(over: Partial<SearchResultRow> = {}): SearchResultRow {
  return {
    ref: { catalogId: 1, entryIndex: 1 },
    name: "a.txt",
    size: 1536,
    modified: "2024-03-05T10:20:30",
    isModifiedBad: false,
    isDirectory: false,
    isReparsePoint: false,
    fullPath: "C:\\dir\\a.txt",
    parentPath: "C:\\dir",
    catalogName: "C-Data",
    ...over,
  };
}

describe("csvField", () => {
  it("leaves plain text unquoted", () => {
    expect(csvField("plain name.txt")).toBe("plain name.txt");
    expect(csvField("")).toBe("");
  });

  it("quotes fields holding a comma, quote or line break, doubling quotes", () => {
    expect(csvField("a,b")).toBe('"a,b"');
    expect(csvField('say "hi"')).toBe('"say ""hi"""');
    expect(csvField("a\nb")).toBe('"a\nb"');
    expect(csvField("a\rb")).toBe('"a\rb"');
  });

  it("passes non-ASCII text through unchanged", () => {
    expect(csvField("naïve 日本語 😀.txt")).toBe("naïve 日本語 😀.txt");
  });
});

describe("resultsToCsv", () => {
  const DEFAULT = ["name", "size", "modified", "catalog", "path"];

  it("writes a header then one CRLF-terminated line per row", () => {
    const csv = resultsToCsv([row(), row({ name: "b.txt", size: 0 })], DEFAULT);
    expect(csv).toBe(
      "Name,Size,Modified,Catalog,Path\r\n" +
        "a.txt,1536,2024-03-05T10:20:30,C-Data,C:\\dir\r\n" +
        "b.txt,0,2024-03-05T10:20:30,C-Data,C:\\dir\r\n",
    );
  });

  it("follows a reordered column order", () => {
    const csv = resultsToCsv([row()], ["path", "catalog", "modified", "size", "name"]);
    expect(csv).toBe("Path,Catalog,Modified,Size,Name\r\nC:\\dir,C-Data,2024-03-05T10:20:30,1536,a.txt\r\n");
  });

  it("reconciles a stale or partial saved order to the known columns", () => {
    const csv = resultsToCsv([row()], ["size", "bogus"]);
    expect(csv.split("\r\n")[0]).toBe("Size,Name,Modified,Catalog,Path");
  });

  it("escapes awkward names", () => {
    const csv = resultsToCsv([row({ name: 'a,"b"\nc' })], ["name"]);
    expect(csv.startsWith('Name,Size,Modified,Catalog,Path\r\n"a,""b""\nc",1536,')).toBe(true);
  });

  it("leaves the modified cell empty for a bad date", () => {
    const csv = resultsToCsv([row({ isModifiedBad: true, modified: "0001-01-01T00:00:00" })], ["modified"]);
    expect(csv.split("\r\n")[1]).toBe(",a.txt,1536,C-Data,C:\\dir");
  });

  it("gives just the header for no rows", () => {
    expect(resultsToCsv([], DEFAULT)).toBe("Name,Size,Modified,Catalog,Path\r\n");
  });

  it("exposes the column ids the results list uses", () => {
    expect(CSV_COLUMN_IDS).toEqual(DEFAULT);
  });
});

describe("encodeCsv", () => {
  it("prefixes a UTF-8 BOM so Excel reads non-ASCII, then the UTF-8 bytes", () => {
    expect([...encodeCsv("é")]).toEqual([0xef, 0xbb, 0xbf, 0xc3, 0xa9]);
  });
});

describe("exportFileName", () => {
  it("is a csv name stamped with the local date and time", () => {
    expect(exportFileName(new Date(2024, 2, 5, 9, 4, 7))).toBe("cde-results-20240305-090407.csv");
  });
});
