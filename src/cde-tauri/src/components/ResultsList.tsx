import { SearchResultRow } from "../api/types";
import { formatDate, sizeCell } from "../format";

interface Props {
  rows: SearchResultRow[];
  onActivate: (row: SearchResultRow) => void;
  onContextMenu: (row: SearchResultRow, x: number, y: number) => void;
}

/** Streaming search results table (Name / Size / Modified / Catalog / Path). */
export function ResultsList({ rows, onActivate, onContextMenu }: Props) {
  return (
    <div className="grid results">
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Size</th>
            <th>Modified</th>
            <th>Catalog</th>
            <th>Path</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr
              key={`${row.ref.catalogId}-${row.ref.entryIndex}-${i}`}
              className={row.isDirectory ? "dir" : ""}
              onDoubleClick={() => onActivate(row)}
              onContextMenu={(e) => {
                e.preventDefault();
                onContextMenu(row, e.clientX, e.clientY);
              }}
            >
              <td>{row.name}</td>
              <td className="num">{sizeCell(row.isDirectory, row.size, row.isReparsePoint)}</td>
              <td>{formatDate(row.modified, row.isModifiedBad)}</td>
              <td>{row.catalogName}</td>
              <td>{row.parentPath}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
