// Pure logic for the "Rescan catalog" feature: which catalogs are rescanning, what the menus and
// status bar say, which search results went stale, and where the selection lands afterwards.
// No React, no network: everything here is unit-tested in rescan.test.ts.

import type { DirectoryNode, EntryRef, SearchResultRow } from "./api/types";

// ---------------------------------------------------------------- bookkeeping

export interface RescanEntry {
  catalogId: number;
  /** What the user sees in the status bar: the catalog's root path. */
  label: string;
  /** "scanning" | "writing" | "swapping" (as sent by the sidecar). */
  phase: string;
  entriesScanned: number;
}

/** One entry per rescanning catalog (one rescan per catalog; several catalogs may run at once). */
export type RescanState = ReadonlyMap<number, RescanEntry>;

export function emptyRescanState(): RescanState {
  return new Map();
}

export function beginRescan(state: RescanState, catalogId: number, label: string): RescanState {
  if (state.has(catalogId)) return state;
  const next = new Map(state);
  next.set(catalogId, { catalogId, label, phase: "scanning", entriesScanned: 0 });
  return next;
}

export function updateRescan(
  state: RescanState,
  catalogId: number,
  progress: { phase: string; entriesScanned: number },
): RescanState {
  const cur = state.get(catalogId);
  if (!cur) return state;
  const next = new Map(state);
  // The sidecar reports 0 entries for the writing/swapping phases; keep the last real count.
  next.set(catalogId, {
    ...cur,
    phase: progress.phase,
    entriesScanned: Math.max(cur.entriesScanned, progress.entriesScanned),
  });
  return next;
}

export function endRescan(state: RescanState, catalogId: number): RescanState {
  if (!state.has(catalogId)) return state;
  const next = new Map(state);
  next.delete(catalogId);
  return next;
}

export function isRescanning(state: RescanState, catalogId: number): boolean {
  return state.has(catalogId);
}

export function anyRescanning(state: RescanState): boolean {
  return state.size > 0;
}

export function rescanMenuItem(
  state: RescanState,
  catalogId: number,
): { label: string; disabled: boolean; title?: string } {
  return isRescanning(state, catalogId)
    ? { label: "Rescan", disabled: true, title: "A rescan of this catalog is already running" }
    : { label: "Rescan", disabled: false };
}

/** Why Reload / File > Open Folder are unavailable right now, or null when they are allowed. */
export function reloadDisabledReason(state: RescanState): string | null {
  return anyRescanning(state)
    ? "Unavailable while a catalog is being rescanned (reloading would drop the catalog being replaced)"
    : null;
}

export function statusLines(state: RescanState): { catalogId: number; text: string }[] {
  return [...state.values()]
    .sort((a, b) => a.catalogId - b.catalogId)
    .map((e) => ({ catalogId: e.catalogId, text: statusText(e) }));
}

function statusText(e: RescanEntry): string {
  if (e.phase === "swapping") return `Finishing rescan of ${e.label}…`;
  if (e.entriesScanned > 0) return `Rescanning ${e.label} — ${e.entriesScanned.toLocaleString("en-US")} entries`;
  return `Rescanning ${e.label}…`;
}

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
  const root = norm(rootPath).replace(/\/+$/, "");
  const target = norm(targetPath).replace(/\/+$/, "");
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
