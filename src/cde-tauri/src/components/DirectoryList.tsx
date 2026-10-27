import { DirectoryNode } from "../api/types";
import type { StyleResolver } from "../colourRules";
import { formatDate, sizeCell } from "../format";

interface Props {
  path: string;
  nodes: DirectoryNode[];
  onActivate: (node: DirectoryNode) => void;
  onContextMenu: (node: DirectoryNode, x: number, y: number) => void;
  /** Colour-rule style for an entry name (undefined = no custom style). */
  resolveStyle: StyleResolver;
}

/** Per-directory listing (Name / Size / Modified). Double-click a folder drills in. */
export function DirectoryList({ path, nodes, onActivate, onContextMenu, resolveStyle }: Props) {
  return (
    <div className="grid dirlist">
      <div className="path-bar">{path}</div>
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Size</th>
            <th>Modified</th>
          </tr>
        </thead>
        <tbody>
          {nodes.map((n) => {
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
