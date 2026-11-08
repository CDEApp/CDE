import type { DirectoryNode, SubtreeSummary } from "./api/types";

export type DiffStatus = "same" | "different" | "left-only" | "right-only";

export interface DiffRow {
  /** Path relative to the comparison roots, case-folded: how the two sides are matched. */
  key: string;
  depth: number;
  name: string;
  left: DirectoryNode | null;
  right: DirectoryNode | null;
  status: DiffStatus;
  isDirectory: boolean;
}

export interface ChildLists {
  left: readonly DirectoryNode[];
  right: readonly DirectoryNode[];
}

/** Catalogs from different scans round timestamps differently (FAT stores 2 s steps). */
const MODIFIED_TOLERANCE_MS = 2000;

function modifiedMs(n: DirectoryNode): number | null {
  if (n.isModifiedBad) return null;
  const t = new Date(n.modified).getTime();
  return Number.isNaN(t) ? null : t;
}

function sameModified(a: DirectoryNode, b: DirectoryNode): boolean {
  const x = modifiedMs(a);
  const y = modifiedMs(b);
  if (x === null || y === null) return x === y;
  return Math.abs(x - y) <= MODIFIED_TOLERANCE_MS;
}

/**
 * Status of a name present on both sides. A file is the same when size and modified time agree.
 * A folder is the same when its aggregated size agrees: a size heuristic, because the children are
 * loaded lazily and not compared until the folder is expanded. A file on one side and a folder on
 * the other is different.
 */
function bothSidesStatus(left: DirectoryNode, right: DirectoryNode): DiffStatus {
  if (left.isDirectory !== right.isDirectory) return "different";
  if (left.size !== right.size) return "different";
  return left.isDirectory || sameModified(left, right) ? "same" : "different";
}

const foldName = (name: string) => name.toUpperCase();

const compareText = (x: string, y: string) => (x < y ? -1 : x > y ? 1 : 0);

function compareRows(a: DiffRow, b: DiffRow): number {
  if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
  return compareText(foldName(a.name), foldName(b.name)) || compareText(a.name, b.name);
}

function groupByFoldedName(nodes: readonly DirectoryNode[]): Map<string, DirectoryNode[]> {
  const groups = new Map<string, DirectoryNode[]>();
  for (const n of nodes) {
    const folded = n.name.toLowerCase();
    const group = groups.get(folded);
    if (group) group.push(n);
    else groups.set(folded, [n]);
  }
  return groups;
}

/** Pairs left with right entries of one folded name: exact (case-sensitive) names first, then the rest in order. */
function pairByName(lefts: readonly DirectoryNode[], rights: readonly DirectoryNode[]) {
  const pairs: { left: DirectoryNode | null; right: DirectoryNode | null }[] = [];
  const unmatchedRights = [...rights];
  const unmatchedLefts: DirectoryNode[] = [];
  for (const left of lefts) {
    const i = unmatchedRights.findIndex((r) => r.name === left.name);
    if (i < 0) unmatchedLefts.push(left);
    else pairs.push({ left, right: unmatchedRights.splice(i, 1)[0] });
  }
  const count = Math.max(unmatchedLefts.length, unmatchedRights.length);
  for (let i = 0; i < count; i++) {
    pairs.push({ left: unmatchedLefts[i] ?? null, right: unmatchedRights[i] ?? null });
  }
  return pairs;
}

/**
 * Merge two child lists into rows by case-insensitive name: folders first, then name order (the
 * Directory view's convention). Two entries of the same side are never merged, so names that differ
 * only by case stay separate rows. A name that is a file on one side and a folder on the other is one
 * non-expandable "different" row.
 */
