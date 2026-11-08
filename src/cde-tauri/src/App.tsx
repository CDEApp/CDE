import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { open, save } from "@tauri-apps/plugin-dialog";
import { writeFile } from "@tauri-apps/plugin-fs";
import { notifications } from "@mantine/notifications";
import { CdeApiClient } from "./api/client";
import { activeCatalogIds, activeCount, loadBlockedReason, sortedJobs } from "./jobsModel";
import { NewCatalogDialog } from "./components/NewCatalogDialog";
import { EditDescriptionDialog } from "./components/EditDescriptionDialog";
import { RootUnreachableModal } from "./components/RootUnreachableModal";
import { AboutModal } from "./components/AboutModal";
import { SearchSyntaxModal } from "./components/SearchSyntaxModal";
import { MigrateLegacyDialog } from "./components/MigrateLegacyDialog";
import { legacyNotice } from "./legacyMigration";
import {
  CatalogInfo,
  CatalogsResponse,
  DirectoryNode,
  EntryRef,
  TreemapRow,
} from "./api/types";
import { startSidecar, Sidecar } from "./sidecar";
import { UiStateManager } from "./state/uiState";
import { catalogFile, catalogName, fileBaseName } from "./format";
import { searchFromHereQuery } from "./everythingSearch";
import type { TreeRoot } from "./components/CatalogTree";
import { DEFAULT_SORT, type DirectorySort, type SortColumn, nextSort } from "./directorySort";
import { DirectoryPane } from "./components/DirectoryPane";
import { ResultsList } from "./components/ResultsList";
import { CatalogList } from "./components/CatalogList";
import { type CatalogSort, nextCatalogSort } from "./catalogSort";
import { SearchBar } from "./components/SearchBar";
import { StatusBar } from "./components/StatusBar";
import { MenuBar } from "./components/MenuBar";
import { ContextMenu, MenuState } from "./components/ContextMenu";
import { TreemapView } from "./components/TreemapView";
import { ColoursView } from "./components/ColoursView";
import { DiffView } from "./components/DiffView";
import type { CatalogColumnWidths } from "./catalogColumns";
import type { ColumnWidths } from "./columnWidths";
import { viewPaneClass } from "./viewPane";
import { parseRef, useCatalogSelection } from "./hooks/useCatalogSelection";
import { useJobs } from "./hooks/useJobs";
import { useColourRules } from "./hooks/useColourRules";
import { useShellActions } from "./hooks/useShellActions";
import { useLegacyMigration } from "./hooks/useLegacyMigration";
import { useNewCatalog } from "./hooks/useNewCatalog";
import { JobsView } from "./components/JobsView";
import { useSearch } from "./hooks/useSearch";
import { encodeCsv, exportFileName, resultsToCsv } from "./csvExport";
import {
  dropTreemap,
  initialView,
  selectTreemap,
  setTreemapDepth,
  showTreemap as showTreemapView,
  showView,
  type ViewKind,
} from "./viewState";
import {
  catalogMenu,
  collapseAllItem,
  entryMenu,
  listMenu,
  rescanItem as rescanMenuEntry,
  treeBackgroundMenu as treeBackgroundItems,
  treeNodeMenu,
  type EntryMenuContext,
} from "./menus";

const DEFAULT_TREEMAP_DEPTH = 3;

