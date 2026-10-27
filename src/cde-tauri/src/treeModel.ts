import { DirectoryNode, EntryRef, refToString } from "./api/types";

/**
 * The directory tree as a flat list of visible rows (issue #86), so the tree can be virtualized:
 * expanding a folder with tens of thousands of sub-folders only mounts the rows on screen. Pure (no
 * React), so it is unit-tested in Node.
 */

export interface TreeRoot {
  ref: EntryRef;
  label: string;
  fullPath: string;
  /** Catalog file name without extension, shown in brackets after the root label. */
  catalogName: string;
  /** Total size of everything under the root (CatalogInfo.rootSize). */
  size: number;
}

export interface TreeRow {
  /** `refToString(ref)`; also the selection key. */
  key: string;
  ref: EntryRef;
  label: string;
  fullPath: string;
  /** Aggregated subtree size in bytes. */
  size: number;
  /** Nesting level; roots are 0. */
  depth: number;
  hasChildren: boolean;
  /** Shown open: expanded and its children are loaded. */
  expanded: boolean;
  isRoot: boolean;
  /** Root rows only. */
  catalogName?: string;
}

/**
 * Depth-first, in the given order: each root, then (if expanded and loaded) its children, nested.
 * `children` maps a node key to its loaded child folders; a node that is expanded but not loaded yet
 * is drawn collapsed. A collapsed node keeps its loaded children for the next expand.
 */
export function flattenTree(
  roots: TreeRoot[],
  expanded: ReadonlySet<string>,
  children: ReadonlyMap<string, DirectoryNode[]>,
): TreeRow[] {
  const rows: TreeRow[] = [];

  const visit = (row: Omit<TreeRow, "expanded">) => {
    const kids = expanded.has(row.key) ? children.get(row.key) : undefined;
    rows.push({ ...row, expanded: kids !== undefined });
    if (!kids) return;
    for (const c of kids) {
      visit({
        key: refToString(c.ref),
        ref: c.ref,
        label: c.name,
        fullPath: c.fullPath,
        size: c.size,
        depth: row.depth + 1,
        hasChildren: c.hasChildren,
        isRoot: false,
      });
    }
  };

  for (const r of roots) {
    visit({
      key: refToString(r.ref),
      ref: r.ref,
      label: r.label,
      fullPath: r.fullPath,
      size: r.size,
      depth: 0,
      hasChildren: true,
      isRoot: true,
      catalogName: r.catalogName,
    });
  }
  return rows;
}
