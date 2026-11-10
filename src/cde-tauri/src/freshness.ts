import type { CatalogInfo, CatalogsResponse } from "./api/types";
import { catalogCreated } from "./catalogColumns";

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

const plural = (n: number, unit: string) => `${n} ${unit}${n === 1 ? "" : "s"} ago`;

/** "3 days ago": the largest whole unit that fits; anything under a minute (or in the future) is "just now". */
export function formatAge(date: Date, now: Date): string {
  const ms = now.getTime() - date.getTime();
  if (ms < MINUTE) return "just now";
  if (ms < HOUR) return plural(Math.floor(ms / MINUTE), "minute");
  if (ms < DAY) return plural(Math.floor(ms / HOUR), "hour");
  const days = Math.floor(ms / DAY);
  if (days < 30) return plural(days, "day");
  if (days < 365) return plural(Math.floor(days / 30), "month");
  return plural(Math.floor(days / 365), "year");
}

/** "Scanned 3 days ago", or "" when the catalog records no scan time. */
export function scannedText(scannedAt: Date | null | undefined, now: Date): string {
  return scannedAt ? `Scanned ${formatAge(scannedAt, now)}` : "";
}

/** Older than `staleAfterDays` days; 0 turns staleness off and a catalog with no scan time is never stale. */
export function isStale(c: CatalogInfo, staleAfterDays: number, now: Date): boolean {
  const scanned = catalogCreated(c);
  return staleAfterDays > 0 && scanned !== null && now.getTime() - scanned.getTime() > staleAfterDays * DAY;
}

export function staleCatalogIds(catalogs: readonly CatalogInfo[], staleAfterDays: number, now: Date): number[] {
  return catalogs.filter((c) => isStale(c, staleAfterDays, now)).map((c) => c.catalogId);
}

/** The status bar text for `count` stale catalogs, or null when there are none. */
export function staleNotice(count: number, staleAfterDays: number): string | null {
  return count > 0 ? `${count} ${count === 1 ? "catalog" : "catalogs"} older than ${staleAfterDays} days` : null;
}

/** Catalogs to rescan right after the first load: the stale ones, and only when the setting is on. */
export function startupRescanIds(loaded: CatalogsResponse, now: Date): number[] {
  return loaded.rescanStaleOnStartup ? staleCatalogIds(loaded.catalogs, loaded.staleAfterDays, now) : [];
}
