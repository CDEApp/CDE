import { CatalogsResponse } from "../api/types";
import { mb } from "../format";

interface Props {
  catalogs: CatalogsResponse | null;
  resultCount: number;
  searchTiming: string;
  /** One line per running background job (rescan or new catalog): a stable key and the progress text. */
  jobs?: { key: string; text: string }[];
  onCancelJob?: (key: string) => void;
}

/** Status bar parity: results, entries loaded, catalogs loaded, memory, timing; plus background job progress. */
export function StatusBar({ catalogs, resultCount, searchTiming, jobs = [], onCancelJob }: Props) {
  return (
    <div className="statusbar">
      <span>Results: {resultCount.toLocaleString()}</span>
      <span>Entries loaded: {(catalogs?.totalEntries ?? 0).toLocaleString()}</span>
      <span>Catalogs loaded: {catalogs?.catalogsLoaded ?? 0}</span>
      <span>Memory: {catalogs ? mb(catalogs.memoryBytes) : "-"}</span>
      <span>{searchTiming}</span>
      {jobs.map((j) => (
        <span key={j.key} className="rescan-status">
          {j.text}
          <button onClick={() => onCancelJob?.(j.key)} title="Cancel this job; nothing is changed or left behind">
            Cancel
          </button>
        </span>
      ))}
    </div>
  );
}
