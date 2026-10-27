import { CatalogInfo } from "../api/types";
import { toHRString } from "../format";

interface Props {
  catalogs: CatalogInfo[];
  onActivate: (catalog: CatalogInfo) => void;
  onContextMenu?: (catalog: CatalogInfo, x: number, y: number) => void;
  /** Catalog ids being rescanned right now (their rows are dimmed). */
  rescanningIds?: ReadonlySet<number>;
}

/** Catalog list view. Double-click a catalog to make it the directory-tree root. */
export function CatalogList({ catalogs, onActivate, onContextMenu, rescanningIds }: Props) {
  return (
    <div className="grid cataloglist">
      <table>
        <thead>
          <tr>
            <th>Root</th>
            <th>Volume</th>
            <th>Dirs</th>
            <th>Files</th>
            <th>Size</th>
            <th>Drive</th>
            <th>Catalog</th>
          </tr>
        </thead>
        <tbody>
          {catalogs.map((c) => (
            <tr
              key={c.catalogId}
              className={rescanningIds?.has(c.catalogId) ? "rescanning" : undefined}
              onDoubleClick={() => onActivate(c)}
              onContextMenu={(e) => {
                if (!onContextMenu) return;
                e.preventDefault();
                e.stopPropagation();
                onContextMenu(c, e.clientX, e.clientY);
              }}
            >
              <td>{c.rootPath}</td>
              <td>{c.volumeName}</td>
              <td className="num">{c.dirEntryCount.toLocaleString()}</td>
              <td className="num">{c.fileEntryCount.toLocaleString()}</td>
              <td className="num">{toHRString(c.rootSize)}</td>
              <td>{c.driveLetterHint}</td>
              <td>{c.actualFileName}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
