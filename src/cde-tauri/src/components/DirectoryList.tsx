import { DirectoryNode } from "../api/types";
import { formatDate, sizeCell } from "../format";

interface Props {
  path: string;
  nodes: DirectoryNode[];
  onActivate: (node: DirectoryNode) => void;
  onContextMenu: (node: DirectoryNode, x: number, y: number) => void;
}

/** Per-directory listing (Name / Size / Modified). Double-click a folder drills in. */
export function DirectoryList({ path, nodes, onActivate, onContextMenu }: Props) {
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
          {nodes.map((n) => (
            <tr
              key={`${n.ref.catalogId}-${n.ref.entryIndex}`}
              className={n.isDirectory ? "dir" : ""}
              onDoubleClick={() => onActivate(n)}
              onContextMenu={(e) => {
                e.preventDefault();
                onContextMenu(n, e.clientX, e.clientY);
              }}
            >
              <td>{n.name}</td>
              <td className="num">{sizeCell(n.isDirectory, n.size, n.isReparsePoint)}</td>
              <td>{formatDate(n.modified, n.isModifiedBad)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
