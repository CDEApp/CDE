import { CatalogsResponse } from "../api/types";
import { mb } from "../format";

interface Props {
  catalogs: CatalogsResponse | null;
  resultCount: number;
  searchTiming: string;
  /** One line per running rescan (catalog name + entries scanned). */
  rescans?: { catalogId: number; text: string }[];
  onCancelRescan?: (catalogId: number) => void;
}

/** Status bar parity: results, entries loaded, catalogs loaded, memory, timing; plus rescan progress. */
export function StatusBar({ catalogs, resultCount, searchTiming, rescans = [], onCancelRescan }: Props) {
  return (
    <div className="statusbar">
      <span>Results: {resultCount.toLocaleString()}</span>
      <span>Entries loaded: {(catalogs?.totalEntries ?? 0).toLocaleString()}</span>
      <span>Catalogs loaded: {catalogs?.catalogsLoaded ?? 0}</span>
      <span>Memory: {catalogs ? mb(catalogs.memoryBytes) : "-"}</span>
      <span>{searchTiming}</span>
      {rescans.map((r) => (
        <span key={r.catalogId} className="rescan-status">
          {r.text}
          <button onClick={() => onCancelRescan?.(r.catalogId)} title="Cancel this rescan; the catalog stays as it was">
            Cancel
          </button>
        </span>
      ))}
    </div>
  );
}
