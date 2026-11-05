import type { CatalogInfo } from "./api/types";
import type { CatalogSortColumn } from "./catalogSort";

/** Catalog-list columns whose width the user can set (same ids as the sort columns). */
export type CatalogColumnWidths = Partial<Record<CatalogSortColumn, number>>;

export const CATALOG_COLUMN_IDS: readonly CatalogSortColumn[] = [
  "root", "volume", "dirs", "files", "size", "drive", "catalog", "description", "created", "modified",
];

export const CATALOG_DEFAULT_WIDTH: Record<CatalogSortColumn, number> = {
  root: 240, volume: 140, dirs: 80, files: 90, size: 90, drive: 60, catalog: 240, description: 220, created: 150, modified: 150,
};

export const CATALOG_MIN_WIDTH = 40;
export const CATALOG_MAX_WIDTH = 2000;

export function clampCatalogWidth(width: number): number {
  return Math.round(Math.max(CATALOG_MIN_WIDTH, Math.min(CATALOG_MAX_WIDTH, width)));
}

/** Drops unknown ids and non-finite or non-positive values, and clamps the rest, so a stale ui-state can't break the table. */
export function normalizeCatalogWidths(saved: unknown): CatalogColumnWidths {
  const out: CatalogColumnWidths = {};
  if (typeof saved !== "object" || saved === null || Array.isArray(saved)) return out;
  const record = saved as Record<string, unknown>;
  for (const id of CATALOG_COLUMN_IDS) {
    const v = record[id];
    if (typeof v === "number" && Number.isFinite(v) && v > 0) out[id] = clampCatalogWidth(v);
  }
  return out;
}

export function catalogWidthOf(widths: CatalogColumnWidths, id: CatalogSortColumn): number {
  return widths[id] ?? CATALOG_DEFAULT_WIDTH[id];
}

export function catalogTotalWidth(widths: CatalogColumnWidths): number {
  return CATALOG_COLUMN_IDS.reduce((sum, id) => sum + catalogWidthOf(widths, id), 0);
}

const TICKS_AT_UNIX_EPOCH = 621_355_968_000_000_000;
const TICKS_PER_MS = 10_000;

/** .NET UTC ticks to a Date; null for 0 or anything that is not a real time. */
export function ticksToDate(ticks: number | undefined): Date | null {
  if (!ticks || ticks <= TICKS_AT_UNIX_EPOCH) return null;
  const d = new Date((ticks - TICKS_AT_UNIX_EPOCH) / TICKS_PER_MS);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** When the catalog was scanned: the only time every catalog records. */
export function catalogCreated(c: CatalogInfo): Date | null {
  return ticksToDate(c.scanEndUtcTicks) ?? ticksToDate(c.scanStartUtcTicks);
}

/** When the catalog file was last written (a scan, a hash run, a description edit); null when unknown. */
export function catalogModified(c: CatalogInfo): Date | null {
  return ticksToDate(c.fileModifiedUtcTicks);
}
