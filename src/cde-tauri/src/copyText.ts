import type { SearchResultRow } from "./api/types";

export type CopyKind = "path" | "name" | "parent";

const FIELD: Record<CopyKind, (r: SearchResultRow) => string> = {
  path: (r) => r.fullPath,
  name: (r) => r.name,
  parent: (r) => r.parentPath,
};

/** One value per line, in the order given: the clipboard text for copying `rows`. */
export function rowsToClipboardText(rows: readonly SearchResultRow[], kind: CopyKind): string {
  return rows.map(FIELD[kind]).join("\n");
}