export function alignChildren(
  leftNodes: readonly DirectoryNode[],
  rightNodes: readonly DirectoryNode[],
  depth: number,
  parentKey: string,
): DiffRow[] {
  const leftGroups = groupByFoldedName(leftNodes);
  const rightGroups = groupByFoldedName(rightNodes);
  const rows: DiffRow[] = [];
  const usedKeys = new Set<string>();

  for (const folded of new Set([...leftGroups.keys(), ...rightGroups.keys()])) {
    const lefts = leftGroups.get(folded) ?? [];
    const rights = rightGroups.get(folded) ?? [];
    const ambiguous = lefts.length > 1 || rights.length > 1;
    for (const { left, right } of pairByName(lefts, rights)) {
      const primary = (left ?? right)!;
      let key = `${parentKey}/${folded}`;
      if (ambiguous) key += `\u0000${primary.name}`;
      for (let n = 2; usedKeys.has(key); n++) key = `${parentKey}/${folded}\u0000${primary.name}\u0000${n}`;
      usedKeys.add(key);
      rows.push({
        key,
        depth,
        name: primary.name,
        left,
        right,
        status: left && right ? bothSidesStatus(left, right) : left ? "left-only" : "right-only",
        isDirectory: left && right ? left.isDirectory && right.isDirectory : primary.isDirectory,
      });
    }
  }
  return rows.sort(compareRows);
}

export type DiffFilterId =
  | "all"
  | "same"
  | "differences"
  | "noOrphans"
  | "differencesNoOrphans"
  | "orphans"
  | "leftNewer"
  | "rightNewer"
  | "leftNewerLeftOrphans"
  | "rightNewerRightOrphans"
  | "leftOrphans"
  | "rightOrphans";

/** Positive when the left side was modified later, negative when the right was, null when not comparable. */
function modifiedDeltaMs(row: DiffRow): number | null {
  if (!row.left || !row.right) return null;
  const l = modifiedMs(row.left);
  const r = modifiedMs(row.right);
  return l === null || r === null ? null : l - r;
}

// "Newer" needs a "different" row whose dates really differ: one that differs by size only (dates
// within tolerance) or has a bad date is in neither newer filter.
const leftNewer = (row: DiffRow) =>
  row.status === "different" && (modifiedDeltaMs(row) ?? 0) > MODIFIED_TOLERANCE_MS;
const rightNewer = (row: DiffRow) =>
  row.status === "different" && (modifiedDeltaMs(row) ?? 0) < -MODIFIED_TOLERANCE_MS;

/**
 * Every row filter. `shows` is the whole-row predicate ("newer" compares both sides). `belowMatches`
 * says, from a both-sides folder's exact summary, whether some row below it passes `shows`: a file, a
 * one-sided entry, or a nested both-sides folder row, which the summary counts as `sameFolders` or
 * `differentFolders` (and the newer subsets). The test suite checks the two agree.
 */
export const DIFF_FILTERS: Record<
  DiffFilterId,
  {
    label: string;
    /** Shown on the Diffs toggle while this mode is the selected one. */
    short: string;
    shows: (row: DiffRow) => boolean;
    belowMatches: (s: SubtreeSummary) => boolean;
  }
> = {
  all: { label: "All", short: "All", shows: () => true, belowMatches: () => true },
  same: { label: "Same", short: "Same", shows: (row) => row.status === "same", belowMatches: (s) => s.sameFiles + s.sameFolders > 0 },
  differences: {
    label: "Show Differences",
    short: "Diffs",
    shows: (row) => row.status !== "same",
    belowMatches: (s) => s.differentFiles + s.differentFolders + s.leftOnly + s.rightOnly > 0,
  },
  noOrphans: {
    label: "Show No Orphans",
    short: "No Orphans",
    shows: (row) => row.status === "same" || row.status === "different",
    belowMatches: (s) => s.sameFiles + s.differentFiles + s.sameFolders + s.differentFolders > 0,
  },
  differencesNoOrphans: {
    label: "Show Differences but No Orphans",
    short: "Diffs, No Orphans",
    shows: (row) => row.status === "different",
    belowMatches: (s) => s.differentFiles + s.differentFolders > 0,
  },
  orphans: {
    label: "Show Orphans",
    short: "Orphans",
    shows: (row) => row.status === "left-only" || row.status === "right-only",
    belowMatches: (s) => s.leftOnly + s.rightOnly > 0,
  },
  leftNewer: { label: "Show Left Newer", short: "Left Newer", shows: leftNewer, belowMatches: (s) => s.leftNewerFiles + s.leftNewerFolders > 0 },
  rightNewer: { label: "Show Right Newer", short: "Right Newer", shows: rightNewer, belowMatches: (s) => s.rightNewerFiles + s.rightNewerFolders > 0 },
  leftNewerLeftOrphans: {
    label: "Show Left Newer and Left Orphans",
    short: "Left Newer + Orphans",
    shows: (row) => leftNewer(row) || row.status === "left-only",
    belowMatches: (s) => s.leftNewerFiles + s.leftNewerFolders + s.leftOnly > 0,
  },
  rightNewerRightOrphans: {
    label: "Show Right Newer and Right Orphans",
    short: "Right Newer + Orphans",
    shows: (row) => rightNewer(row) || row.status === "right-only",
    belowMatches: (s) => s.rightNewerFiles + s.rightNewerFolders + s.rightOnly > 0,
  },
  leftOrphans: { label: "Show Left Orphans", short: "Left Orphans", shows: (row) => row.status === "left-only", belowMatches: (s) => s.leftOnly > 0 },
  rightOrphans: { label: "Show Right Orphans", short: "Right Orphans", shows: (row) => row.status === "right-only", belowMatches: (s) => s.rightOnly > 0 },
};

