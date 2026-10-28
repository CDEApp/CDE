import { describe, expect, it } from "vitest";
import type { LegacyCatalog } from "./api/types";
import {
  dismissFolder,
  fileName,
  legacyFoundText,
  legacyNotice,
  migratingText,
  migrationReport,
  sameFolder,
  shouldPromptLegacy,
  summarizeLegacy,
} from "./legacyMigration";

const cat = (path: string, sizeBytes: number, writable = true): LegacyCatalog => ({ path, sizeBytes, writable });

describe("sameFolder", () => {
  it("ignores case, separator style and a trailing separator", () => {
    expect(sameFolder("C:\\utils\\cde", "c:/Utils/CDE/")).toBe(true);
    expect(sameFolder("C:\\utils\\cde", "C:\\utils\\cde2")).toBe(false);
  });
});

describe("shouldPromptLegacy", () => {
  const legacy = [cat("C:\\utils\\cde\\a.cde", 10)];

  it("prompts when there are legacy catalogs and the folder was not dismissed", () => {
    expect(shouldPromptLegacy(legacy, "C:\\utils\\cde", undefined)).toBe(true);
    expect(shouldPromptLegacy(legacy, "C:\\utils\\cde", ["D:\\other"])).toBe(true);
  });

  it("does not prompt again after Not now for that folder", () => {
    expect(shouldPromptLegacy(legacy, "C:\\utils\\cde", ["c:\\UTILS\\cde\\"])).toBe(false);
  });

  it("does not prompt with no legacy catalogs or no folder", () => {
    expect(shouldPromptLegacy([], "C:\\utils\\cde", undefined)).toBe(false);
    expect(shouldPromptLegacy(legacy, null, undefined)).toBe(false);
  });
});

describe("dismissFolder", () => {
  it("adds the folder once", () => {
    expect(dismissFolder(undefined, "C:\\a")).toEqual(["C:\\a"]);
    expect(dismissFolder(["C:\\a"], "c:\\A\\")).toEqual(["C:\\a"]);
    expect(dismissFolder(["C:\\a"], "C:\\b")).toEqual(["C:\\a", "C:\\b"]);
  });
});

describe("summarizeLegacy / legacyFoundText", () => {
  it("counts, totals and splits by whether the folder is writable", () => {
    const s = summarizeLegacy([cat("a.cde", 3 * 1024 * 1024), cat("b.cde", 1024 * 1024, false)]);
    expect(s.count).toBe(2);
    expect(s.totalBytes).toBe(4 * 1024 * 1024);
    expect(s.writable.map((l) => l.path)).toEqual(["a.cde"]);
    expect(s.readOnly.map((l) => l.path)).toEqual(["b.cde"]);
    expect(legacyFoundText(s, "C:\\utils\\cde")).toBe("Found 2 legacy catalogs (4 MB) in C:\\utils\\cde.");
  });
});

describe("legacyNotice", () => {
  it("names the count, or is null when there is nothing to migrate", () => {
    expect(legacyNotice([])).toBeNull();
    expect(legacyNotice([cat("a.cde", 1)])).toBe("1 legacy catalog not loaded");
    expect(legacyNotice(Array.from({ length: 1117 }, (_, i) => cat(`${i}.cde`, 1)))).toBe(
      "1,117 legacy catalogs not loaded",
    );
  });
});

describe("migratingText / fileName", () => {
  it("shows the position and the file name only", () => {
    expect(fileName("C:\\utils\\cde\\D-2TB M2.cde")).toBe("D-2TB M2.cde");
    expect(fileName("/mnt/x/a.cde")).toBe("a.cde");
    expect(migratingText({ current: 3, total: 117, path: "C:\\utils\\cde\\E.cde" })).toBe("Migrating 3 of 117: E.cde");
  });
});

describe("migrationReport", () => {
  it("summarises, then lists failures before warnings", () => {
    const r = migrationReport({
      migrated: 115,
      total: 117,
      failures: [{ path: "C:\\x\\broken.cde", message: "the catalog could not be loaded" }],
      warnings: [{ path: "C:\\x\\locked.cde", message: "could not rename to locked.cde.migrated: in use" }],
    });
    expect(r.headline).toBe("Migrated 115 of 117 catalogs.");
    expect(r.lines).toEqual([
      "Failed: broken.cde: the catalog could not be loaded",
      "Warning: locked.cde: could not rename to locked.cde.migrated: in use",
    ]);
  });

  it("uses the singular for one catalog", () => {
    expect(migrationReport({ migrated: 1, total: 1, failures: [], warnings: [] }).headline).toBe(
      "Migrated 1 of 1 catalog.",
    );
  });
});
