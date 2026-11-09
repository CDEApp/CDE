import { useCallback, useState, type Dispatch, type SetStateAction } from "react";
import type { CdeApiClient } from "../api/client";
import type { CatalogInfo, EntryRef } from "../api/types";
import type { CatalogColumnWidths } from "../catalogColumns";
import { type CatalogSort, nextCatalogSort } from "../catalogSort";
import type { ColumnWidths } from "../columnWidths";
import { DEFAULT_SORT, type DirectorySort, type SortColumn, nextSort } from "../directorySort";
import type { useCatalogSelection } from "../hooks/useCatalogSelection";
import type { useColourRules } from "../hooks/useColourRules";
import type { useContextMenus } from "../hooks/useContextMenus";
import type { useJobs } from "../hooks/useJobs";
import type { useSearch } from "../hooks/useSearch";
import { activeCatalogIds, sortedJobs } from "../jobsModel";
import type { UiStateManager } from "../state/uiState";
import { viewPaneClass } from "../viewPane";
import { setTreemapDepth, showView, type ViewKind, type ViewState } from "../viewState";
import { CatalogList } from "./CatalogList";
import type { TreeRoot } from "./CatalogTree";
import { ColoursView } from "./ColoursView";
import { DiffView } from "./DiffView";
import { DirectoryPane } from "./DirectoryPane";
import { JobsView } from "./JobsView";
import { ResultsPane } from "./ResultsPane";
import { TreemapView } from "./TreemapView";

const DEFAULT_TREEMAP_DEPTH = 3;

interface Props {
  view: ViewState;
  setView: Dispatch<SetStateAction<ViewState>>;
  client: CdeApiClient;
  ui: UiStateManager | null;
  catalogs: CatalogInfo[];
  roots: TreeRoot[];
  selection: ReturnType<typeof useCatalogSelection>;
  search: ReturnType<typeof useSearch>;
  colours: ReturnType<typeof useColourRules>;
  jobs: ReturnType<typeof useJobs>;
  menus: ReturnType<typeof useContextMenus>;
  /** Bumped when a reload or rescan invalidates the Compare view's refs: it remounts. */
  diffEpoch: number;
  collapseNonce: number;
  selectedCatalogId: number | null;
  onSelectCatalog: (catalogId: number | null) => void;
  onNewCatalog: () => void;
  onExplore: (ref: EntryRef) => void;
  onStatus: (text: string) => void;
}

