import { useMemo } from "react";
import { DirectoryNode } from "../api/types";
import { type DirectorySort, type SortColumn, sortDirectoryNodes } from "../directorySort";
import type { StyleResolver } from "../colourRules";
import { formatDate, sizeCell } from "../format";

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
}

const COLUMNS: { column: SortColumn; label: string }[] = [
  { column: "name", label: "Name" },
  { column: "size", label: "Size" },
  { column: "modified", label: "Modified" },
];

/** Per-directory listing (Name / Size / Modified). Double-click a folder drills in. */
export function DirectoryList({ path, nodes, onActivate, onContextMenu, resolveStyle, sort, onSort }: Props) {
  const sorted = useMemo(() => sortDirectoryNodes(nodes, sort), [nodes, sort]);
  return (
    <div className="grid dirlist">
      <div className="path-bar">{path}</div>
      <table>
        <thead>
          <tr>
            {COLUMNS.map(({ column, label }) => {
              const active = sort.column === column;
              return (
                <th
                  key={column}
                  className={column === "size" ? "num" : undefined}
                  aria-sort={active ? (sort.direction === "asc" ? "ascending" : "descending") : "none"}
                >
                  <button type="button" className="th-sort" onClick={() => onSort(column)}>
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
          {sorted.map((n) => {
            const style = resolveStyle(n.name, n.isDirectory);
            return (
            <tr
              key={`${n.ref.catalogId}-${n.ref.entryIndex}`}
              onDoubleClick={() => onActivate(n)}
              onContextMenu={(e) => {
                e.preventDefault();
                e.stopPropagation();
                onContextMenu(n, e.clientX, e.clientY);
              }}
            >
              <td style={style}>{n.name}</td>
              <td className="num" style={style}>{sizeCell(n.isDirectory, n.size, n.isReparsePoint)}</td>
              <td style={style}>{formatDate(n.modified, n.isModifiedBad)}</td>
            </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
