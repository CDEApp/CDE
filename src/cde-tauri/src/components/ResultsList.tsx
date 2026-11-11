import { useEffect, useId, useMemo, useRef, useState, type Dispatch, type ReactNode } from "react";
import {
  columnOrderingFeature,
  columnResizingFeature,
  columnSizingFeature,
  createColumnHelper,
  createSortedRowModel,
  rowSortingFeature,
  sortFn_basic,
  sortFn_text,
  tableFeatures,
  useTable,
  type Row,
  type SortFn,
} from "@tanstack/react-table";
import { refToString, SearchResultRow } from "../api/types";
import { moveColumn, normalizeColumnOrder } from "../columnOrder";
import { splitRowAndNameStyle, type StyleResolver } from "../colourRules";
import { formatDate, sizeCell, sizeTitle } from "../format";
import type { RowSelection, SelectionAction } from "../rowSelection";
import { StyledName } from "./StyledName";
import { VirtualList, type VirtualListHandle } from "./VirtualList";

interface Props {
  rows: SearchResultRow[];
  onActivate: (row: SearchResultRow) => void;
  onContextMenu: (row: SearchResultRow, x: number, y: number) => void;
  /** Persisted column order (column ids); reconciled against the current columns on mount. */
  columnOrder?: unknown;
  onColumnOrderChange: (order: string[]) => void;
  /** Colour-rule style for an entry name (undefined = no custom style). */
  resolveStyle: StyleResolver;
  selection: RowSelection;
  dispatchSelection: Dispatch<SelectionAction>;
  onCopy?: () => void;
  visibleRowsRef?: { current: readonly SearchResultRow[] };
}

const ROW_HEIGHT = 22;
/** Pixels the pointer must travel before a header press becomes a column drag (not a sort click). */
const DRAG_THRESHOLD = 5;

// v9 tables opt into each feature; built-in sort fns are registered by name so the bundle only
// carries the ones the columns use.
const features = tableFeatures({
  rowSortingFeature,
  columnSizingFeature,
  columnResizingFeature,
  columnOrderingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: { text: sortFn_text, basic: sortFn_basic },
});
type Features = typeof features;

const col = createColumnHelper<Features, SearchResultRow>();

// Modified is sorted on the parsed timestamp; bad dates (NaN) are pushed to the end of the
// ascending order so they stay grouped rather than scattered through the list.
const modifiedSort: SortFn<Features, SearchResultRow> = (a, b, columnId) => {
  const av = a.getValue<number>(columnId);
  const bv = b.getValue<number>(columnId);
  const an = Number.isNaN(av);
  const bn = Number.isNaN(bv);
  if (an && bn) return 0;
  if (an) return 1;
  if (bn) return -1;
  return av === bv ? 0 : av < bv ? -1 : 1;
};

const columns = col.columns([
  col.accessor((r) => r.name, { id: "name", header: "Name", sortFn: "text", size: 260 }),
  col.accessor((r) => r.size, { id: "size", header: "Size", sortFn: "basic", size: 90 }),
  col.accessor((r) => Date.parse(r.modified), { id: "modified", header: "Modified", sortFn: modifiedSort, size: 150 }),
  col.accessor((r) => r.catalogName, { id: "catalog", header: "Catalog", sortFn: "text", size: 160 }),
  col.accessor((r) => r.parentPath, { id: "path", header: "Path", sortFn: "text", size: 380 }),
]);
const DEFAULT_COLUMN_IDS = columns.map((c) => c.id as string);

/** The column that absorbs spare pane width, wherever it has been moved to. */
const FILL_COLUMN = "path";

// Row cell rendering per column id, so cells follow the current column order.
const CELLS: Record<
  string,
  { className: string; render: (r: SearchResultRow) => ReactNode; title?: (r: SearchResultRow) => string }
