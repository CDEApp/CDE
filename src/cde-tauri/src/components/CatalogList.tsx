import { useMemo, useState } from "react";
import { CatalogInfo } from "../api/types";
import {
  CATALOG_MIN_WIDTH,
  type CatalogColumnWidths,
  catalogCreated,
  catalogModified,
  catalogTotalWidth,
  catalogWidthOf,
  clampCatalogWidth,
  normalizeCatalogWidths,
} from "../catalogColumns";
import { type CatalogSort, type CatalogSortColumn, filterCatalogs, sortCatalogs } from "../catalogSort";
import { catalogFile, fileBaseName, formatDate, toHRString } from "../format";

interface Props {
  catalogs: CatalogInfo[];
  onActivate: (catalog: CatalogInfo) => void;
  /** File > New Catalog… from the tab itself. */
  onNewCatalog?: () => void;
  onContextMenu?: (catalog: CatalogInfo, x: number, y: number) => void;
  /** Catalog ids being rescanned right now (their rows are dimmed). */
  rescanningIds?: ReadonlySet<number>;
  /** Current sort (held by App so it survives tab switches). */
  sort: CatalogSort;
  /** A column header was clicked. */
  onSort: (column: CatalogSortColumn) => void;
  /** Persisted column widths (`catalogColumnWidths`); validated on mount. */
  columnWidths?: unknown;
  /** A column resize finished (or was reset): the full set of user-set widths to persist. */
  onColumnWidthsChange?: (widths: CatalogColumnWidths) => void;
  /** Selected catalog id (held by App so it survives tab switches). */
  selectedId?: number | null;
  onSelect?: (catalogId: number) => void;
}

const COLUMNS: { column: CatalogSortColumn; label: string; numeric?: boolean }[] = [
  { column: "root", label: "Root" },
  { column: "volume", label: "Volume" },
  { column: "dirs", label: "Dirs", numeric: true },
  { column: "files", label: "Files", numeric: true },
  { column: "size", label: "Size", numeric: true },
  { column: "free", label: "Free", numeric: true },
  { column: "drive", label: "Drive" },
  { column: "catalog", label: "Catalog" },
  { column: "description", label: "Description" },
  { column: "created", label: "Created" },
  { column: "modified", label: "Modified" },
];

const KEY_STEP = 10;
const KEY_STEP_LARGE = 50;

const dateText = (d: Date) => formatDate(d.toISOString(), false);

