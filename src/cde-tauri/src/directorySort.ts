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

// Ordinal ignore-case, like the server's StringComparer.OrdinalIgnoreCase (upper-cased code units).
function compareNames(a: string, b: string): number {
  const x = a.toUpperCase();
  const y = b.toUpperCase();
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
  return [...nodes].sort((a, b) => {
    if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
    let c = 0;
    switch (sort.column) {
      case "name":
        c = sign * compareNames(a.name, b.name);
        break;
      case "size":
        c = sign * (a.size - b.size);
        break;
      case "modified": {
        const ma = modifiedMs(a);
        const mb = modifiedMs(b);
        if (ma === null || mb === null) {
          if (ma === mb) break;
          return ma === null ? 1 : -1;
        }
        c = sign * (ma - mb);
        break;
      }
    }
    return c !== 0 ? c : compareNames(a.name, b.name);
  });
}