> = {
  name: { className: "cell", render: (r) => r.name },
  size: {
    className: "cell num",
    render: (r) => sizeCell(r.isDirectory, r.size, r.isReparsePoint),
    title: (r) => sizeTitle(r.size),
  },
  modified: { className: "cell", render: (r) => formatDate(r.modified, r.isModifiedBad) },
  catalog: { className: "cell", render: (r) => r.catalogName },
  path: { className: "cell", render: (r) => r.parentPath },
};

const SORT_ARIA = { none: "none", asc: "ascending", desc: "descending" } as const;
const MIN_COLUMN_WIDTH = 30;
const KEY_STEP = 10;
const KEY_STEP_LARGE = 50;
const SORT_GLYPH = { asc: " ▲", desc: " ▼" } as const;

/**
 * Virtualized streaming search results (Name / Size / Modified / Catalog / Path): click a header
 * to sort, drag its right edge to resize, drag the header itself sideways to reorder.
 */
export function ResultsList({
  rows,
  onActivate,
  onContextMenu,
  columnOrder,
  onColumnOrderChange,
  resolveStyle,
  selection,
  dispatchSelection,
  visibleRowsRef,
  onCopy,
}: Props) {
  const [initialOrder] = useState(() => normalizeColumnOrder(columnOrder, DEFAULT_COLUMN_IDS));

  // Sorting, sizes and order live in the table's own store; useTable re-renders on their change.
  const table = useTable({
    features,
    data: rows,
    columns,
    columnResizeMode: "onChange",
    initialState: { columnOrder: initialOrder },
  });

  const headerRef = useRef<HTMLDivElement>(null);
  const suppressClickRef = useRef(false);
  const [drag, setDrag] = useState<{ id: string; drop: number } | null>(null);

  const sortedRows = table.getRowModel().rows;
  const visibleRows = useMemo(() => sortedRows.map((r) => r.original), [sortedRows]);
  const order = useMemo(() => visibleRows.map((r) => refToString(r.ref)), [visibleRows]);
  const listRef = useRef<VirtualListHandle>(null);
  const idPrefix = useId();
  const rowId = (key: string) => `${idPrefix}-row-${key}`;

  useEffect(() => {
    if (visibleRowsRef) visibleRowsRef.current = visibleRows;
  }, [visibleRows, visibleRowsRef]);

  const orderRef = useRef(order);
  orderRef.current = order;
  // Bumped by every keyboard move, so Home or End on an already-active row still scrolls it back into view.
  const [revealNonce, setRevealNonce] = useState(0);
  useEffect(() => {
    if (selection.active === null) return;
    const index = orderRef.current.indexOf(selection.active);
    if (index >= 0) listRef.current?.scrollToIndex(index, "nearest");
  }, [selection.active, revealNonce]);
  const move = (to: { delta: number } | "start" | "end", extend: boolean) => {
    dispatchSelection({ type: "move", to, extend, order });
    setRevealNonce((n) => n + 1);
  };

  function onGridKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.target !== e.currentTarget || e.altKey) return;
    const mod = e.ctrlKey || e.metaKey;
    const extend = e.shiftKey;
    if (mod && e.key.toLowerCase() === "a") dispatchSelection({ type: "selectAll", order });
    else if (mod && e.key.toLowerCase() === "c" && onCopy) onCopy();
    else if (mod) return;
    else if (e.key === "ArrowDown") move({ delta: 1 }, extend);
    else if (e.key === "ArrowUp") move({ delta: -1 }, extend);
    else if (e.key === "Home") move("start", extend);
    else if (e.key === "End") move("end", extend);
    else if (e.key === " ") dispatchSelection({ type: "toggleActive" });
    else if (e.key === "Escape") dispatchSelection({ type: "clear" });
    else return;
    e.preventDefault();
  }

  const leafColumns = table.getAllLeafColumns();
  const columnIds = leafColumns.map((c) => c.id);

  // Fixed widths for every column but Path, which — until the user resizes it — grows into any
  // spare pane width so long paths aren't ellipsised beside empty space. Its size is then only a
  // minimum, so when the columns exceed the pane the row still overflows and scrolls horizontally.
  const fillActive = table.state.columnSizing[FILL_COLUMN] === undefined;
  const gridTemplateColumns = leafColumns
    .map((c) => (c.id === FILL_COLUMN && fillActive ? `minmax(${c.getSize()}px, 1fr)` : `${c.getSize()}px`))
    .join(" ");
  const totalWidth = table.getTotalSize();

  // Insertion gap (0..n) under the pointer: past a header cell's midpoint means "after it".
  function dropIndexAt(clientX: number): number {
    const cells = headerRef.current ? Array.from(headerRef.current.children) : [];
    let index = 0;
    cells.forEach((cell, i) => {
      const r = cell.getBoundingClientRect();
      if (clientX > r.left + r.width / 2) index = i + 1;
    });
    return index;
  }

  // Mouse-driven (not HTML5 drag-and-drop, which the Tauri webview reserves for file drops). A
  // press that never moves past the threshold stays a plain click and sorts as before.
  function startReorder(e: React.MouseEvent, id: string) {
    if (e.button !== 0) return;
    const startX = e.clientX;
    const startOrder = columnIds;
    let active = false;
    let drop = -1;

    const onMove = (ev: MouseEvent) => {
      if (!active) {
        if (Math.abs(ev.clientX - startX) < DRAG_THRESHOLD) return;
        active = true;
      }
      ev.preventDefault();
      drop = dropIndexAt(ev.clientX);
      setDrag({ id, drop });
    };
    const onUp = () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
      if (!active) return;
      // The click that follows this mouseup must not also toggle sorting. Cleared next tick in
      // case the button was released off the header (where no click follows).
      suppressClickRef.current = true;
      setTimeout(() => (suppressClickRef.current = false), 0);
      setDrag(null);
      const next = moveColumn(startOrder, id, drop);
      if (next !== startOrder) {
        table.setColumnOrder([...next]);
        onColumnOrderChange([...next]);
      }
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  }

  // Keyboard column resize; a filling Path column is pinned to its rendered width by setting it.
  function nudge(id: string, width: number) {
    table.setColumnSizing((old) => ({ ...old, [id]: Math.max(MIN_COLUMN_WIDTH, width) }));
  }

  // Show the drop marker only where releasing would actually move the column.
  const marker = drag && moveColumn(columnIds, drag.id, drag.drop) !== columnIds ? drag.drop : -1;

  return (
    <VirtualList<Row<Features, SearchResultRow>>
      className="results-cols"
      items={sortedRows}
      handleRef={listRef}
      rootProps={{
        tabIndex: 0,
        onKeyDown: onGridKeyDown,
        "aria-multiselectable": true,
        "aria-activedescendant": selection.active === null ? undefined : rowId(selection.active),
      }}
      rowHeight={ROW_HEIGHT}
      innerStyle={{ minWidth: totalWidth }}
      role="grid"
      ariaLabel="Search results"
      header={
        <div
          ref={headerRef}
          role="row"
          className={`vlist-header results-cols${drag ? " reordering" : ""}`}
          style={{ gridTemplateColumns, minWidth: totalWidth }}
        >
          {table.getFlatHeaders().map((h, i, all) => {
            const sorted = h.column.getIsSorted();
            const resizeHandler = h.getResizeHandler();
            // A filling Path column is wider than its size (which is only its minimum), so a drag
            // would start from the wrong width and appear dead. Pin it to its rendered width
            // first; the resize then tracks the pointer and Path stays fixed-width afterwards.
            const resize = (e: React.MouseEvent | React.TouchEvent) => {
              if (h.column.id === FILL_COLUMN && fillActive) {
                const cell = (e.currentTarget as HTMLElement).parentElement;
                const width = Math.round(cell?.getBoundingClientRect().width ?? h.column.getSize());
                table.setColumnSizing((old) => ({ ...old, [FILL_COLUMN]: width }));
              }
              resizeHandler(e);
            };
            const classes = [
              "sortable",
              h.column.id === "size" ? "num" : "",
              sorted ? "sorted" : "",
              drag?.id === h.column.id ? "dragging" : "",
              marker === i ? "drop-before" : "",
              marker === all.length && i === all.length - 1 ? "drop-after" : "",
            ];
            return (
              <div
                key={h.id}
                role="columnheader"
                aria-sort={SORT_ARIA[sorted || "none"]}
                className={classes.filter(Boolean).join(" ")}
              >
                <button
                  type="button"
                  className="sort-btn"
                  title="Click to sort, drag to move"
                  onMouseDown={(e) => startReorder(e, h.column.id)}
                  onClick={(e) => {
                    if (suppressClickRef.current) return;
                    h.column.getToggleSortingHandler()?.(e);
                  }}
                >
                  {String(h.column.columnDef.header)}
                  {sorted ? SORT_GLYPH[sorted] : ""}
                </button>
                <span
                  className={`resizer${h.column.getIsResizing() ? " resizing" : ""}`}
                  role="slider"
                  aria-orientation="horizontal"
                  aria-label={`Resize ${String(h.column.columnDef.header)} column`}
                  aria-valuenow={h.column.getSize()}
                  aria-valuemin={MIN_COLUMN_WIDTH}
                  tabIndex={0}
                  onMouseDown={(e) => {
                    e.stopPropagation(); // resizing, not reordering
                    resize(e);
                  }}
                  onTouchStart={resize}
                  onKeyDown={(e) => {
                    const step = e.shiftKey ? KEY_STEP_LARGE : KEY_STEP;
                    if (e.key === "ArrowRight") nudge(h.column.id, h.column.getSize() + step);
                    else if (e.key === "ArrowLeft") nudge(h.column.id, h.column.getSize() - step);
                    else if (e.key === "Home") h.column.resetSize();
                    else return;
                    e.preventDefault();
                  }}
                  // Back to the default width (and, for Path, back to filling the spare width).
                  onDoubleClick={(e) => {
                    e.stopPropagation();
                    h.column.resetSize();
                  }}
                  title="Drag or use the arrow keys to resize, double-click or Home to reset"
                />
              </div>
            );
          })}
        </div>
      }
      renderRow={(row, i, style) => {
        const r = row.original;
        const key = refToString(r.ref);
        const isSelected = selection.selected.has(key);
        // Selected rows keep the selection look: no rule styles (transparent gradient text must not hide the name).
        const { row: rowStyle, name: nameStyle } = isSelected
          ? { row: undefined, name: undefined }
          : splitRowAndNameStyle(resolveStyle(r.name, r.isDirectory));
        const click = (mode: "replace" | "toggle" | "range") => dispatchSelection({ type: "click", key, mode, order });
        return (
          <div
            key={`${key}-${i}`}
            id={rowId(key)}
            role="row"
            aria-selected={isSelected}
            aria-rowindex={i + 2}
            className={`vlist-row${r.isDirectory ? " dir" : ""}${isSelected ? " selected" : ""}${selection.active === key ? " active" : ""}`}
            style={{ ...style, ...rowStyle, gridTemplateColumns }}
            onClick={(e) => click(e.shiftKey ? "range" : e.ctrlKey || e.metaKey ? "toggle" : "replace")}
            onDoubleClick={() => onActivate(r)}
            onContextMenu={(e) => {
              e.preventDefault();
              e.stopPropagation();
              if (!isSelected) click("replace");
              onContextMenu(r, e.clientX, e.clientY);
            }}
          >
            {columnIds.map((id) => (
              <div key={id} role="gridcell" className={CELLS[id].className} title={CELLS[id].title?.(r)}>
                <StyledName nameStyle={id === "name" ? nameStyle : undefined}>{CELLS[id].render(r)}</StyledName>
              </div>
            ))}
          </div>
        );
      }}
    />
  );
}
