import { SearchResultRow } from "../api/types";
import { formatDate, sizeCell } from "../format";
import { VirtualList } from "./VirtualList";

interface Props {
  rows: SearchResultRow[];
  onActivate: (row: SearchResultRow) => void;
  onContextMenu: (row: SearchResultRow, x: number, y: number) => void;
}

const ROW_HEIGHT = 22;

/** Virtualized streaming search results (Name / Size / Modified / Catalog / Path). */
export function ResultsList({ rows, onActivate, onContextMenu }: Props) {
  return (
    <VirtualList
      className="results-cols"
      items={rows}
      rowHeight={ROW_HEIGHT}
      header={
        <div className="vlist-header results-cols">
          <div>Name</div>
          <div className="num">Size</div>
          <div>Modified</div>
          <div>Catalog</div>
          <div>Path</div>
        </div>
      }
      renderRow={(row, i, style) => (
        <div
          key={`${row.ref.catalogId}-${row.ref.entryIndex}-${i}`}
          className={`vlist-row${row.isDirectory ? " dir" : ""}`}
          style={style}
          onDoubleClick={() => onActivate(row)}
          onContextMenu={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onContextMenu(row, e.clientX, e.clientY);
          }}
        >
          <div className="cell">{row.name}</div>
          <div className="cell num">{sizeCell(row.isDirectory, row.size, row.isReparsePoint)}</div>
          <div className="cell">{formatDate(row.modified, row.isModifiedBad)}</div>
          <div className="cell">{row.catalogName}</div>
          <div className="cell">{row.parentPath}</div>
        </div>
      )}
    />
  );
}
