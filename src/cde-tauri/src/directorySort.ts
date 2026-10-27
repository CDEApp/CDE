import type { DirectoryNode } from "./api/types";

export type SortColumn = "name" | "size" | "modified";
export type SortDirection = "asc" | "desc";
export interface DirectorySort {
  column: SortColumn;
  direction: SortDirection;
}

/** Matches the server's listing order (folders first, then name, ordinal ignore-case). */
export const DEFAULT_SORT: DirectorySort = { column: "name", direction: "asc" };

/** Header click: same column flips direction; a new column starts ascending. */
export function nextSort(current: DirectorySort, clicked: SortColumn): DirectorySort {
  if (current.column === clicked) {
    return { column: clicked, direction: current.direction === "asc" ? "desc" : "asc" };
  }
  return { column: clicked, direction: "asc" };
}

// Ordinal ignore-case, like the server's StringComparer.OrdinalIgnoreCase: compare upper-cased
// keys by code unit. Keys are computed once per sort, not per comparison.
function compareKeys(x: string, y: string): number {
  return x < y ? -1 : x > y ? 1 : 0;
}

/** Modified time in ms, or null when the entry's date is bad/unparseable. */
function modifiedMs(n: DirectoryNode): number | null {
  if (n.isModifiedBad) return null;
  const t = new Date(n.modified).getTime();
  return Number.isNaN(t) ? null : t;
}

/**
 * Sorted copy of a folder listing. Folders always precede files; the chosen column orders each
 * group. Entries with a bad modified date always sort last within their group (both directions).
 * Ties fall back to name ascending, then original order (Array.sort is stable).
 */
export function sortDirectoryNodes(nodes: readonly DirectoryNode[], sort: DirectorySort): DirectoryNode[] {
  const sign = sort.direction === "asc" ? 1 : -1;
  // Decorate once so a large folder doesn't upper-case names or parse dates on every comparison.
  const rows = nodes.map((n) => ({
    n,
    key: n.name.toUpperCase(),
    ms: sort.column === "modified" ? modifiedMs(n) : null,
  }));
  rows.sort((a, b) => {
    if (a.n.isDirectory !== b.n.isDirectory) return a.n.isDirectory ? -1 : 1;
    let c = 0;
    switch (sort.column) {
      case "name":
        c = sign * compareKeys(a.key, b.key);
        break;
      case "size":
        c = sign * (a.n.size - b.n.size);
        break;
      case "modified":
        if (a.ms === null || b.ms === null) {
          if (a.ms === b.ms) break;
          return a.ms === null ? 1 : -1;
        }
        c = sign * (a.ms - b.ms);
        break;
    }
    return c !== 0 ? c : compareKeys(a.key, b.key);
  });
  return rows.map((r) => r.n);
}
