import { useMemo } from "react";
import { DirectoryNode } from "../api/types";
import { type DirectorySort, type SortColumn, sortDirectoryNodes } from "../directorySort";
import { splitRowAndNameStyle, type StyleResolver } from "../colourRules";
import { formatDate, sizeCell, sizeTitle } from "../format";
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
export function DirectoryList({ path, nodes, onActivate, onContextMenu, resolveStyle, sort, onSort }: Props) {
  const sorted = useMemo(() => sortDirectoryNodes(nodes, sort), [nodes, sort]);
  return (
    <div className="dirlist">
      <div className="path-bar">{path}</div>
      {/* Keyed by folder, so a newly opened folder starts at the top rather than at the last one's offset. */}
      <VirtualList<DirectoryNode>
        key={path}
        className="dir-cols"
        items={sorted}
        rowHeight={ROW_HEIGHT}
        header={
          <div className="vlist-header" role="row">
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
              style={{ ...style, ...row }}
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
