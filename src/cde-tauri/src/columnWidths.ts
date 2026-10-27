/** Directory-listing columns whose width the user can set. */
export type DirColumnId = "name" | "size" | "modified";

export type ColumnWidths = Partial<Record<DirColumnId, number>>;

export const DIR_COLUMN_IDS: readonly DirColumnId[] = ["name", "size", "modified"];

/** Width a column starts at; for the fill column it is only the minimum until the user resizes it. */
export const DIR_DEFAULT_WIDTH: Record<DirColumnId, number> = { name: 200, size: 100, modified: 160 };

/** Floor for a dragged column, so it can never shrink to nothing. */
export const DIR_MIN_WIDTH: Record<DirColumnId, number> = { name: 80, size: 60, modified: 90 };

/** Ceiling that also guards against a corrupt saved value producing an absurd grid. */
export const MAX_COLUMN_WIDTH = 2000;

/** The column that absorbs spare pane width until the user resizes it. */
export const DIR_FILL_COLUMN: DirColumnId = "name";

export function clampWidth(id: DirColumnId, width: number): number {
  return Math.round(Math.max(DIR_MIN_WIDTH[id], Math.min(MAX_COLUMN_WIDTH, width)));
}

/**
 * Reconcile persisted widths with the known columns: unknown ids and non-finite or non-positive
 * values are dropped and the rest are clamped, so a stale or hand-edited ui-state can't break the grid.
 */
export function normalizeColumnWidths(saved: unknown): ColumnWidths {
  const out: ColumnWidths = {};
  if (typeof saved !== "object" || saved === null || Array.isArray(saved)) return out;
  const record = saved as Record<string, unknown>;
  for (const id of DIR_COLUMN_IDS) {
    const v = record[id];
    if (typeof v === "number" && Number.isFinite(v) && v > 0) out[id] = clampWidth(id, v);
  }
  return out;
}

/** Width of a column: the user's, else its default. */
export function widthOf(widths: ColumnWidths, id: DirColumnId): number {
  return widths[id] ?? DIR_DEFAULT_WIDTH[id];
}

/** Whether the fill column is still unset and so grows into spare pane width. */
export function isFillActive(widths: ColumnWidths): boolean {
  return widths[DIR_FILL_COLUMN] === undefined;
}

/** CSS grid template shared by the header and every row so they stay aligned. */
export function gridTemplate(widths: ColumnWidths): string {
  const fill = isFillActive(widths);
  return DIR_COLUMN_IDS.map((id) =>
    id === DIR_FILL_COLUMN && fill ? `minmax(${widthOf(widths, id)}px, 1fr)` : `${widthOf(widths, id)}px`,
  ).join(" ");
}

/** Sum of the column widths: the minimum row width, beyond which the list scrolls horizontally. */
export function totalWidth(widths: ColumnWidths): number {
  return DIR_COLUMN_IDS.reduce((sum, id) => sum + widthOf(widths, id), 0);
}
