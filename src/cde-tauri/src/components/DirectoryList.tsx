import { useEffect, useMemo, useRef, useState } from "react";
import { DirectoryNode } from "../api/types";
import { type DirectorySort, type SortColumn, sortDirectoryNodes } from "../directorySort";
import { splitRowAndNameStyle, type StyleResolver } from "../colourRules";
import { formatDate, sizeCell, sizeTitle } from "../format";
import {
  clampWidth,
  type ColumnWidths,
  type DirColumnId,
  DIR_MIN_WIDTH,
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
  /** Open the parent folder. When given, a ".." row leads the listing. */
  onUp?: () => void;
}

const PARENT_ROW = { parent: true } as const;
type ParentRow = typeof PARENT_ROW;
type Row = DirectoryNode | ParentRow;
const isParentRow = (r: Row): r is ParentRow => r === PARENT_ROW;

function onEnter(run: () => void) {
  return (e: React.KeyboardEvent) => {
    if (e.key !== "Enter") return;
    e.preventDefault();
    run();
  };
}

const COLUMNS: { column: SortColumn; label: string }[] = [
  { column: "name", label: "Name" },
  { column: "size", label: "Size" },
  { column: "modified", label: "Modified" },
];

const ROW_HEIGHT = 22;
const KEY_STEP = 10;
const KEY_STEP_LARGE = 50;

/**
 * Per-directory listing (Name / Size / Modified). Double-click or Enter on a folder drills in; the
 * ".." row goes up. Virtualized: a
 * folder with tens of thousands of entries (e.g. C:\Windows\WinSxS) only mounts the rows on screen.
 */
export function DirectoryList({
  path, nodes, onActivate, onContextMenu, resolveStyle, sort, onSort, columnWidths, onColumnWidthsChange, onUp,
}: Props) {
  const sorted = useMemo(() => sortDirectoryNodes(nodes, sort), [nodes, sort]);
  const rows = useMemo<Row[]>(() => (onUp ? [PARENT_ROW, ...sorted] : sorted), [sorted, onUp]);
  // Enter on ".." remounts the listing for the parent, which drops focus. Put it on the new ".." so
  // Enter keeps going up. Cleared after the folder changes, so no other navigation takes focus.
  const refocusParent = useRef(false);
  useEffect(() => {
    refocusParent.current = false;
  }, [path]);
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

  // Keyboard resize from the focused handle; the current width is the pinned one, else the default.
  function nudge(id: DirColumnId, width: number) {
    const next = { ...widths, [id]: clampWidth(id, width) };
    setWidths(next);
    onColumnWidthsChange(next);
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
      <VirtualList<Row>
        key={path}
        className="dir-cols"
        items={rows}
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
                    aria-label={`Resize ${label} column`}
                    aria-valuenow={widthOf(widths, column)}
                    aria-valuemin={DIR_MIN_WIDTH[column]}
                    tabIndex={0} // NOSONAR S6845: a focusable separator with aria-valuenow is the WAI-ARIA window splitter widget, resized with the arrow keys
                    title="Drag or use the arrow keys to resize, double-click or Home to reset"
                    onMouseDown={(e) => startResize(e, column)}
                    onKeyDown={(e) => {
                      const step = e.shiftKey ? KEY_STEP_LARGE : KEY_STEP;
                      const current = widthOf(widths, column);
                      if (e.key === "ArrowRight") nudge(column, current + step);
                      else if (e.key === "ArrowLeft") nudge(column, current - step);
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
                </div>
              );
            })}
          </div>
        }
        renderRow={(n, _i, style) => {
          if (isParentRow(n)) {
            const up = onUp!;
            return (
              <div
                key="parent"
                ref={(el) => {
                  if (el && refocusParent.current) el.focus();
                }}
                className="vlist-row"
                tabIndex={-1}
                title="Up to the parent folder"
                style={{ ...style, gridTemplateColumns: template }}
                onDoubleClick={up}
                onKeyDown={onEnter(() => {
                  refocusParent.current = true;
                  up();
                })}
                onContextMenu={(e) => e.preventDefault()}
              >
                <div className="cell">..</div>
                <div className="cell" />
                <div className="cell" />
              </div>
            );
          }
          const { row, name } = splitRowAndNameStyle(resolveStyle(n.name, n.isDirectory));
          return (
            <div
              key={`${n.ref.catalogId}-${n.ref.entryIndex}`}
              className="vlist-row"
              tabIndex={-1}
              style={{ ...style, ...row, gridTemplateColumns: template }}
              onDoubleClick={() => onActivate(n)}
              onKeyDown={onEnter(() => onActivate(n))}
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
