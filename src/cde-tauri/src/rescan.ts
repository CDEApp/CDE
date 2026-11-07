// Pure logic around a finished rescan: which search results went stale, and where the selection lands afterwards.
// No React, no network: everything here is unit-tested in rescan.test.ts.

import type { DirectoryNode, EntryRef, SearchResultRow } from "./api/types";

// ---------------------------------------------------------------- stale results

/**
 * The banner above search results that point into a catalog rescanned after the search ran, or
 * null when none do. `rescannedSinceSearch` is the set of catalog ids rescanned since that search.
 */
export function staleResultsBanner(
  rows: readonly SearchResultRow[],
  rescannedSinceSearch: ReadonlySet<number>,
  nameOf: (catalogId: number) => string,
): string | null {
  if (rescannedSinceSearch.size === 0 || rows.length === 0) return null;
  const hit = new Set<number>();
  for (const r of rows) {
    if (rescannedSinceSearch.has(r.ref.catalogId)) hit.add(r.ref.catalogId);
  }
  if (hit.size === 0) return null;
  const names = [...hit].sort((a, b) => a - b).map(nameOf);
  return names.length === 1
    ? `Catalog ${names[0]} was rescanned — results may be out of date.`
    : `Catalogs ${names.join(", ")} were rescanned — results may be out of date.`;
}

/**
 * Whether a search row still resolves after a rescan: indexes are per catalog file, so a stale
 * ref can silently point at a different entry. The ref's path chain must end at the row's path.
 */
export function rowStillResolves(rowFullPath: string, chain: readonly Pick<DirectoryNode, "fullPath">[]): boolean {
  if (chain.length === 0) return false;
  return chain[chain.length - 1].fullPath.toLowerCase() === rowFullPath.toLowerCase();
}

// ---------------------------------------------------------------- selection fallback

/** Drop any trailing "/" characters (linear scan; avoids a backtracking regex). */
function trimTrailingSlashes(p: string): string {
  let end = p.length;
  while (end > 0 && p.charCodeAt(end - 1) === 47) end--;
  return p.slice(0, end);
}

export interface SurvivingFolder {
  ref: EntryRef;
  fullPath: string;
  /** False when the requested folder is gone and this is its nearest surviving parent. */
  exact: boolean;
}

/**
 * Find `targetPath` in the (new) catalog by walking folder names down from the catalog root; stop at
 * the deepest folder that still exists. Names match case-insensitively; both separators are accepted.
 */
export async function resolveSurvivingFolder(
  rootRef: EntryRef,
  rootPath: string,
  targetPath: string,
  listFolders: (ref: EntryRef) => Promise<readonly DirectoryNode[]>,
): Promise<SurvivingFolder> {
  const norm = (p: string) => p.replace(/\\/g, "/").toLowerCase();
  const root = trimTrailingSlashes(norm(rootPath));
  const target = trimTrailingSlashes(norm(targetPath));
  let current: SurvivingFolder = { ref: rootRef, fullPath: rootPath, exact: true };

  if (target === root || !target.startsWith(root + "/")) {
    return { ...current, exact: target === root };
  }

  const segments = target.slice(root.length + 1).split("/").filter(Boolean);
  for (const seg of segments) {
    const kids = await listFolders(current.ref);
    const next = kids.find((k) => k.isDirectory && k.name.toLowerCase() === seg);
    if (!next) return { ...current, exact: false };
    current = { ref: next.ref, fullPath: next.fullPath, exact: true };
  }
  return current;
}
