import type { SearchResultRow } from "./api/types";
import { formatDate, sizeCell, sizeTitle } from "./format";
import { normalizeColumnOrder } from "./columnOrder";

export type SortKind = "text" | "basic" | "date";

export interface ResultColumn {
  id: string;
  header: string;
  defaultWidthPx: number;
  sort: SortKind;
  sortValue: (r: SearchResultRow) => string | number;
  cell: (r: SearchResultRow) => string;
  cellTitle?: (r: SearchResultRow) => string;
  numeric?: boolean;
  csv: (r: SearchResultRow) => string;
  guardFormula?: boolean;
  /** Shown until the user chooses otherwise; false keeps a new column from changing anyone's layout. */
  defaultVisible: boolean;
}

export function fileExtension(r: Pick<SearchResultRow, "name" | "isDirectory">): string {
  if (r.isDirectory) return "";
  const dot = r.name.lastIndexOf(".");
  return dot <= 0 ? "" : r.name.slice(dot + 1);
}

export type EntryKind = "file" | "folder" | "link";

export function entryKind(r: Pick<SearchResultRow, "isDirectory" | "isReparsePoint">): EntryKind {
  return r.isReparsePoint ? "link" : r.isDirectory ? "folder" : "file";
}

export const RESULT_COLUMNS: readonly ResultColumn[] = [
  {
    id: "name", header: "Name", defaultWidthPx: 260, sort: "text", defaultVisible: true, guardFormula: true,
    sortValue: (r) => r.name, cell: (r) => r.name, csv: (r) => r.name,
  },
  {
    id: "size", header: "Size", defaultWidthPx: 90, sort: "basic", defaultVisible: true, numeric: true,
    sortValue: (r) => r.size, cell: (r) => sizeCell(r.isDirectory, r.size, r.isReparsePoint),
    cellTitle: (r) => sizeTitle(r.size), csv: (r) => String(r.size),
  },
  {
    id: "modified", header: "Modified", defaultWidthPx: 150, sort: "date", defaultVisible: true,
    sortValue: (r) => Date.parse(r.modified), cell: (r) => formatDate(r.modified, r.isModifiedBad),
    csv: (r) => (r.isModifiedBad ? "" : r.modified),
  },
  {
    id: "catalog", header: "Catalog", defaultWidthPx: 160, sort: "text", defaultVisible: true, guardFormula: true,
    sortValue: (r) => r.catalogName, cell: (r) => r.catalogName, csv: (r) => r.catalogName,
  },
  {
    id: "path", header: "Path", defaultWidthPx: 380, sort: "text", defaultVisible: true, guardFormula: true,
    sortValue: (r) => r.parentPath, cell: (r) => r.parentPath, csv: (r) => r.parentPath,
  },
  {
    id: "extension", header: "Extension", defaultWidthPx: 90, sort: "text", defaultVisible: false, guardFormula: true,
    sortValue: fileExtension, cell: fileExtension, csv: fileExtension,
  },
  {
    id: "type", header: "Type", defaultWidthPx: 80, sort: "text", defaultVisible: false,
    sortValue: entryKind, cell: entryKind, csv: entryKind,
  },
];

export const RESULT_COLUMN_IDS: readonly string[] = RESULT_COLUMNS.map((c) => c.id);

export const RESULT_COLUMN_BY_ID: ReadonlyMap<string, ResultColumn> = new Map(RESULT_COLUMNS.map((c) => [c.id, c]));

const DEFAULT_VISIBLE_IDS: readonly string[] = RESULT_COLUMNS.filter((c) => c.defaultVisible).map((c) => c.id);

export function resolveVisibleColumns(saved: unknown): ReadonlySet<string> {
  const known = Array.isArray(saved) ? saved.filter((id): id is string => RESULT_COLUMN_BY_ID.has(id as string)) : [];
  return new Set(known.length > 0 ? known : DEFAULT_VISIBLE_IDS);
}

export function visibleColumnIds(savedOrder: unknown, savedVisible: unknown): string[] {
  const visible = resolveVisibleColumns(savedVisible);
  return normalizeColumnOrder(savedOrder, RESULT_COLUMN_IDS).filter((id) => visible.has(id));
}
