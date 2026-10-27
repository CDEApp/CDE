import { CatalogsResponse } from "../api/types";
import { mb } from "../format";

interface Props {
  catalogs: CatalogsResponse | null;
  resultCount: number;
  searchTiming: string;
  /** One line per running background job (rescan or new catalog): a stable key and the progress text. */
  jobs?: { key: string; text: string }[];
  onCancelJob?: (key: string) => void;
  /** Legacy catalogs that were not loaded ("N legacy catalogs not loaded"), with a Migrate… action; null for none. */
  legacyNotice?: string | null;
  onMigrate?: () => void;
}

/** Status bar parity: results, entries loaded, catalogs loaded, memory, timing; plus background job progress. */
export function StatusBar({
  catalogs,
  resultCount,
  searchTiming,
  jobs = [],
  onCancelJob,
  legacyNotice = null,
  onMigrate,
}: Props) {
  return (
    <div className="statusbar">
      <span>Results: {resultCount.toLocaleString()}</span>
      <span>Entries loaded: {(catalogs?.totalEntries ?? 0).toLocaleString()}</span>
      <span>Catalogs loaded: {catalogs?.catalogsLoaded ?? 0}</span>
      <span>Memory: {catalogs ? mb(catalogs.memoryBytes) : "-"}</span>
      <span>{searchTiming}</span>
      {legacyNotice && (
        <span className="rescan-status">
          {legacyNotice}
          <button onClick={() => onMigrate?.()} title="Convert the legacy .cde catalogs to the faster .cdex format">
            Migrate…
          </button>
        </span>
      )}
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
