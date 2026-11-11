import { useMemo, useRef } from "react";
import { save } from "@tauri-apps/plugin-dialog";
import { writeFile } from "@tauri-apps/plugin-fs";
import { notifications } from "@mantine/notifications";
import { refToString, type SearchResultRow } from "../api/types";
import type { StyleResolver } from "../colourRules";
import { rowsToClipboardText } from "../copyText";
import { encodeCsv, exportFileName, resultsToCsv } from "../csvExport";
import type { ResultsFilterApi } from "../hooks/useResultsFilter";
import type { RowSelectionApi } from "../hooks/useRowSelection";
import { selectedInOrder } from "../rowSelection";
import { ResultsFilterBar } from "./ResultsFilterBar";
import { ResultsList } from "./ResultsList";

interface Props {
  rows: SearchResultRow[];
  rowSelection: RowSelectionApi;
  filter: ResultsFilterApi;
  canSearchWithFilter: boolean;
  onSearchWithFilter: () => void;
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
  /** The persisted ids of the shown columns, read when the list mounts and at each export. */
  getVisibleColumns: () => string[] | undefined;
  onVisibleColumnsChange: (visible: string[]) => void;
  onActivate: (row: SearchResultRow) => void;
  /** `selection` is the rows the menu acts on, in on-screen order: the whole selection if `row` is in it, else just `row`. */
  onContextMenu: (row: SearchResultRow, selection: readonly SearchResultRow[], x: number, y: number) => void;
}

/** The Search Results tab: the stale-results banner, Export results…, and the results list. */
export function ResultsPane({
  rows, busy, staleBanner, onSearchAgain, limitReachedCount = null, onRaiseLimit, getColumnOrder, getVisibleColumns, rowSelection, filter, canSearchWithFilter, onSearchWithFilter, onContextMenu, ...list
}: Props) {
  const visibleRowsRef = useRef<readonly SearchResultRow[]>(filter.shown);
  const selected = rowSelection.selection.selected;
  const shownSelectedCount = useMemo(
    () => (selected.size === 0 ? 0 : filter.shown.reduce((n, r) => (selected.has(refToString(r.ref)) ? n + 1 : n), 0)),
    [filter.shown, selected],
  );
  const selectedRows = () => selectedInOrder(visibleRowsRef.current, (r) => refToString(r.ref), rowSelection.selection);

  // The rows as UTF-8 CSV, with the list's shown columns in its current order: a column drag or toggle saves
  // without re-rendering this pane, so both are read at click time.
  async function exportRows(toExport: readonly SearchResultRow[]) {
    try {
      const path = await save({
        title: "Export search results",
        defaultPath: exportFileName(new Date()),
        filters: [{ name: "CSV", extensions: ["csv"] }],
      });
      if (!path) return;
      await writeFile(path, encodeCsv(resultsToCsv(toExport, getColumnOrder(), getVisibleColumns())));
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
      <ResultsFilterBar filter={filter} busy={busy} canSearchAgain={canSearchWithFilter} onSearchAgain={onSearchWithFilter} />
      <div className="results-toolbar">
        <button
          disabled={busy || rows.length === 0}
          title={
            busy
              ? "Wait for the search to finish"
              : rows.length === 0
                ? "No results to export"
                : filter.active
                  ? `Exports all ${rows.length.toLocaleString()} loaded results, including the rows the filter hides`
                  : undefined
          }
          onClick={() => void exportRows(rows)}
        >
          Export results…
        </button>
        <button
          disabled={busy || shownSelectedCount === 0}
          title={
            busy
              ? "Wait for the search to finish"
              : shownSelectedCount > 0
                ? undefined
                : selected.size > 0
                  ? "The filter hides every selected result"
                  : "Select results to export"
          }
          onClick={() => void exportRows(selectedRows())}
        >
          Export selected…
        </button>
      </div>
      <div className="results-fill">
        <ResultsList
          rows={filter.shown}
          selection={rowSelection.selection}
          dispatchSelection={rowSelection.dispatch}
          columnOrder={getColumnOrder()}
          visibleColumns={getVisibleColumns()}
          visibleRowsRef={visibleRowsRef}
          onCopy={copySelectedPaths}
          onContextMenu={(row, x, y) => {
            const inSelection = rowSelection.selection.selected.has(refToString(row.ref));
            onContextMenu(row, inSelection ? selectedRows() : [row], x, y);
          }}
          partialSortNotice={limitReachedCount === null ? undefined : `sorted within the first ${limitReachedCount.toLocaleString()} matches`}
          {...list}
        />
      </div>
    </div>
  );
}
