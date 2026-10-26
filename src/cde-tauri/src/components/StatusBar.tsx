import { CatalogsResponse } from "../api/types";
import { mb } from "../format";

interface Props {
  catalogs: CatalogsResponse | null;
  resultCount: number;
  searchTiming: string;
}

/** Status bar parity: results, entries loaded, catalogs loaded, memory, timing. */
export function StatusBar({ catalogs, resultCount, searchTiming }: Props) {
  return (
    <div className="statusbar">
      <span>Results: {resultCount.toLocaleString()}</span>
      <span>Entries loaded: {(catalogs?.totalEntries ?? 0).toLocaleString()}</span>
      <span>Catalogs loaded: {catalogs?.catalogsLoaded ?? 0}</span>
      <span>Memory: {catalogs ? mb(catalogs.memoryBytes) : "-"}</span>
      <span>{searchTiming}</span>
    </div>
  );
}
