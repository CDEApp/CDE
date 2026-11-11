import { save } from "@tauri-apps/plugin-dialog";
import { writeFile } from "@tauri-apps/plugin-fs";
import { notifications } from "@mantine/notifications";
import type { SearchResultRow } from "../api/types";
import type { StyleResolver } from "../colourRules";
import { encodeCsv, exportFileName, resultsToCsv } from "../csvExport";
import { ResultsList } from "./ResultsList";

interface Props {
  rows: SearchResultRow[];
  busy: boolean;
  /** Set when results point into a catalog rescanned since the search; offers Search again. */
  staleBanner: string | null;
  onSearchAgain: () => void;
  limitReachedCount?: number | null;
  onRaiseLimit?: () => void;
  resolveStyle: StyleResolver;
  /** The persisted column ids in display order, read when the list mounts and at each export. */
  getColumnOrder: () => string[] | undefined;
  onColumnOrderChange: (order: string[]) => void;
  onActivate: (row: SearchResultRow) => void;
  onContextMenu: (row: SearchResultRow, x: number, y: number) => void;
}

/** The Search Results tab: the stale-results banner, Export results…, and the results list. */
export function ResultsPane({
  rows, busy, staleBanner, onSearchAgain, limitReachedCount = null, onRaiseLimit, getColumnOrder, ...list
}: Props) {
  // Every streamed row (all held in `rows`) as UTF-8 CSV, columns in the list's current order: a column
  // drag saves the order without re-rendering this pane, so it is read at click time.
  async function exportResults() {
    try {
      const path = await save({
        title: "Export search results",
        defaultPath: exportFileName(new Date()),
        filters: [{ name: "CSV", extensions: ["csv"] }],
      });
      if (!path) return;
      await writeFile(path, encodeCsv(resultsToCsv(rows, getColumnOrder())));
      notifications.show({
        color: "green",
        title: "Results exported",
        message: `${rows.length.toLocaleString()} rows written to ${path}`,
      });
    } catch (e) {
      notifications.show({ color: "red", title: "Export failed", message: e instanceof Error ? e.message : String(e) });
    }
  }

  return (
    <div className="results-pane">
      {staleBanner && (
        <div className="stale-banner" role="status">
          <span>{staleBanner}</span>
          <button disabled={busy} onClick={onSearchAgain}>
            Search again
          </button>
        </div>
      )}
      {limitReachedCount !== null && (
        <div className="stale-banner" role="status">
          <span>
            {limitReachedCount.toLocaleString()} results (limit reached, more exist). Change the Limit in the search bar,
            or raise it here.
          </span>
          <button disabled={busy} onClick={onRaiseLimit}>
            Raise limit
          </button>
        </div>
      )}
      <div className="results-toolbar">
        <button
          disabled={busy || rows.length === 0}
          title={busy ? "Wait for the search to finish" : rows.length === 0 ? "No results to export" : undefined}
          onClick={() => void exportResults()}
        >
          Export results…
        </button>
      </div>
      <div className="results-fill">
        <ResultsList
          rows={rows}
          columnOrder={getColumnOrder()}
          partialSortNotice={limitReachedCount === null ? undefined : `sorted within the first ${limitReachedCount.toLocaleString()} matches`}
          {...list}
        />
      </div>
    </div>
  );
}