/** The entries of the Diffs drop-down menu, in display order. */
export const DIFF_MENU_FILTERS: readonly DiffFilterId[] = [
  "differences",
  "noOrphans",
  "differencesNoOrphans",
  "orphans",
  "leftNewer",
  "rightNewer",
  "leftNewerLeftOrphans",
  "rightNewerRightOrphans",
  "leftOrphans",
  "rightOrphans",
];

/** True when the summary says nothing below the folder pair differs or is one-sided (nested folders count through their contents). */
export const isIdentical = (s: SubtreeSummary) => s.differentFiles + s.leftOnly + s.rightOnly === 0;

/**
 * The flat, ordered rows to render. Children load lazily, so a both-sides folder's own status is a size
 * heuristic until its exact `summaries` entry (by row key) arrives. With a summary the folder's status is
 * exact (identical means same, otherwise different), and under a filter it is kept when it matches itself
 * or when something below it matches, so a container holding matches is never hidden. Without a summary
 * the folder stays visible: never hide what is not yet judged. Files and one-sided folders are filtered
 * by their own status. The children of an expanded shown folder are visited the same way. A folder with
 * no entry in `childrenByKey` (not loaded yet) contributes no children.
 */
export function flattenVisible(
  expanded: ReadonlySet<string>,
  childrenByKey: ReadonlyMap<string, ChildLists>,
  rootRows: readonly DiffRow[],
  filter: DiffFilterId,
  summaries: ReadonlyMap<string, SubtreeSummary>,
): DiffRow[] {
  const { shows, belowMatches } = DIFF_FILTERS[filter];
  const out: DiffRow[] = [];
  const visit = (rows: readonly DiffRow[]) => {
    for (const row of rows) {
      let shown = row;
      let visible: boolean;
      if (row.isDirectory && row.left !== null && row.right !== null) {
        const summary = summaries.get(row.key);
        if (summary) {
          const status: DiffStatus = isIdentical(summary) ? "same" : "different";
          if (status !== row.status) shown = { ...row, status };
        }
        visible = filter === "all" || !summary || shows(shown) || belowMatches(summary);
      } else {
        visible = shows(row);
      }
      if (!visible) continue;
      out.push(shown);
      const kids = row.isDirectory && expanded.has(row.key) ? childrenByKey.get(row.key) : undefined;
      if (kids) visit(alignChildren(kids.left, kids.right, row.depth + 1, row.key));
    }
  };
  visit(rootRows);
  return out;
}

/**
 * The folder names from `rootPath` down to `path`, or null when `path` is not at or below the root.
 * Accepts `/` or `\`, ignores empty segments and compares case-insensitively.
 */
export function relativeSegments(rootPath: string, path: string): string[] | null {
  const split = (p: string) => p.split(/[\\/]+/).filter((s) => s !== "");
  const root = split(rootPath);
  const target = split(path);
  if (target.length < root.length) return null;
  for (let i = 0; i < root.length; i++) {
    if (foldName(root[i]) !== foldName(target[i])) return null;
  }
  return target.slice(root.length);
}
