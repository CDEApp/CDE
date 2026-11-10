import { describe, expect, it } from "vitest";
import type { CatalogInfo, CatalogsResponse } from "./api/types";
import { formatAge, isStale, scannedText, staleCatalogIds, staleNotice, startupRescanIds } from "./freshness";

const NOW = new Date("2026-06-15T12:00:00Z");
const ago = (ms: number) => new Date(NOW.getTime() - ms);
const MIN = 60_000;
const HOUR = 60 * MIN;
const DAY = 24 * HOUR;

const ticksOf = (d: Date) => d.getTime() * 10_000 + 621_355_968_000_000_000;
const cat = (id: number, scanned: Date | null): CatalogInfo =>
  ({ catalogId: id, scanEndUtcTicks: scanned ? ticksOf(scanned) : 0, scanStartUtcTicks: 0 }) as CatalogInfo;

describe("formatAge", () => {
  it.each([
    [0, "just now"],
    [59_000, "just now"],
    [-5 * MIN, "just now"],
    [MIN, "1 minute ago"],
    [45 * MIN, "45 minutes ago"],
    [HOUR, "1 hour ago"],
    [23 * HOUR, "23 hours ago"],
    [DAY, "1 day ago"],
    [3 * DAY, "3 days ago"],
    [29 * DAY, "29 days ago"],
    [30 * DAY, "1 month ago"],
    [75 * DAY, "2 months ago"],
    [364 * DAY, "12 months ago"],
    [365 * DAY, "1 year ago"],
    [800 * DAY, "2 years ago"],
  ])("%d ms old reads %s", (ms, text) => {
    expect(formatAge(ago(ms), NOW)).toBe(text);
  });
});

describe("scannedText", () => {
  it("says when the catalog was scanned, or nothing when that is unknown", () => {
    expect(scannedText(ago(3 * DAY), NOW)).toBe("Scanned 3 days ago");
    expect(scannedText(null, NOW)).toBe("");
  });
});

describe("stale classification", () => {
  it("is stale only past the threshold, so exactly N days old is still fresh", () => {
    expect(isStale(cat(1, ago(30 * DAY)), 30, NOW)).toBe(false);
    expect(isStale(cat(1, ago(30 * DAY + MIN)), 30, NOW)).toBe(true);
  });

  it("is never stale when the threshold is 0, or when the catalog has no scan time", () => {
    expect(isStale(cat(1, ago(900 * DAY)), 0, NOW)).toBe(false);
    expect(isStale(cat(1, null), 30, NOW)).toBe(false);
  });

  it("lists the ids of the stale catalogs only", () => {
    const catalogs = [cat(1, ago(2 * DAY)), cat(2, ago(40 * DAY)), cat(3, ago(31 * DAY)), cat(4, null)];
    expect(staleCatalogIds(catalogs, 30, NOW)).toEqual([2, 3]);
  });
});

describe("staleNotice", () => {
  it("names the count and the threshold, singular or plural, and is absent for none", () => {
    expect(staleNotice(3, 30)).toBe("3 catalogs older than 30 days");
    expect(staleNotice(1, 7)).toBe("1 catalog older than 7 days");
    expect(staleNotice(0, 30)).toBeNull();
  });
});

describe("startupRescanIds", () => {
  const loaded = (rescanStaleOnStartup: boolean): CatalogsResponse =>
    ({ catalogs: [cat(1, ago(2 * DAY)), cat(2, ago(60 * DAY))], staleAfterDays: 30, rescanStaleOnStartup }) as CatalogsResponse;

  it("picks the stale catalogs when the setting is on", () => {
    expect(startupRescanIds(loaded(true), NOW)).toEqual([2]);
  });

  it("picks nothing when the setting is off", () => {
    expect(startupRescanIds(loaded(false), NOW)).toEqual([]);
  });
});
