import { useState } from "react";
import {
  createColumnHelper,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type Row,
  type SortingFn,
  type SortingState,
} from "@tanstack/react-table";
import { SearchResultRow } from "../api/types";
import { formatDate, sizeCell } from "../format";
import { VirtualList } from "./VirtualList";

interface Props {
  rows: SearchResultRow[];
  onActivate: (row: SearchResultRow) => void;
  onContextMenu: (row: SearchResultRow, x: number, y: number) => void;
}

const ROW_HEIGHT = 22;

const col = createColumnHelper<SearchResultRow>();

// Modified is sorted on the parsed timestamp; bad dates (NaN) are pushed to the end of the
// ascending order so they stay grouped rather than scattered through the list.
const modifiedSort: SortingFn<SearchResultRow> = (a, b, columnId) => {
  const av = a.getValue<number>(columnId);
  const bv = b.getValue<number>(columnId);
  const an = Number.isNaN(av);
  const bn = Number.isNaN(bv);
  if (an && bn) return 0;
  if (an) return 1;
  if (bn) return -1;
  return av === bv ? 0 : av < bv ? -1 : 1;
};

const columns = [
  col.accessor((r) => r.name, { id: "name", header: "Name", sortingFn: "text", size: 260 }),
  col.accessor((r) => r.size, { id: "size", header: "Size", sortingFn: "basic", size: 90 }),
  col.accessor((r) => Date.parse(r.modified), { id: "modified", header: "Modified", sortingFn: modifiedSort, size: 150 }),
  col.accessor((r) => r.catalogName, { id: "catalog", header: "Catalog", sortingFn: "text", size: 160 }),
  col.accessor((r) => r.parentPath, { id: "path", header: "Path", sortingFn: "text", size: 380 }),
];

const SORT_GLYPH = { asc: " ▲", desc: " ▼" } as const;

/** Virtualized streaming search results (Name / Size / Modified / Catalog / Path), sortable by column. */
export function ResultsList({ rows, onActivate, onContextMenu }: Props) {
  const [sorting, setSorting] = useState<SortingState>([]);
  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    columnResizeMode: "onChange",
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });

  const gridTemplateColumns = table.getVisibleLeafColumns().map((c) => `${c.getSize()}px`).join(" ");
  const totalWidth = table.getTotalSize();

  return (
    <VirtualList<Row<SearchResultRow>>
      className="results-cols"
      items={table.getRowModel().rows}
      rowHeight={ROW_HEIGHT}
      innerStyle={{ minWidth: totalWidth }}
      header={
        <div className="vlist-header results-cols" style={{ gridTemplateColumns, minWidth: totalWidth }}>
          {table.getFlatHeaders().map((h) => {
            const sorted = h.column.getIsSorted();
            return (
              <div
                key={h.id}
                className={`sortable${h.column.id === "size" ? " num" : ""}${sorted ? " sorted" : ""}`}
                onClick={h.column.getToggleSortingHandler()}
              >
                {String(h.column.columnDef.header)}
                {sorted ? SORT_GLYPH[sorted] : ""}
                <span
                  className={`resizer${h.column.getIsResizing() ? " resizing" : ""}`}
                  onMouseDown={h.getResizeHandler()}
                  onTouchStart={h.getResizeHandler()}
                  onClick={(e) => e.stopPropagation()}
                />
              </div>
            );
          })}
        </div>
      }
      renderRow={(row, i, style) => {
        const r = row.original;
        return (
          <div
            key={`${r.ref.catalogId}-${r.ref.entryIndex}-${i}`}
            className={`vlist-row${r.isDirectory ? " dir" : ""}`}
            style={{ ...style, gridTemplateColumns }}
            onDoubleClick={() => onActivate(r)}
            onContextMenu={(e) => {
              e.preventDefault();
              e.stopPropagation();
              onContextMenu(r, e.clientX, e.clientY);
            }}
          >
            <div className="cell">{r.name}</div>
            <div className="cell num">{sizeCell(r.isDirectory, r.size, r.isReparsePoint)}</div>
            <div className="cell">{formatDate(r.modified, r.isModifiedBad)}</div>
            <div className="cell">{r.catalogName}</div>
            <div className="cell">{r.parentPath}</div>
          </div>
        );
      }}
    />
  );
}
