import { CatalogsResponse } from "../api/types";
import { mb } from "../format";

interface Props {
  catalogs: CatalogsResponse | null;
  resultCount: number;
  searchTiming: string;
  /** Queued and running jobs; the count links to the Jobs tab, where they can be cancelled. */
  activeJobCount?: number;
  onShowJobs?: () => void;
  /** Legacy catalogs that were not loaded ("N legacy catalogs not loaded"), with a Migrate… action; null for none. */
  legacyNotice?: string | null;
  onMigrate?: () => void;
}

/** Status bar parity: results, entries loaded, catalogs loaded, memory, timing; plus background job progress. */
export function StatusBar({
  catalogs,
  resultCount,
  searchTiming,
  activeJobCount = 0,
  onShowJobs,
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
      {activeJobCount > 0 && (
        <span className="rescan-status">
          <span>Jobs running</span>
          <button className="link" onClick={() => onShowJobs?.()} title="Show the Jobs tab">
            {activeJobCount}
          </button>
        </span>
      )}
    </div>
  );
}
