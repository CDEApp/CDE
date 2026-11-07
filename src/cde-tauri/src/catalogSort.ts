import type { CatalogInfo } from "./api/types";
import { catalogCreated, catalogModified } from "./catalogColumns";
import { catalogFile, fileBaseName } from "./format";

export type CatalogSortColumn = "root" | "volume" | "dirs" | "files" | "size" | "free" | "drive" | "catalog" | "description" | "created" | "modified";
export type CatalogSortDirection = "asc" | "desc";
/** null = the order the catalogs arrived in (the initial state). */
export type CatalogSort = { column: CatalogSortColumn; direction: CatalogSortDirection } | null;

/** Header click: a new column (or no sort yet) starts ascending; the same column flips direction. */
export function nextCatalogSort(current: CatalogSort, clicked: CatalogSortColumn): CatalogSort {
  if (current?.column === clicked) {
    return { column: clicked, direction: current.direction === "asc" ? "desc" : "asc" };
  }
  return { column: clicked, direction: "asc" };
}

/**
 * Catalogs whose root, volume, drive or catalog file name contains the trimmed filter (case
 * insensitive). A blank filter keeps everything. Returns a new array.
 */
export function filterCatalogs(catalogs: readonly CatalogInfo[], filter: string): CatalogInfo[] {
  const needle = filter.trim().toUpperCase();
  if (!needle) return catalogs.slice();
  return catalogs.filter((c) =>
    [c.rootPath, c.volumeName, c.driveLetterHint, fileBaseName(catalogFile(c))].some((s) =>
      (s ?? "").toUpperCase().includes(needle),
    ),
  );
}

function compareKeys(x: string, y: string): number {
  return x < y ? -1 : x > y ? 1 : 0;
}

type Key = string | number;

// Strings are upper-cased for an ordinal ignore-case compare, like the directory listing.
function keyOf(c: CatalogInfo, column: CatalogSortColumn): Key {
  switch (column) {
    case "root":
      return (c.rootPath ?? "").toUpperCase();
    case "volume":
      return (c.volumeName ?? "").toUpperCase();
    case "drive":
      return (c.driveLetterHint ?? "").toUpperCase();
    case "catalog":
      return fileBaseName(catalogFile(c)).toUpperCase();
    case "dirs":
      return c.dirEntryCount;
    case "files":
      return c.fileEntryCount;
    case "size":
      return c.rootSize;
    case "free":
      return c.availSpace ?? 0;
    case "description":
      return (c.description ?? "").toUpperCase();
    case "created":
      return catalogCreated(c)?.getTime() ?? 0;
    case "modified":
      return catalogModified(c)?.getTime() ?? 0;
  }
}

function compareValues(a: Key, b: Key): number {
  return typeof a === "number" && typeof b === "number" ? a - b : compareKeys(String(a), String(b));
}

/**
 * Sorted copy of the catalog list. Counts and size compare numerically. Descending reverses the
 * chosen column only: ties fall back to root path ascending, then original order (Array.sort is
 * stable).
 */
export function sortCatalogs(catalogs: readonly CatalogInfo[], sort: CatalogSort): CatalogInfo[] {
  if (!sort) return catalogs.slice();
  const sign = sort.direction === "asc" ? 1 : -1;
  const rows = catalogs.map((c) => ({ c, key: keyOf(c, sort.column), root: keyOf(c, "root") as string }));
  rows.sort((a, b) => sign * compareValues(a.key, b.key) || compareKeys(a.root, b.root));
  return rows.map((r) => r.c);
}
