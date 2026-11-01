import { describe, expect, it } from "vitest";
import {
  catalogName,
  formatDate,
  mb,
  notOlderThanCutoff,
  parseAge,
  parseSize,
  sizeCell,
  sizeTitle,
  skipSummary,
  toHRString,
  toLocalIsoString,
} from "./format";
// Shared with cdeAppCoreTest's SizeFormatterTests, so both frontends format sizes identically.
import sizeCases from "../../testdata/size-format-cases.json";

describe("toHRString", () => {
  it.each(sizeCases.cases)("$bytes -> $expected", ({ bytes, expected }) => {
    expect(toHRString(bytes)).toBe(expected);
  });
});

describe("sizeCell", () => {
  it("shows a file's size human-readable, without the <Dir> marker", () => {
    expect(sizeCell(false, 50, false)).toBe("50 B");
    expect(sizeCell(false, 1258291, false)).toBe("1.2 MB");
  });

  it("shows a zero-byte file as 0, like an empty directory's size", () => {
    expect(sizeCell(false, 0, false)).toBe("0");
  });

  it("annotates a directory, matching cdeAppCore's FormatDirectorySizeCell", () => {
    expect(sizeCell(true, 0, false)).toBe("0 <Dir>");
    expect(sizeCell(true, 2048, false)).toBe("2 KB <Dir>");
  });

  it("marks a reparse-point directory", () => {
    expect(sizeCell(true, 1024, true)).toBe("1 KB <Dir R>");
  });
});

describe("sizeTitle", () => {
  it("gives the exact byte count, digit-grouped", () => {
    expect(sizeTitle(1258291, "en-US")).toBe("1,258,291 bytes");
    expect(sizeTitle(0, "en-US")).toBe("0 bytes");
  });

  it("uses the singular for one byte", () => {
    expect(sizeTitle(1, "en-US")).toBe("1 byte");
  });
});

describe("formatDate", () => {
  it("formats a local timestamp as yyyy/MM/dd HH:mm:ss", () => {
    expect(formatDate("2026-10-27T09:05:03", false)).toBe("2026/10/27 09:05:03");
  });

  it("shows <Bad Date> when the entry's modified time is flagged bad", () => {
    expect(formatDate("2026-10-27T09:05:03", true)).toBe("<Bad Date>");
  });

  it("shows <Bad Date> for an unparseable timestamp", () => {
    expect(formatDate("not a date", false)).toBe("<Bad Date>");
  });
});

describe("mb", () => {
  it("rounds bytes to whole megabytes", () => {
    expect(mb(5 * 1024 * 1024)).toBe("5 MB");
    expect(mb(5.6 * 1024 * 1024)).toBe("6 MB");
  });
});

describe("parseSize", () => {
  it.each([
    ["100", 100],
    ["25 KB", 25 * 1024],
    ["25kb", 25 * 1024],
    ["2.5 MB", 2.5 * 1024 ** 2],
    ["4GB", 4 * 1024 ** 3],
    [".5 K", 512],
    ["  1 t  ", 1024 ** 4],
  ])("%s -> %d", (text, expected) => {
    expect(parseSize(text)).toBe(expected);
  });

  it.each(["", "   ", "abc", "5 XB", "-1 KB", "1.2.3"])("rejects %j", (text) => {
    expect(parseSize(text)).toBeNull();
  });
});

describe("parseAge", () => {
  it("accepts whole numbers, trimming whitespace", () => {
    expect(parseAge("3")).toBe(3);
    expect(parseAge(" 0 ")).toBe(0);
  });

  it.each(["", "   ", "1.5", "-1", "two"])("rejects %j", (text) => {
    expect(parseAge(text)).toBeNull();
  });
});

describe("notOlderThanCutoff", () => {
  const now = new Date(2026, 0, 15, 12, 30, 45, 500);

  it("steps back hours and days in local time", () => {
    expect(notOlderThanCutoff(3, "hours", now)).toEqual(new Date(2026, 0, 15, 9, 30, 45, 500));
    expect(notOlderThanCutoff(20, "days", now)).toEqual(new Date(2025, 11, 26, 12, 30, 45, 500));
  });

  // Noon, not midnight: in zones with a DST gap at midnight, midnight doesn't exist on that day.
  it("clamps the day to the target month like .NET AddMonths", () => {
    expect(notOlderThanCutoff(1, "months", new Date(2026, 2, 31, 12))).toEqual(new Date(2026, 1, 28, 12));
    expect(notOlderThanCutoff(1, "months", new Date(2024, 2, 31, 12))).toEqual(new Date(2024, 1, 29, 12));
  });

  it("steps back whole years, clamping Feb 29", () => {
    expect(notOlderThanCutoff(1, "years", new Date(2024, 1, 29, 12))).toEqual(new Date(2023, 1, 28, 12));
    expect(notOlderThanCutoff(2, "years", now)).toEqual(new Date(2024, 0, 15, 12, 30, 45, 500));
  });

  it("does not mutate the passed-in date", () => {
    const before = now.getTime();
    notOlderThanCutoff(5, "days", now);
    expect(now.getTime()).toBe(before);
  });
});

describe("toLocalIsoString", () => {
  it("formats local wall-clock time with no offset", () => {
    expect(toLocalIsoString(new Date(2026, 9, 27, 9, 5, 3))).toBe("2026-10-27T09:05:03");
  });
});

describe("catalogName", () => {
  it.each([
    ["D-Backup2024.cdex", "D-Backup2024"],
    ["C-V3Win7-C__users.cde", "C-V3Win7-C__users"],
    ["E:\\catalogs\\D-Photos.cdex", "D-Photos"],
    ["/home/me/catalogs/UNC-toothless_c__users_.CDEX", "UNC-toothless_c__users_"],
    ["sub\\nested/name.cdex", "name"],
    ["no-extension", "no-extension"],
    ["keep.other.ext", "keep.other.ext"],
    ["dotted.name.cdex", "dotted.name"],
  ])("%j -> %j", (fileName, expected) => {
    expect(catalogName(fileName)).toBe(expected);
  });

  it("is empty for an empty or missing file name", () => {
    expect(catalogName("")).toBe("");
    expect(catalogName(null)).toBe("");
    expect(catalogName(undefined)).toBe("");
  });
});

describe("skipSummary", () => {
  it("is null when nothing was skipped (including counts missing from an older sidecar)", () => {
    expect(skipSummary(0, 0)).toBeNull();
    expect(skipSummary(undefined, undefined)).toBeNull();
  });

  it("matches the cde scan wording", () => {
    expect(skipSummary(9, 5)).toBe("skipped 14 folders: 9 excluded, 5 access denied");
  });

  it("singularises a single skipped folder", () => {
    expect(skipSummary(1, 0)).toBe("skipped 1 folder: 1 excluded, 0 access denied");
    expect(skipSummary(0, 1)).toBe("skipped 1 folder: 0 excluded, 1 access denied");
  });

  it("digit-groups large counts", () => {
    expect(skipSummary(1500, 0, "en-US")).toBe("skipped 1,500 folders: 1,500 excluded, 0 access denied");
  });
});
