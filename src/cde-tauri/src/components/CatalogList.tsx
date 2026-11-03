import { useMemo } from "react";
import { CatalogInfo } from "../api/types";
import { type CatalogSort, type CatalogSortColumn, sortCatalogs } from "../catalogSort";
import { catalogFile, fileBaseName, toHRString } from "../format";

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
}

const COLUMNS: { column: CatalogSortColumn; label: string; numeric?: boolean }[] = [
  { column: "root", label: "Root" },
  { column: "volume", label: "Volume" },
  { column: "dirs", label: "Dirs", numeric: true },
  { column: "files", label: "Files", numeric: true },
  { column: "size", label: "Size", numeric: true },
  { column: "drive", label: "Drive" },
  { column: "catalog", label: "Catalog" },
];

/** Catalog list view. Double-click a catalog to make it the directory-tree root. */
export function CatalogList({ catalogs, onActivate, onNewCatalog, onContextMenu, rescanningIds, sort, onSort }: Props) {
  const rows = useMemo(() => sortCatalogs(catalogs, sort), [catalogs, sort]);
  return (
    <div className="grid cataloglist">
      {onNewCatalog && (
        <div className="cataloglist-toolbar">
          <button onClick={onNewCatalog} title="Scan a folder into a new catalog file">
            New Catalog
          </button>
        </div>
      )}
      <table>
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
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((c) => (
            <tr
              key={c.catalogId}
              className={rescanningIds?.has(c.catalogId) ? "rescanning" : undefined}
              onDoubleClick={() => onActivate(c)}
              onContextMenu={(e) => {
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
              <td>{c.driveLetterHint}</td>
              <td title={catalogFile(c)}>{fileBaseName(catalogFile(c))}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
