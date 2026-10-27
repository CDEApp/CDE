// Pure helpers for the TreeMap view: kept free of React and the chart library so they unit-test
// in plain Node.

/** Smallest tile edge (px) worth drawing; anything smaller is merged server-side. */
export const MIN_TILE_PX = 6;
/** Fallback when the chart area hasn't been measured yet (≈ a 1000×600 area). */
const FALLBACK_AREA = 1000 * 600;

/**
 * Fraction of the mapped folder's size below which an entry would get a tile smaller than about
 * `minTilePx`² pixels in a `width`×`height` area — the server folds those into one row per folder.
 */
export function minFractionFor(width: number, height: number, minTilePx = MIN_TILE_PX): number {
  const area = width > 0 && height > 0 ? width * height : FALLBACK_AREA;
  return Math.min(0.5, (minTilePx * minTilePx) / area);
}

/** Join a path segment without doubling a separator after a root like "C:\". */
export function joinPath(base: string, name: string): string {
  if (base === "") return name;
  const last = base[base.length - 1];
  return last === "\\" || last === "/" ? base + name : `${base}\\${name}`;
}

/** The minimal node shape these helpers need (a subset of TanStack's TreemapNode). */
export interface MapNode {
  id: string;
  depth: number;
  ancestorIds: readonly string[];
}

/**
 * The top-level branch (child of the mapped root) a node belongs to, or null for the root itself.
 * `ancestorIds` runs root → parent, so index 1 is the depth-1 ancestor.
 */
export function branchIdOf(node: MapNode): string | null {
  if (node.depth <= 0) return null;
  return node.depth === 1 ? node.id : (node.ancestorIds[1] ?? null);
}

/** Categorical branch hues, mid-lightness so dark tile labels stay legible. */
export const BRANCH_PALETTE = [
  "#5b9bd5", "#e3a857", "#70ad47", "#d9706d", "#9b84c9",
  "#4fb4b0", "#c98bb6", "#b5a642", "#7f9fbf", "#d4936a",
];
export const MERGED_COLOR = "#7a7a7a";

/**
 * Colour per top-level branch, assigned largest-first so the biggest branches get the most
 * distinct hues. Merged rows are grey; the palette cycles past ten branches.
 */
export function branchColors(
  branches: readonly { nodeId: string; size: number; mergedCount: number }[],
): Map<string, string> {
  const colors = new Map<string, string>();
  let i = 0;
  for (const b of [...branches].sort((a, z) => z.size - a.size)) {
    colors.set(b.nodeId, b.mergedCount > 0 ? MERGED_COLOR : BRANCH_PALETTE[i++ % BRANCH_PALETTE.length]);
  }
  return colors;
}

/** Blend a #rrggbb colour toward `target` by `amount` (0 = unchanged, 1 = target). */
export function mix(color: string, target: string, amount: number): string {
  const a = parseHex(color);
  const b = parseHex(target);
  const t = Math.max(0, Math.min(1, amount));
  const c = a.map((v, k) => Math.round(v + (b[k] - v) * t));
  return `#${c.map((v) => v.toString(16).padStart(2, "0")).join("")}`;
}

function parseHex(hex: string): [number, number, number] {
  const h = hex.replace("#", "");
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16)) as [number, number, number];
}

/** Tile fill: the branch colour, lightened a little per level deeper so nesting reads. */
export function tileColor(branchColor: string, depth: number): string {
  return mix(branchColor, "#ffffff", Math.max(0, depth - 1) * 0.14);
}

/**
 * The folder a click on `node` drills into: its top-level branch, provided that branch is a
 * directory with a catalog ref (files and merged rows directly under the root lead nowhere).
 */
export function drillTargetId(
  node: MapNode,
  rowsById: ReadonlyMap<string, { isDirectory: boolean; ref: unknown }>,
): string | null {
  const id = branchIdOf(node);
  if (id == null) return null;
  const row = rowsById.get(id);
  return row && row.isDirectory && row.ref ? id : null;
}

/** Full filesystem path of `nodeId`, built from the mapped root path and row names. */
export function fullPathOf(
  nodeId: string,
  rowsById: ReadonlyMap<string, { parentId: string | null; name: string }>,
  rootFullPath: string,
): string {
  const names: string[] = [];
  for (let row = rowsById.get(nodeId); row && row.parentId != null; row = rowsById.get(row.parentId)) {
    names.push(row.name);
  }
  return names.reverse().reduce((path, name) => joinPath(path, name), rootFullPath);
}

/** Inset (0.5) + labelPadding (4): the library's clearance between a tile edge and its label. */
export const LABEL_PADDING = 4.5;

export interface TileRect {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
}

export type LabelLines = "both" | "name" | "none";

/**
 * Which label lines fit inside a tile: the name over the size, the name alone, or nothing. Mirrors
 * the treemap mark's own rule (label bounds must sit inside the rect shrunk by `padding` on every
 * side), with the size as a second line of `lineHeight` below the name. Widths are measured text.
 */
export function labelLines(
  rect: TileRect,
  nameWidth: number,
  sizeWidth: number,
  lineHeight: number,
  padding: number,
): LabelLines {
  const availW = rect.x1 - rect.x0 - 2 * padding;
  const availH = rect.y1 - rect.y0 - 2 * padding;
  if (!(availW > 0 && availH > 0)) return "none";
  if (nameWidth > availW || lineHeight > availH) return "none";
  return sizeWidth <= availW && 2 * lineHeight <= availH ? "both" : "name";
}

interface TileNode {
  parent?: unknown;
  children?: (TileNode & TileRect)[];
}

/**
 * Wraps a d3-style tiler `(node, x0, y0, x1, y1)` so every leaf rectangle it assigns is recorded
 * by id. The treemap mark only labels leaves (`hierarchy.leaves()`), and a tiler call for the root
 * starts a new layout, so each (re)layout yields a fresh map and bumps `generation()`.
 */
export function capturingTiler<N extends TileNode>(
  base: (node: N, x0: number, y0: number, x1: number, y1: number) => void,
  idOf: (node: N) => string,
) {
  let rects = new Map<string, TileRect>();
  let generation = 0;
  return {
    tile(node: N, x0: number, y0: number, x1: number, y1: number): void {
      base(node, x0, y0, x1, y1);
      if (!node.parent) {
        rects = new Map();
        generation++;
      }
      for (const child of (node.children ?? []) as (N & TileRect)[]) {
        if (!child.children?.length) {
          rects.set(idOf(child), { x0: child.x0, y0: child.y0, x1: child.x1, y1: child.y1 });
        }
      }
    },
    current: () => rects,
    generation: () => generation,
  };
}
