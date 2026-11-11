import { useRef } from "react";
import { save } from "@tauri-apps/plugin-dialog";
import { writeFile } from "@tauri-apps/plugin-fs";
import { notifications } from "@mantine/notifications";
import { refToString, type SearchResultRow } from "../api/types";
import type { StyleResolver } from "../colourRules";
import { rowsToClipboardText } from "../copyText";
import { encodeCsv, exportFileName, resultsToCsv } from "../csvExport";
import type { RowSelectionApi } from "../hooks/useRowSelection";
import { selectedInOrder } from "../rowSelection";
import { ResultsList } from "./ResultsList";

interface Props {
  rows: SearchResultRow[];
  rowSelection: RowSelectionApi;
  busy: boolean;
  /** Set when results point into a catalog rescanned since the search; offers Search again. */
  staleBanner: string | null;
  onSearchAgain: () => void;
  resolveStyle: StyleResolver;
  /** The persisted column ids in display order, read when the list mounts and at each export. */
  getColumnOrder: () => string[] | undefined;
  onColumnOrderChange: (order: string[]) => void;
  onActivate: (row: SearchResultRow) => void;
  /** `selection` is the rows the menu acts on, in on-screen order: the whole selection if `row` is in it, else just `row`. */
  onContextMenu: (row: SearchResultRow, selection: readonly SearchResultRow[], x: number, y: number) => void;
}

/** The Search Results tab: the stale-results banner, Export results…, and the results list. */
export function ResultsPane({ rows, busy, staleBanner, onSearchAgain, getColumnOrder, rowSelection, onContextMenu, ...list }: Props) {
  const visibleRowsRef = useRef<readonly SearchResultRow[]>(rows);
  const selectedCount = rowSelection.selection.selected.size;
  const selectedRows = () => selectedInOrder(visibleRowsRef.current, (r) => refToString(r.ref), rowSelection.selection);

  // The rows as UTF-8 CSV, columns in the list's current order: a column drag saves the order without
  // re-rendering this pane, so it is read at click time.
  async function exportRows(toExport: readonly SearchResultRow[]) {
    try {
      const path = await save({
        title: "Export search results",
        defaultPath: exportFileName(new Date()),
        filters: [{ name: "CSV", extensions: ["csv"] }],
      });
      if (!path) return;
      await writeFile(path, encodeCsv(resultsToCsv(toExport, getColumnOrder())));
      notifications.show({
        color: "green",
        title: "Results exported",
        message: `${toExport.length.toLocaleString()} rows written to ${path}`,
      });
    } catch (e) {
      notifications.show({ color: "red", title: "Export failed", message: e instanceof Error ? e.message : String(e) });
    }
  }

  const copySelectedPaths = () => {
    const picked = selectedRows();
    if (picked.length > 0) void navigator.clipboard.writeText(rowsToClipboardText(picked, "path"));
  };

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
      <div className="results-toolbar">
        <button
          disabled={busy || rows.length === 0}
          title={busy ? "Wait for the search to finish" : rows.length === 0 ? "No results to export" : undefined}
          onClick={() => void exportRows(rows)}
        >
          Export results…
        </button>
        <button
          disabled={busy || selectedCount === 0}
          title={busy ? "Wait for the search to finish" : selectedCount === 0 ? "Select results to export" : undefined}
          onClick={() => void exportRows(selectedRows())}
        >
          Export selected…
        </button>
      </div>
      <div className="results-fill">
        <ResultsList
          rows={rows}
          selection={rowSelection.selection}
          dispatchSelection={rowSelection.dispatch}
          columnOrder={getColumnOrder()}
          visibleRowsRef={visibleRowsRef}
          onCopy={copySelectedPaths}
          onContextMenu={(row, x, y) => {
            const inSelection = rowSelection.selection.selected.has(refToString(row.ref));
            onContextMenu(row, inSelection ? selectedRows() : [row], x, y);
          }}
          {...list}
        />
      </div>
    </div>
  );
}