export default function App() {
  const clientRef = useRef<CdeApiClient | null>(null);
  const sidecarRef = useRef<Sidecar | null>(null);
  const uiRef = useRef<UiStateManager | null>(null);

  const [status, setStatus] = useState("Starting…");
  const [ready, setReady] = useState(false);
  const [view, setView] = useState(initialView);
  const [catalogs, setCatalogs] = useState<CatalogsResponse | null>(null);
  const [roots, setRoots] = useState<TreeRoot[]>([]);

  // Stable accessors so the hooks' callbacks keep a stable identity.
  const getClient = useCallback(() => {
    const client = clientRef.current;
    if (!client) throw new Error("The catalog service is not ready yet.");
    return client;
  }, []);
  const getUi = useCallback(() => uiRef.current, []);

  const colours = useColourRules({ getClient });
  const { resolveStyle, resolveTreeStyle } = colours;
  const shell = useShellActions({ getClient });
  const { runShell, exploreEntry } = shell;
  const legacy = useLegacyMigration({ getUi });
  const { promptFor: promptForLegacy } = legacy;
  const newCatalog = useNewCatalog({ getUi });

  // Directory-listing sort: session-only (deliberately not in uiState) and kept across folders.
  const [dirSort, setDirSort] = useState<DirectorySort>(DEFAULT_SORT);
  // Catalogs-tab sort: session-only too, and held here because the tab unmounts on every switch.
  const [catalogSort, setCatalogSort] = useState<CatalogSort>(null);
  // Catalogs-tab selected row (by catalog id), held here for the same reason.
  const [selectedCatalogId, setSelectedCatalogId] = useState<number | null>(null);

  const [menu, setMenu] = useState<MenuState | null>(null);
  const [catalogDir, setCatalogDir] = useState<string | null>(null);

  const [aboutOpen, setAboutOpen] = useState(false);
  const [syntaxOpen, setSyntaxOpen] = useState(false);
  // Per-catalog counter that remounts a tree root after a rescan (drops its cached children).
  const treeGenRef = useRef<Record<number, number>>({});
  // Bumped on every Reload / Open Folder: the Directory view stays mounted across tab switches (#80),
  // so a reload must drop the tree's cached expansion + children itself.
  const reloadEpochRef = useRef(0);
  // Remounts the Diff view (which stays mounted across tabs) when a reload or rescan invalidates its refs.
  const [diffEpoch, setDiffEpoch] = useState(0);

  // --- bootstrap: spawn sidecar, build client, load catalogs + ui-state ---
  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const sidecar = await startSidecar();
        if (disposed) {
          await sidecar.kill();
          return;
        }
        sidecarRef.current = sidecar;
        const client = new CdeApiClient(sidecar.connection.baseUrl, sidecar.connection.token);
        clientRef.current = client;
        uiRef.current = new UiStateManager(client);

        const ui = await uiRef.current.load();
        await shell.loadCommands();
        await colours.load();
        // Load the last-used catalog folder, or the sidecar's default folder. The sidecar loads nothing on
        // its own, so the handshake never waits on catalogs (a folder of legacy .cde files is slow).
        const savedDir = typeof ui.catalogDir === "string" ? ui.catalogDir : undefined;
        if (savedDir) setCatalogDir(savedDir);
        setStatus("Loading catalogs…");
        await client.reload({ onProgress: (p) => setStatus(p.message) }, savedDir);
        promptForLegacy(await refreshCatalogs(client));
        setReady(true);
        setStatus("");
      } catch (e) {
        console.error("bootstrap failed", e);
        const msg = e instanceof Error ? e.message : typeof e === "string" ? e : JSON.stringify(e);
        setStatus(`Failed to start: ${msg}`);
      }
    })();
    return () => {
      disposed = true;
    };
  }, []);

  // --- persist UI state + kill sidecar on window close ---
  useEffect(() => {
    const win = getCurrentWindow();
    const unlistenPromise = win.onCloseRequested(async (event) => {
      event.preventDefault();
      // Best-effort cleanup; neither step may block the actual close.
      try {
        await uiRef.current?.flush();
      } catch {
        /* ignore */
      }
      try {
        await sidecarRef.current?.kill();
      } catch {
        /* ignore */
      }
      await win.destroy();
    });
    return () => {
      void unlistenPromise.then((f) => f());
    };
  }, []);

  const refreshCatalogs = useCallback(async (client: CdeApiClient): Promise<CatalogsResponse> => {
    const cat = await client.catalogs();
    setCatalogs(cat);
    setRoots(
      cat.catalogs.map<TreeRoot>((c) => ({
        ref: { catalogId: c.catalogId, entryIndex: 0 },
        label: c.rootPath,
        fullPath: c.rootPath,
        size: c.rootSize,
        catalogName: catalogName(catalogFile(c)),
        generation: (treeGenRef.current[c.catalogId] ?? 0) + reloadEpochRef.current,
      })),
    );
    return cat;
  }, []);

  const showDirectory = useCallback(() => setView((v) => showView(v, "directory")), []);
  const showResults = useCallback(() => setView((v) => showView(v, "results")), []);

  const selection = useCatalogSelection({ getClient, showDirectory });
  const { results, busy, timing, runSearch, cancelSearch, searchAgain, markRescanned, staleBanner: staleBannerFor } = useSearch({
    getClient,
    getUi,
    onStart: showResults,
  });
  const { selectNode, viewInTree, openRoot, resetForReload, selectedKeyRef, selectFromTree, activateDir, canGoUp, goUp } = selection;

  // After the sidecar swapped the catalog in: refresh the lists, remount that catalog's tree root,
  // mark results into it stale, and put the selection back (nearest surviving parent if it is gone).
  const { snapshot, restoreAfterRescan } = selection;
  const afterRescan = useCallback(
    async (client: CdeApiClient, catalogId: number) => {
      const before = snapshot();
      // Bookkeeping first: it must happen even if the refresh below fails.
      treeGenRef.current[catalogId] = (treeGenRef.current[catalogId] ?? 0) + 1;
      markRescanned(catalogId);
      setDiffEpoch((n) => n + 1);
      // Indexes belong to the old file: a TreeMap request for this catalog is no longer valid.
      setView((v) => (v.treemap?.ref.catalogId === catalogId ? dropTreemap(v) : v));
      await refreshCatalogs(client);
      await restoreAfterRescan(client, catalogId, before);
    },
    [refreshCatalogs, snapshot, restoreAfterRescan, markRescanned],
  );
  const openNewRoot = useCallback(
    (catalogId: number, rootPath: string) => openRoot({ catalogId, entryIndex: 0 }, rootPath),
    [openRoot],
  );
  const { model: jobs, busy: jobsBusy, unreachableRoot, clearUnreachable, rescanCatalog, createCatalog, cancelJob, dismissJob, clearFinished } =
    useJobs({
      getClient,
      sidecarReady: ready,
      afterRescan,
      refreshCatalogs,
      openCatalog: openNewRoot,
    });
  const jobsBusyRef = useRef(jobsBusy);
  jobsBusyRef.current = jobsBusy;

  const reloadCatalogs = useCallback(
    async (dir?: string | null) => {
      if (jobsBusyRef.current) return;
      const client = clientRef.current!;
      setStatus("Loading catalogs…");
      // Reset BEFORE the call, so it also happens when the reload fails part-way (the sidecar may
      // already have dropped the old catalogs): refs and expansion from the old load are not
      // trusted, and in-flight navigation responses must not land after this.
      resetForReload();
      reloadEpochRef.current += 1;
      setSelectedCatalogId(null); // ids are list positions: the old one may name a different catalog now
      setDiffEpoch((n) => n + 1);
      setView(dropTreemap);
      try {
        await client.reload({}, dir ?? undefined);
        promptForLegacy(await refreshCatalogs(client));
        setStatus("");
      } catch (e) {
        setStatus(`Reload failed: ${(e as Error).message}`);
        // Best effort: re-read the catalog list so the tree picks up the new epoch and rebuilds.
        try {
          await refreshCatalogs(client);
        } catch {
          /* keep the failure message */
        }
      }
    },
    [refreshCatalogs, promptForLegacy, resetForReload],
  );

  // Re-read the rules file each time the Customize tab opens. Fetch first, then switch, so the editor
  // never shows stale text that a keystroke could then overwrite.
  const { load: loadColours } = colours;
  const openColours = useCallback(async () => {
    await loadColours();
    setView((v) => showView(v, "colours"));
  }, [loadColours]);

  const onDirColumnWidthsChange = useCallback((directoryColumnWidths: ColumnWidths) => {
    uiRef.current?.update({ directoryColumnWidths });
  }, []);

  const onCatalogColumnWidthsChange = useCallback((catalogColumnWidths: CatalogColumnWidths) => {
    uiRef.current?.update({ catalogColumnWidths });
  }, []);

  const onTreePaneResize = useCallback((width: number) => {
    uiRef.current?.update({ treePaneWidth: Math.round(width) });
  }, []);

  // File ▸ Open Folder… — native folder picker, then load .cdex catalogs from it.
  const openFolder = useCallback(async () => {
    if (jobsBusyRef.current) return;
    const picked = await open({ directory: true, multiple: false, title: "Select catalog folder" });
    if (typeof picked !== "string") return; // cancelled
    setCatalogDir(picked);
    uiRef.current?.update({ catalogDir: picked });
    await reloadCatalogs(picked);
    setView((v) => showView(v, "catalogs"));
  }, [reloadCatalogs]);

  // Export results: every streamed row (all held in `results`) as UTF-8 CSV, columns in the list's current order.
  const exportResults = useCallback(async () => {
    try {
      const path = await save({
        title: "Export search results",
        defaultPath: exportFileName(new Date()),
        filters: [{ name: "CSV", extensions: ["csv"] }],
      });
      if (!path) return; // cancelled: do nothing
      await writeFile(path, encodeCsv(resultsToCsv(results, uiRef.current?.get().searchResultColumnOrder)));
      notifications.show({
        color: "green",
        title: "Results exported",
        message: `${results.length.toLocaleString()} rows written to ${path}`,
      });
    } catch (e) {
      notifications.show({
        color: "red",
        title: "Export failed",
        message: e instanceof Error ? e.message : String(e),
      });
    }
  }, [results]);

  // --- context menu ---
  const showTreemap = useCallback((ref: EntryRef) => setView((v) => showTreemapView(v, ref)), []);

  const rescanItem = useCallback(
    (catalogId: number) =>
      rescanMenuEntry(
        jobs,
        catalogId,
        catalogs?.catalogs.find((c) => c.catalogId === catalogId)?.rootPath,
        (id) => void rescanCatalog(id),
      ),
    [jobs, catalogs, rescanCatalog],
  );

  const [editingDescription, setEditingDescription] = useState<CatalogInfo | null>(null);
  const saveDescription = useCallback(
    async (catalogId: number, description: string) => {
      const client = clientRef.current!;
      await client.setDescription(catalogId, description);
      await refreshCatalogs(client);
    },
    [refreshCatalogs],
  );

  const editCatalogDescription = useCallback(
    (catalogId: number) => {
      const c = catalogs?.catalogs.find((x) => x.catalogId === catalogId);
      if (c) setEditingDescription(c);
    },
    [catalogs],
  );

  const [searchPrefill, setSearchPrefill] = useState<{ pattern: string; nonce: number } | null>(null);
  const searchFromHere = useCallback(
    (ref: EntryRef, fullPath: string) => {
      const c = catalogs?.catalogs.find((x) => x.catalogId === ref.catalogId);
      setSearchPrefill((p) => ({
        pattern: searchFromHereQuery(fullPath, c ? catalogFile(c) : ""),
        nonce: (p?.nonce ?? 0) + 1,
      }));
    },
    [catalogs],
  );

  const menuContext = useMemo<EntryMenuContext>(
    () => ({
      commands: shell.commands,
      runShell: (ref, action, commandId) => void runShell(ref, action, commandId),
      showTreemap,
      searchFromHere,
      copyPath: (fullPath) => void navigator.clipboard.writeText(fullPath),
      rescanFor: rescanItem,
      editDescription: editCatalogDescription,
    }),
    [shell.commands, runShell, showTreemap, searchFromHere, rescanItem, editCatalogDescription],
  );

  // Handlers for the memoised DirectoryPane must keep a stable identity, or every App render (e.g. each
  // streamed search flush) would re-render the hidden tree + listing. They read the latest
  // menu context / selection through refs at event time instead of closing over them.
  const menuContextRef = useRef(menuContext);
  menuContextRef.current = menuContext;

  const [collapseNonce, setCollapseNonce] = useState(0);
  const collapseAll = useCallback(() => setCollapseNonce((n) => n + 1), []);

  const treeBackgroundMenu = useCallback(
    (x: number, y: number) => {
      const key = selectedKeyRef.current;
      setMenu({ x, y, items: treeBackgroundItems(key ? parseRef(key) : null, showTreemap, collapseAll) });
    },
    [showTreemap, collapseAll, selectedKeyRef],
  );
  const onTreeContextMenu = useCallback(
    (ref: EntryRef, fp: string, x: number, y: number) =>
      setMenu({ x, y, items: treeNodeMenu(menuContextRef.current, ref, fp, collapseAll) }),
    [collapseAll],
  );
  const onListContextMenu = useCallback(
    (n: DirectoryNode, x: number, y: number) => setMenu({ x, y, items: listMenu(menuContextRef.current, n) }),
    [],
  );
  const onDirSort = useCallback((column: SortColumn) => setDirSort((cur) => nextSort(cur, column)), []);
  // Initial split width only (SplitPane owns it afterwards). A plain number, so it is a stable prop.
  const treePaneWidth = (uiRef.current?.get().treePaneWidth as number) ?? 320;

  const onTreemapTileMenu = useCallback(
    (row: TreemapRow, fullPath: string, x: number, y: number) => {
      const ref = row.ref!;
      setMenu({
        x,
        y,
        items: entryMenu(menuContext, { ref, fullPath, isDirectory: row.isDirectory, onViewInTree: () => void viewInTree(ref) }),
      });
    },
    [menuContext, viewInTree],
  );

  if (!ready) {
    return <div className="boot">{status || "Loading…"}</div>;
  }

  const { selectedKey, revealKey, revealNonce, expandTo, dirPath, dirNodes } = selection;
  const loadBlockedReasonText = loadBlockedReason(jobsBusy);
  const staleBanner = staleBannerFor((id) => {
    const c = catalogs?.catalogs.find((x) => x.catalogId === id);
    return c ? c.rootPath : `#${id}`;
  });

  const renderTransientPane = () => {
    switch (view.kind) {
      case "directory":
      case "diff":
        return null;
      case "results":
        return (
          <div className="results-pane">
            {staleBanner && (
              <div className="stale-banner" role="status">
                <span>{staleBanner}</span>
                <button
                  disabled={busy}
                  onClick={() => void searchAgain()}
                >
                  Search again
                </button>
              </div>
            )}
            <div className="results-toolbar">
              <button
                disabled={busy || results.length === 0}
                title={busy ? "Wait for the search to finish" : results.length === 0 ? "No results to export" : undefined}
                onClick={() => void exportResults()}
              >
                Export results…
              </button>
            </div>
            <div className="results-fill">
              <ResultsList
                rows={results}
                resolveStyle={resolveStyle}
                columnOrder={uiRef.current?.get().searchResultColumnOrder}
                onColumnOrderChange={(searchResultColumnOrder) => uiRef.current?.update({ searchResultColumnOrder })}
                onActivate={(row) => void viewInTree(row.ref, row.fullPath)}
                onContextMenu={(row, x, y) => {
                  // Results outlive a reload and catalog ids are reused, so confirm the row's catalog is still at that id.
                  const current = catalogs?.catalogs.find((c) => c.catalogId === row.ref.catalogId);
                  const sameCatalog = !!current && fileBaseName(catalogFile(current)) === row.catalogName;
                  setMenu({
                    x,
                    y,
                    items: entryMenu(menuContext, {
                      ref: row.ref,
                      fullPath: row.fullPath,
                      isDirectory: row.isDirectory,
                      onViewInTree: () => void viewInTree(row.ref, row.fullPath),
                      canEditDescription: sameCatalog,
                    }),
                  });
                }}
              />
            </div>
          </div>
        );
      case "catalogs":
        return (
          <CatalogList
            catalogs={catalogs?.catalogs ?? []}
            rescanningIds={activeCatalogIds(jobs)}
            sort={catalogSort}
            selectedId={selectedCatalogId}
            onSelect={setSelectedCatalogId}
            onSort={(column) => setCatalogSort((s) => nextCatalogSort(s, column))}
            columnWidths={uiRef.current?.get().catalogColumnWidths}
            onColumnWidthsChange={onCatalogColumnWidthsChange}
            onNewCatalog={() => void newCatalog.pick()}
            onContextMenu={(c, x, y) =>
              setMenu({ x, y, items: catalogMenu(c.catalogId, showTreemap, () => setEditingDescription(c), rescanItem(c.catalogId)) })
            }
            onActivate={(c: CatalogInfo) => {
              showDirectory();
              void selectNode({ catalogId: c.catalogId, entryIndex: 0 }, c.rootPath);
            }}
          />
        );
      case "jobs":
        return (
          <JobsView sortedJobs={sortedJobs(jobs)} onCancel={cancelJob} onDismiss={dismissJob} onClearFinished={clearFinished} />
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
            client={clientRef.current!}
            request={view.treemap}
            depth={view.treemapDepth ?? (uiRef.current?.get().treemapDepth as number | undefined) ?? DEFAULT_TREEMAP_DEPTH}
            onDepthChange={(d) => {
              setView((v) => setTreemapDepth(v, d));
              uiRef.current?.update({ treemapDepth: d });
            }}
            onStatus={setStatus}
            onTileMenu={onTreemapTileMenu}
          />
        );
      default: {
        const unreachable: never = view;
        return unreachable;
      }
    }
  };

  return (
    <div className="app">
      <MenuBar
        onNewCatalog={() => void newCatalog.pick()}
        onOpenFolder={openFolder}
        onReload={() => void reloadCatalogs(catalogDir)}
        onExit={() => void getCurrentWindow().close()}
        onAbout={() => setAboutOpen(true)}
        onSearchSyntax={() => setSyntaxOpen(true)}
        loadDisabledReason={loadBlockedReasonText}
      />

      <SearchBar
        busy={busy}
        history={(uiRef.current?.get().searchHistory as string[]) ?? []}
        defaults={uiRef.current?.get() ?? {}}
        filtersExpanded={uiRef.current?.get().filtersExpanded ?? true}
        onFiltersExpandedChange={(filtersExpanded) => uiRef.current?.update({ filtersExpanded })}
        onSearch={runSearch}
        onCancel={cancelSearch}
        prefill={searchPrefill}
      />

      <div className="tabs">
        <button
          className={view.kind === "directory" ? "active" : ""}
          onClick={showDirectory}
          onContextMenu={(e) => {
            e.preventDefault();
            setMenu({ x: e.clientX, y: e.clientY, items: [collapseAllItem(collapseAll)] });
          }}
        >
          Directory
        </button>
        <button className={view.kind === "results" ? "active" : ""} onClick={showResults}>
          Search Results
        </button>
        <button className={view.kind === "catalogs" ? "active" : ""} onClick={() => setView((v) => showView(v, "catalogs"))}>
          Catalogs
        </button>
        <button className={view.kind === "diff" ? "active" : ""} onClick={() => setView((v) => showView(v, "diff"))}>
          Compare
        </button>
        <button className={view.kind === "jobs" ? "active" : ""} onClick={() => setView((v) => showView(v, "jobs"))}>
          {activeCount(jobs) > 0 ? `Jobs (${activeCount(jobs)})` : "Jobs"}
        </button>
        <button className={view.kind === "colours" ? "active" : ""} onClick={() => void openColours()}>
          Customize
        </button>
        {view.treemap && (
          <button
            className={view.kind === "treemap" ? "active" : ""}
            onClick={() => setView(selectTreemap)}
          >
            TreeMap
          </button>
        )}
        <button
          className="reload"
          disabled={!!loadBlockedReasonText}
          title={loadBlockedReasonText ?? undefined}
          onClick={() => void reloadCatalogs(catalogDir)}
        >
          Reload
        </button>
      </div>

      <div className="main">
        {/* Stays mounted when another tab is active (#80): keeps expansion, loaded children, scroll
            offsets, selection, listing and split width. Hidden via visibility (see viewPane.ts). */}
        <div className={viewPaneClass<ViewKind>("directory", view.kind)} aria-hidden={view.kind !== "directory"}>
          <DirectoryPane
            client={clientRef.current!}
            roots={roots}
            selectedKey={selectedKey}
            revealKey={revealKey}
            revealNonce={revealNonce}
            expandTo={expandTo}
            collapseNonce={collapseNonce}
            dirPath={dirPath}
            dirNodes={dirNodes}
            sort={dirSort}
            columnWidths={uiRef.current?.get().directoryColumnWidths}
            onColumnWidthsChange={onDirColumnWidthsChange}
            initialLeftWidth={treePaneWidth}
            resolveTreeStyle={resolveTreeStyle}
            resolveStyle={resolveStyle}
            onResizeEnd={onTreePaneResize}
            onSelect={selectFromTree}
            onTreeContextMenu={onTreeContextMenu}
            onBackgroundContextMenu={treeBackgroundMenu}
            onSort={onDirSort}
            onActivate={activateDir}
            onGoUp={canGoUp ? goUp : undefined}
            onListContextMenu={onListContextMenu}
          />
        </div>

        <div className={viewPaneClass<ViewKind>("diff", view.kind)} aria-hidden={view.kind !== "diff"}>
          <DiffView
            key={diffEpoch}
            client={clientRef.current!}
            catalogs={catalogs?.catalogs ?? []}
            resolveStyle={resolveStyle}
            onExplore={exploreEntry}
          />
        </div>

        {renderTransientPane()}
      </div>

      <StatusBar
        catalogs={catalogs}
        resultCount={results.length}
        searchTiming={status || timing}
        activeJobCount={activeCount(jobs)}
        onShowJobs={() => setView((v) => showView(v, "jobs"))}
        legacyNotice={legacyNotice(catalogs?.legacyCatalogs ?? [])}
        onMigrate={legacy.show}
      />
      <ContextMenu menu={menu} onClose={() => setMenu(null)} />
      <RootUnreachableModal rootPath={unreachableRoot} onClose={clearUnreachable} />
      <AboutModal opened={aboutOpen} onClose={() => setAboutOpen(false)} />
      <SearchSyntaxModal opened={syntaxOpen} onClose={() => setSyntaxOpen(false)} />
      <MigrateLegacyDialog
        opened={legacy.open}
        legacy={catalogs?.legacyCatalogs ?? []}
        folder={catalogs?.catalogDir ?? ""}
        blockedReason={
          jobsBusy
            ? "Unavailable while catalog jobs are queued, running or finishing; try again when they finish."
            : null
        }
        client={clientRef.current}
        onNotNow={() => legacy.dismiss(catalogs?.catalogDir)}
        onFinished={() => {
          // The report has been seen: don't open again by itself here (failures stay in the status-bar notice).
          legacy.dismiss(catalogs?.catalogDir);
          void reloadCatalogs(catalogDir);
        }}
      />
      <EditDescriptionDialog
        catalog={editingDescription}
        onSave={saveDescription}
        onClose={() => setEditingDescription(null)}
      />
      <NewCatalogDialog
        rootPath={newCatalog.root}
        client={clientRef.current}
        catalogs={catalogs?.catalogs ?? []}
        onChangeRoot={() => void newCatalog.pick()}
        onCreate={(form) => {
          newCatalog.close();
          void createCatalog(form);
        }}
        onRescanExisting={(catalogId) => {
          newCatalog.close();
          void rescanCatalog(catalogId);
        }}
        onClose={newCatalog.close}
      />
    </div>
  );
}