/** The tab area: the Directory and Compare views stay mounted across tabs; the active other view renders on demand. */
export function MainLayout(p: Props) {
  const { view, ui, selection, search, colours, jobs, menus } = p;
  // Session-only sorts (deliberately not in uiState), kept across folders and tab switches.
  const [dirSort, setDirSort] = useState<DirectorySort>(DEFAULT_SORT);
  const [catalogSort, setCatalogSort] = useState<CatalogSort>(null);

  // Handlers for the memoised DirectoryPane keep a stable identity.
  const onDirSort = useCallback((column: SortColumn) => setDirSort((cur) => nextSort(cur, column)), []);
  const onDirColumnWidthsChange = useCallback((w: ColumnWidths) => ui?.update({ directoryColumnWidths: w }), [ui]);
  const onCatalogColumnWidthsChange = useCallback((w: CatalogColumnWidths) => ui?.update({ catalogColumnWidths: w }), [ui]);
  const onTreePaneResize = useCallback((width: number) => ui?.update({ treePaneWidth: Math.round(width) }), [ui]);

  const transientPane = () => {
    switch (view.kind) {
      case "directory":
      case "diff":
        return null;
      case "results":
        return (
          <ResultsPane
            rows={search.results}
            busy={search.busy}
            staleBanner={search.staleBanner((id) => p.catalogs.find((c) => c.catalogId === id)?.rootPath ?? `#${id}`)}
            onSearchAgain={() => void search.searchAgain()}
            resolveStyle={colours.resolveStyle}
            getColumnOrder={() => ui?.get().searchResultColumnOrder}
            onColumnOrderChange={(searchResultColumnOrder) => ui?.update({ searchResultColumnOrder })}
            onActivate={(row) => void selection.viewInTree(row.ref, row.fullPath)}
            onContextMenu={menus.onResultContextMenu}
          />
        );
      case "catalogs":
        return (
          <CatalogList
            catalogs={p.catalogs}
            rescanningIds={activeCatalogIds(jobs.model)}
            sort={catalogSort}
            selectedId={p.selectedCatalogId}
            onSelect={p.onSelectCatalog}
            onSort={(column) => setCatalogSort((s) => nextCatalogSort(s, column))}
            columnWidths={ui?.get().catalogColumnWidths}
            onColumnWidthsChange={onCatalogColumnWidthsChange}
            onNewCatalog={p.onNewCatalog}
            onContextMenu={menus.onCatalogContextMenu}
            onActivate={(c) => {
              p.setView((v) => showView(v, "directory"));
              void selection.selectNode({ catalogId: c.catalogId, entryIndex: 0 }, c.rootPath);
            }}
          />
        );
      case "jobs":
        return (
          <JobsView sortedJobs={sortedJobs(jobs.model)} onCancel={jobs.cancelJob} onDismiss={jobs.dismissJob} onClearFinished={jobs.clearFinished} />
        );
      case "colours":
        return (
          <ColoursView
            text={colours.text}
            onChange={colours.setText}
            save={colours.save}
            saveError={colours.saveError}
            getDefaults={colours.getDefaults}
          />
        );
      case "treemap":
        return (
          <TreemapView
            client={p.client}
            request={view.treemap}
            depth={view.treemapDepth ?? (ui?.get().treemapDepth as number | undefined) ?? DEFAULT_TREEMAP_DEPTH}
            onDepthChange={(d) => {
              p.setView((v) => setTreemapDepth(v, d));
              ui?.update({ treemapDepth: d });
            }}
            onStatus={p.onStatus}
            onTileMenu={menus.onTreemapTileMenu}
          />
        );
      default: {
        const unreachable: never = view;
        return unreachable;
      }
    }
  };

  return (
    <div className="main">
      {/* Stays mounted when another tab is active (#80): keeps expansion, loaded children, scroll
          offsets, selection, listing and split width. Hidden via visibility (see viewPane.ts). */}
      <div className={viewPaneClass<ViewKind>("directory", view.kind)} aria-hidden={view.kind !== "directory"}>
        <DirectoryPane
          client={p.client}
          roots={p.roots}
          selectedKey={selection.selectedKey}
          revealKey={selection.revealKey}
          revealNonce={selection.revealNonce}
          expandTo={selection.expandTo}
          collapseNonce={p.collapseNonce}
          dirPath={selection.dirPath}
          dirNodes={selection.dirNodes}
          sort={dirSort}
          columnWidths={ui?.get().directoryColumnWidths}
          onColumnWidthsChange={onDirColumnWidthsChange}
          initialLeftWidth={(ui?.get().treePaneWidth as number) ?? 320}
          resolveTreeStyle={colours.resolveTreeStyle}
          resolveStyle={colours.resolveStyle}
          onResizeEnd={onTreePaneResize}
          onSelect={selection.selectFromTree}
          onTreeContextMenu={menus.onTreeContextMenu}
          onBackgroundContextMenu={menus.onTreeBackgroundMenu}
          onSort={onDirSort}
          onActivate={selection.activateDir}
          onGoUp={selection.canGoUp ? selection.goUp : undefined}
          onListContextMenu={menus.onListContextMenu}
        />
      </div>

      <div className={viewPaneClass<ViewKind>("diff", view.kind)} aria-hidden={view.kind !== "diff"}>
        <DiffView key={p.diffEpoch} client={p.client} catalogs={p.catalogs} resolveStyle={colours.resolveStyle} onExplore={p.onExplore} />
      </div>

      {transientPane()}
    </div>
  );
}
