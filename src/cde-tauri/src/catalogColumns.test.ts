import { describe, expect, it } from "vitest";
import type { CatalogInfo } from "./api/types";
import {
  catalogCreated,
  catalogModified,
  catalogTotalWidth,
  catalogWidthOf,
  clampCatalogWidth,
  normalizeCatalogWidths,
  ticksToDate,
} from "./catalogColumns";

const EPOCH_TICKS = 621_355_968_000_000_000;
const minute = 600_000_000;
const at = (iso: string) => EPOCH_TICKS + Date.parse(iso) * 10_000;

function cat(scanEnd: number, fileModified: number): CatalogInfo {
  return { scanStartUtcTicks: scanEnd, scanEndUtcTicks: scanEnd, fileModifiedUtcTicks: fileModified } as CatalogInfo;
}

describe("catalog dates", () => {
  it("converts .NET ticks and rejects zero", () => {
    expect(ticksToDate(at("2026-10-27T11:12:46Z"))?.toISOString()).toBe("2026-10-27T11:12:46.000Z");
    expect(ticksToDate(0)).toBeNull();
  });

  it("created is the scan time; unknown when never recorded", () => {
    expect(catalogCreated(cat(at("2026-01-02T03:04:05Z"), 0))?.toISOString()).toBe("2026-01-02T03:04:05.000Z");
    expect(catalogCreated(cat(0, 0))).toBeNull();
  });

  it("modified is the file's last write; unknown when not recorded", () => {
    const scan = at("2026-01-02T03:04:05Z");
    expect(catalogModified(cat(scan, 0))).toBeNull();
    expect(catalogModified(cat(scan, scan + 30 * minute))?.getTime()).toBe(Date.parse("2026-01-02T03:34:05Z"));
  });
});

describe("catalog column widths", () => {
  it("drops invalid entries, clamps the rest and falls back to defaults", () => {
    const w = normalizeCatalogWidths({ root: 300, size: "wide", created: 1, bogus: 5, dirs: -4 });
    expect(w).toEqual({ root: 300, created: 40 });
    expect(catalogWidthOf(w, "root")).toBe(300);
    expect(catalogWidthOf(w, "dirs")).toBe(80);
    expect(normalizeCatalogWidths(null)).toEqual({});
  });

  it("clamps and sums", () => {
    expect(clampCatalogWidth(5)).toBe(40);
    expect(clampCatalogWidth(99999)).toBe(2000);
    expect(catalogTotalWidth({})).toBe(240 + 140 + 80 + 90 + 90 + 90 + 60 + 240 + 220 + 150 + 150);
  });
});