/** Catalog list view. Double-click a catalog to make it the directory-tree root. */
export function CatalogList({
  catalogs, onActivate, onNewCatalog, onContextMenu, rescanningIds, sort, onSort, columnWidths, onColumnWidthsChange,
  selectedId, onSelect,
}: Props) {
  const [filter, setFilter] = useState("");
  const rows = useMemo(() => sortCatalogs(filterCatalogs(catalogs, filter), sort), [catalogs, filter, sort]);
  const [widths, setWidths] = useState<CatalogColumnWidths>(() => normalizeCatalogWidths(columnWidths));

  function commit(next: CatalogColumnWidths) {
    setWidths(next);
    onColumnWidthsChange?.(next);
  }

  // The width tracks the pointer; it is saved once, on release.
  function startResize(e: React.MouseEvent, id: CatalogSortColumn) {
    if (e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation(); // resizing, not sorting
    const startWidth = catalogWidthOf(widths, id);
    const startX = e.clientX;
    let latest = widths;
    const onMove = (ev: MouseEvent) => {
      latest = { ...latest, [id]: clampCatalogWidth(startWidth + ev.clientX - startX) };
      setWidths(latest);
    };
    const onUp = () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
      if (latest !== widths) onColumnWidthsChange?.(latest);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  }

  function resetWidth(id: CatalogSortColumn) {
    const { [id]: _removed, ...rest } = widths;
    commit(rest);
  }

  return (
    <div className="grid cataloglist">
      <div className="cataloglist-toolbar">
        {onNewCatalog && (
          <button onClick={onNewCatalog} title="Scan a folder into a new catalog file">
            New Catalog
          </button>
        )}
        <span className="tree-filter">
          <input
            type="text"
            aria-label="Filter catalogs"
            placeholder="Filter by root, volume, drive, catalog or description…"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Escape" && filter) {
                e.preventDefault();
                setFilter("");
              }
            }}
          />
          {filter && (
            <button type="button" className="tree-filter-clear" aria-label="Clear filter" onClick={() => setFilter("")}>
              {"×"}
            </button>
          )}
        </span>
      </div>
      <table style={{ width: catalogTotalWidth(widths) }}>
        <colgroup>
          {COLUMNS.map(({ column }) => (
            <col key={column} style={{ width: catalogWidthOf(widths, column) }} />
          ))}
        </colgroup>
        <thead>
          <tr>
            {COLUMNS.map(({ column, label, numeric }) => {
              const active = sort?.column === column;
              return (
                <th
                  key={column}
                  className={numeric ? "num" : undefined}
                  aria-sort={!active ? "none" : sort.direction === "asc" ? "ascending" : "descending"}
                >
                  <button className="th-sort" onClick={() => onSort(column)}>
                    {label}
                    <span className="sort-arrow" aria-hidden="true">
                      {active ? (sort.direction === "asc" ? "▲" : "▼") : ""}
                    </span>
                  </button>
                  <span
                    className="resizer"
                    role="separator"
                    aria-orientation="vertical"
                    aria-label={`Resize ${label} column`}
                    aria-valuenow={catalogWidthOf(widths, column)}
                    aria-valuemin={CATALOG_MIN_WIDTH}
                    tabIndex={0}
                    title="Drag or use the arrow keys to resize, double-click or Home to reset"
                    onMouseDown={(e) => startResize(e, column)}
                    onKeyDown={(e) => {
                      const step = e.shiftKey ? KEY_STEP_LARGE : KEY_STEP;
                      const current = catalogWidthOf(widths, column);
                      if (e.key === "ArrowRight") commit({ ...widths, [column]: clampCatalogWidth(current + step) });
                      else if (e.key === "ArrowLeft") commit({ ...widths, [column]: clampCatalogWidth(current - step) });
                      else if (e.key === "Home") resetWidth(column);
                      else return;
                      e.preventDefault();
                    }}
                    onClick={(e) => e.stopPropagation()}
                    onDoubleClick={(e) => {
                      e.stopPropagation();
                      resetWidth(column);
                    }}
                  />
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((c) => {
            const created = catalogCreated(c);
            const modified = catalogModified(c);
            return (
              <tr
                key={c.catalogId}
                className={
                  [rescanningIds?.has(c.catalogId) && "rescanning", c.catalogId === selectedId && "selected"]
                    .filter(Boolean)
                    .join(" ") || undefined
                }
                aria-selected={c.catalogId === selectedId}
                onClick={() => onSelect?.(c.catalogId)}
                onDoubleClick={() => onActivate(c)}
                onContextMenu={(e) => {
                  onSelect?.(c.catalogId);
                  if (!onContextMenu) return;
                  e.preventDefault();
                  e.stopPropagation();
                  onContextMenu(c, e.clientX, e.clientY);
                }}
              >
                <td>{c.rootPath}</td>
                <td>{c.volumeName}</td>
                <td className="num">{c.dirEntryCount.toLocaleString()}</td>
                <td className="num">{c.fileEntryCount.toLocaleString()}</td>
                <td className="num">{toHRString(c.rootSize)}</td>
                <td
                  className="num"
                  title={c.totalSpace > 0 ? `${toHRString(c.availSpace)} free of ${toHRString(c.totalSpace)} on the whole volume` : undefined}
                >
                  {c.totalSpace > 0 ? toHRString(c.availSpace) : ""}
                </td>
                <td>{c.driveLetterHint}</td>
                <td title={catalogFile(c)}>{fileBaseName(catalogFile(c))}</td>
                <td title={c.description || undefined}>{c.description}</td>
                <td className="dates">{created && dateText(created)}</td>
                <td className="dates">{modified && dateText(modified)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {filter.trim() && rows.length === 0 && <div className="cataloglist-empty">No catalogs match “{filter.trim()}”.</div>}
    </div>
  );
}
