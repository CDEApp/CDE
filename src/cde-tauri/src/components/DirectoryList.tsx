import { useMemo, useState } from "react";
import { DirectoryNode } from "../api/types";
import { type DirectorySort, type SortColumn, sortDirectoryNodes } from "../directorySort";
import { splitRowAndNameStyle, type StyleResolver } from "../colourRules";
import { formatDate, sizeCell, sizeTitle } from "../format";
import {
  clampWidth,
  type ColumnWidths,
  type DirColumnId,
  gridTemplate,
  normalizeColumnWidths,
  totalWidth,
  widthOf,
} from "../columnWidths";
import { StyledName } from "./StyledName";
import { VirtualList } from "./VirtualList";

interface Props {
  path: string;
  nodes: DirectoryNode[];
  onActivate: (node: DirectoryNode) => void;
  onContextMenu: (node: DirectoryNode, x: number, y: number) => void;
  /** Colour-rule style for an entry name (undefined = no custom style). */
  resolveStyle: StyleResolver;
  /** Current sort (held by App so it survives folder navigation). */
  sort: DirectorySort;
  /** A column header was clicked. */
  onSort: (column: SortColumn) => void;
  /** Persisted column widths (`directoryColumnWidths`); validated on mount. */
  columnWidths?: unknown;
  /** A column resize finished (or was reset): the full set of user-set widths to persist. */
  onColumnWidthsChange: (widths: ColumnWidths) => void;
}

const COLUMNS: { column: SortColumn; label: string }[] = [
  { column: "name", label: "Name" },
  { column: "size", label: "Size" },
  { column: "modified", label: "Modified" },
];

const ROW_HEIGHT = 22;

/**
 * Per-directory listing (Name / Size / Modified). Double-click a folder drills in. Virtualized: a
 * folder with tens of thousands of entries (e.g. C:\Windows\WinSxS) only mounts the rows on screen.
 */
export function DirectoryList({
  path, nodes, onActivate, onContextMenu, resolveStyle, sort, onSort, columnWidths, onColumnWidthsChange,
}: Props) {
  const sorted = useMemo(() => sortDirectoryNodes(nodes, sort), [nodes, sort]);
  const [widths, setWidths] = useState<ColumnWidths>(() => normalizeColumnWidths(columnWidths));
  const template = gridTemplate(widths);

  // Mouse-driven drag on the header's right edge. The width tracks the pointer from the cell's
  // rendered width, so the filling Name column (wider than its minimum) doesn't jump when pinned.
  function startResize(e: React.MouseEvent, id: DirColumnId) {
    if (e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation(); // resizing, not sorting
    const cell = (e.currentTarget as HTMLElement).parentElement;
    const startWidth = cell?.getBoundingClientRect().width ?? widthOf(widths, id);
    const startX = e.clientX;
    let latest = widths;

    const onMove = (ev: MouseEvent) => {
      latest = { ...latest, [id]: clampWidth(id, startWidth + ev.clientX - startX) };
      setWidths(latest);
    };
    const onUp = () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
      if (latest !== widths) onColumnWidthsChange(latest);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  }

  // Back to the default width (and, for Name, back to filling the spare width).
  function resetWidth(id: DirColumnId) {
    const { [id]: _removed, ...rest } = widths;
    setWidths(rest);
    onColumnWidthsChange(rest);
  }
  return (
    <div className="dirlist">
      <div className="path-bar">{path}</div>
      {/* Keyed by folder, so a newly opened folder starts at the top rather than at the last one's offset. */}
      <VirtualList<DirectoryNode>
        key={path}
        className="dir-cols"
        items={sorted}
        rowHeight={ROW_HEIGHT}
        innerStyle={{ minWidth: totalWidth(widths) }}
        header={
          <div className="vlist-header" role="row" style={{ gridTemplateColumns: template, minWidth: totalWidth(widths) }}>
            {COLUMNS.map(({ column, label }) => {
              const active = sort.column === column;
              return (
                <div
                  key={column}
                  role="columnheader"
                  className={column === "size" ? "num" : undefined}
                  aria-sort={active ? (sort.direction === "asc" ? "ascending" : "descending") : "none"}
                >
                  <button type="button" className="th-sort" onClick={() => onSort(column)}>
                    {label}
                    <span className="sort-arrow" aria-hidden="true">
                      {active ? (sort.direction === "asc" ? "▲" : "▼") : ""}
                    </span>
                  </button>
                  <span
                    className="resizer"
                    role="separator"
                    aria-orientation="vertical"
                    title="Drag to resize, double-click to reset"
                    onMouseDown={(e) => startResize(e, column)}
                    onClick={(e) => e.stopPropagation()}
                    onDoubleClick={(e) => {
                      e.stopPropagation();
                      resetWidth(column);
                    }}
                  />
                </div>
              );
            })}
          </div>
        }
        renderRow={(n, _i, style) => {
          const { row, name } = splitRowAndNameStyle(resolveStyle(n.name, n.isDirectory));
          return (
            <div
              key={`${n.ref.catalogId}-${n.ref.entryIndex}`}
              className="vlist-row"
              style={{ ...style, ...row, gridTemplateColumns: template }}
              onDoubleClick={() => onActivate(n)}
              onContextMenu={(e) => {
                e.preventDefault();
                e.stopPropagation();
                onContextMenu(n, e.clientX, e.clientY);
              }}
            >
              <div className="cell"><StyledName nameStyle={name}>{n.name}</StyledName></div>
              <div className="cell num" title={sizeTitle(n.size)}>
                {sizeCell(n.isDirectory, n.size, n.isReparsePoint)}
              </div>
              <div className="cell">{formatDate(n.modified, n.isModifiedBad)}</div>
            </div>
          );
        }}
      />
    </div>
  );
}
