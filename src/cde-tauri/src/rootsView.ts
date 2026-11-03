import type { TreeRoot } from "./treeModel";

export type RootSortKey = "root" | "catalog" | "size";
export type RootSortDirection = "asc" | "desc";
/** null is the incoming (server) order. */
export type RootSort = { key: RootSortKey; direction: RootSortDirection } | null;

/** Choosing a key from the sort box: size starts largest first, the text keys start A to Z. */
export function sortForKey(key: RootSortKey | null): RootSort {
  if (key === null) return null;
  return { key, direction: key === "size" ? "desc" : "asc" };
}

export function flipDirection(sort: RootSort): RootSort {
  return sort && { key: sort.key, direction: sort.direction === "asc" ? "desc" : "asc" };
}

// Ordinal ignore-case on upper-cased keys, as directorySort.ts does.
const upper = (s: string) => s.toUpperCase();
const compareKeys = (x: string, y: string) => (x < y ? -1 : x > y ? 1 : 0);

/**
 * The roots to show: those whose root label/path or catalog name contains the trimmed filter (case
 * insensitive), ordered by `sort`. Ties fall back to root label, then incoming order (stable sort).
 * Descending reverses the primary key only. Returns a new array.
 */
export function visibleRoots(roots: readonly TreeRoot[], sort: RootSort, filter: string): TreeRoot[] {
  const needle = upper(filter.trim());
  const kept = needle
    ? roots.filter((r) => [r.label, r.fullPath, r.catalogName].some((s) => upper(s).includes(needle)))
    : [...roots];
  if (!sort) return kept;

  const sign = sort.direction === "asc" ? 1 : -1;
  const primary = (a: TreeRoot, b: TreeRoot) => {
    switch (sort.key) {
      case "root":
        return compareKeys(upper(a.label), upper(b.label));
      case "catalog":
        return compareKeys(upper(a.catalogName), upper(b.catalogName));
      case "size":
        return a.size - b.size;
    }
  };
  return kept.sort((a, b) => sign * primary(a, b) || compareKeys(upper(a.label), upper(b.label)));
}
