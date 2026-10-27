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
  /** Bumped when the catalog is rescanned, so its cached children are dropped. */
  generation?: number;
}

const rootIdentity = (r: TreeRoot) => `${r.generation ?? 0}|${r.catalogName}|${r.fullPath}|${r.size}`;

/**
 * Catalog ids whose cached tree state (expansion, loaded children) is no longer valid going from
 * `prev` to `next` roots: rescanned (generation bumped), replaced by a different catalog under the same
 * id, or gone. Refs are only meaningful within one catalog version, so their entries must be dropped.
 */
export function changedCatalogs(prev: TreeRoot[], next: TreeRoot[]): Set<number> {
  const now = new Map(next.map((r) => [r.ref.catalogId, rootIdentity(r)]));
  const changed = new Set<number>();
  for (const r of prev) {
    if (now.get(r.ref.catalogId) !== rootIdentity(r)) changed.add(r.ref.catalogId);
  }
  return changed;
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
  /** Key of the parent row; null for roots. */
  parentKey: string | null;
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
        parentKey: row.key,
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
      parentKey: null,
      catalogName: r.catalogName,
    });
  }
  return rows;
}

export type TreeNav = { select: string } | { expand: string };

/**
 * What a key press does in the tree (issue #89), or null when it does nothing (or is not a tree key).
 * Down/Up: next/previous visible row. Home/End: first/last. Left: the parent (a root does nothing; it
 * never collapses). Right: expand a collapsed folder that has children, else nothing. A selection not in
 * `rows` (null, or collapsed out of view) acts as "nothing selected": Down/Home pick the first row,
 * End the last.
 */
export function nextSelection(rows: readonly TreeRow[], selectedKey: string | null, key: string): TreeNav | null {
  if (rows.length === 0) return null;
  const index = selectedKey === null ? -1 : rows.findIndex((r) => r.key === selectedKey);
  const go = (i: number): TreeNav | null => (i === index || i < 0 || i >= rows.length ? null : { select: rows[i].key });
  switch (key) {
    case "ArrowDown":
      return go(index + 1);
    case "ArrowUp":
      return index < 0 ? null : go(index - 1);
    case "Home":
      return go(0);
    case "End":
      return go(rows.length - 1);
    case "ArrowLeft": {
      const parent = index < 0 ? null : rows[index].parentKey;
      return parent === null ? null : { select: parent };
    }
    case "ArrowRight": {
      const row = index < 0 ? undefined : rows[index];
      return row && row.hasChildren && !row.expanded ? { expand: row.key } : null;
    }
    default:
      return null;
  }
}
