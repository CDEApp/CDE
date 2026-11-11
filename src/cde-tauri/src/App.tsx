import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import type { CdeApiClient } from "./api/client";
import type { CatalogInfo, CatalogsResponse, EntryRef } from "./api/types";
import { activeCount, loadBlockedReason } from "./jobsModel";
import { legacyNotice } from "./legacyMigration";
import { catalogFile } from "./format";
import { staleCatalogIds, startupRescanIds } from "./freshness";
import { useNow } from "./hooks/useNow";
import { searchFromHereQuery } from "./everythingSearch";
import { dropTreemap, initialView, selectTreemap, showTreemap as showTreemapView, showView, type ViewKind } from "./viewState";
import { AboutModal } from "./components/AboutModal";
import { ContextMenu } from "./components/ContextMenu";
import { EditDescriptionDialog } from "./components/EditDescriptionDialog";
import { MainLayout } from "./components/MainLayout";
import { MenuBar } from "./components/MenuBar";
import { MigrateLegacyDialog } from "./components/MigrateLegacyDialog";
import { NewCatalogDialog } from "./components/NewCatalogDialog";
import { RootUnreachableModal } from "./components/RootUnreachableModal";
import { SearchBar } from "./components/SearchBar";
import { SearchSyntaxModal } from "./components/SearchSyntaxModal";
import { StatusBar } from "./components/StatusBar";
import { TabBar } from "./components/TabBar";
import { useCatalogs } from "./hooks/useCatalogs";
import { useCatalogSelection } from "./hooks/useCatalogSelection";
import { useColourRules } from "./hooks/useColourRules";
import { useContextMenus } from "./hooks/useContextMenus";
import { useJobs } from "./hooks/useJobs";
import { useLegacyMigration } from "./hooks/useLegacyMigration";
import { useNewCatalog } from "./hooks/useNewCatalog";
import { useSearch } from "./hooks/useSearch";
import { useShellActions } from "./hooks/useShellActions";
import { useSidecar } from "./hooks/useSidecar";

const NO_CATALOGS: CatalogInfo[] = [];

export default function App() {
  const [view, setView] = useState(initialView);
  // Remounts the Diff view (which stays mounted across tabs) when a reload or rescan invalidates its refs.
  const [diffEpoch, setDiffEpoch] = useState(0);
  // Catalogs-tab selected row: cleared on reload, since catalog ids are list positions.
  const [selectedCatalogId, setSelectedCatalogId] = useState<number | null>(null);
  const [aboutOpen, setAboutOpen] = useState(false);
  const [syntaxOpen, setSyntaxOpen] = useState(false);
  const [editingDescription, setEditingDescription] = useState<CatalogInfo | null>(null);
  const [searchPrefill, setSearchPrefill] = useState<{ pattern: string; nonce: number } | null>(null);
  const [collapseNonce, setCollapseNonce] = useState(0);

  const sidecar = useSidecar({
    boot: async (ui) => {
      await shell.loadCommands();
      await colours.load();
      await catalogsState.loadInitial(typeof ui.catalogDir === "string" ? ui.catalogDir : undefined);
    },
  });
  const { getClient, getUi, setStatus } = sidecar;
  const colours = useColourRules({ getClient });
  const shell = useShellActions({ getClient });
  const legacy = useLegacyMigration({ getUi });
  const newCatalog = useNewCatalog({ getUi });

  const showDirectory = useCallback(() => setView((v) => showView(v, "directory")), []);
  const showResults = useCallback(() => setView((v) => showView(v, "results")), []);
  const showTreemap = useCallback((ref: EntryRef) => setView((v) => showTreemapView(v, ref)), []);
  const collapseAll = useCallback(() => setCollapseNonce((n) => n + 1), []);

  const selection = useCatalogSelection({ getClient, showDirectory });
  const search = useSearch({ getClient, getUi, onStart: showResults });
  const { resetForReload, snapshot, restoreAfterRescan, openRoot, viewInTree } = selection;
  const { markRescanned } = search;

  const onBeforeReload = useCallback(() => {
    resetForReload();
    setSelectedCatalogId(null); // ids are list positions: the old one may name a different catalog now
    setDiffEpoch((n) => n + 1);
    setView(dropTreemap);
  }, [resetForReload]);
  // Jobs block a load, but useJobs needs this hook's refresh, so the check reads its latest state.
  const jobsBusyRef = useRef(false);
  const isBlocked = useCallback(() => jobsBusyRef.current, []);
  // Rescan-on-startup runs once, for the first load of the session, and only after the window is up.
  const startupChecked = useRef(false);
  const startupIds = useRef<number[]>([]);
  const { promptFor } = legacy;
  const onLoaded = useCallback(
    (loaded: CatalogsResponse) => {
      promptFor(loaded);
      if (startupChecked.current) return;
      startupChecked.current = true;
      startupIds.current = startupRescanIds(loaded, new Date());
    },
    [promptFor],
  );
  const catalogsState = useCatalogs({ getClient, getUi, setStatus, isBlocked, onBeforeReload, onLoaded });
  const { catalogs, refreshCatalogs, remountRoot } = catalogsState;
  const catalogList = catalogs?.catalogs ?? NO_CATALOGS;

  // After the sidecar swapped the catalog in: refresh the lists, remount that catalog's tree root,
  // mark results into it stale, and put the selection back (nearest surviving parent if it is gone).
  const afterRescan = useCallback(
    async (client: CdeApiClient, catalogId: number) => {
      const before = snapshot();
      // Bookkeeping first: it must happen even if the refresh below fails.
      remountRoot(catalogId);
      markRescanned(catalogId);
      setDiffEpoch((n) => n + 1);
      // Indexes belong to the old file: a TreeMap request for this catalog is no longer valid.
      setView((v) => (v.treemap?.ref.catalogId === catalogId ? dropTreemap(v) : v));
      await refreshCatalogs(client);
      await restoreAfterRescan(client, catalogId, before);
    },
    [refreshCatalogs, snapshot, restoreAfterRescan, markRescanned, remountRoot],
  );
  const openNewRoot = useCallback(
    (catalogId: number, rootPath: string) => openRoot({ catalogId, entryIndex: 0 }, rootPath),
    [openRoot],
  );
  const jobs = useJobs({ getClient, sidecarReady: sidecar.ready, afterRescan, refreshCatalogs, openCatalog: openNewRoot });
  jobsBusyRef.current = jobs.busy;

  const { rescanMany } = jobs;
  useEffect(() => {
    if (!sidecar.ready || startupIds.current.length === 0) return;
    const ids = startupIds.current;
    startupIds.current = [];
    void rescanMany(ids);
  }, [sidecar.ready, rescanMany]);

  const staleAfterDays = catalogs?.staleAfterDays ?? 0;
  // One clock for the list badges, the status bar count and Rescan stale, so they agree as catalogs age.
  const now = useNow(60_000);
  const staleIds = useMemo(() => staleCatalogIds(catalogList, staleAfterDays, now), [catalogList, staleAfterDays, now]);

  // Customize re-reads the rules file each time it opens: fetch first, then switch, so the editor never
  // shows stale text that a keystroke could then overwrite.
  const { load: loadColours } = colours;
  const selectTab = useCallback(
    (kind: ViewKind) => {
      if (kind === "colours") void loadColours().then(() => setView((v) => showView(v, "colours")));
      else if (kind === "treemap") setView(selectTreemap);
      else setView((v) => showView(v, kind));
    },
    [loadColours],
  );

  const searchFromHere = useCallback(
    (ref: EntryRef, fullPath: string) => {
      const c = catalogList.find((x) => x.catalogId === ref.catalogId);
      setSearchPrefill((p) => ({ pattern: searchFromHereQuery(fullPath, c ? catalogFile(c) : ""), nonce: (p?.nonce ?? 0) + 1 }));
    },
    [catalogList],
  );

  const menus = useContextMenus({
    commands: shell.commands,
    catalogs: catalogList,
    jobs: jobs.model,
    selectedKeyRef: selection.selectedKeyRef,
    runShell: shell.runShell,
    showTreemap,
    searchFromHere,
    viewInTree,
    rescanCatalog: jobs.rescanCatalog,
    editDescription: setEditingDescription,
    collapseAll,
  });

  if (!sidecar.ready) {
    return <div className="boot">{sidecar.status || "Loading…"}</div>;
  }

  const client = getClient();
  const ui = getUi();
  const loadBlocked = loadBlockedReason(jobs.busy);
  const reload = () => void catalogsState.reload(catalogsState.catalogDir);

  return (
    <div className="app">
      <MenuBar
        onNewCatalog={() => void newCatalog.pick()}
        onOpenFolder={async () => {
          if (await catalogsState.openFolder()) setView((v) => showView(v, "catalogs"));
        }}
        onReload={reload}
        onRescanAll={() => void jobs.rescanMany(catalogList.map((c) => c.catalogId))}
        rescanAllDisabledReason={catalogList.length === 0 ? "No catalogs are loaded" : null}
        onExit={() => void getCurrentWindow().close()}
        onAbout={() => setAboutOpen(true)}
        onSearchSyntax={() => setSyntaxOpen(true)}
        loadDisabledReason={loadBlocked}
      />

      <SearchBar
        busy={search.busy}
        history={(ui?.get().searchHistory as string[]) ?? []}
        defaults={ui?.get() ?? {}}
        filtersExpanded={ui?.get().filtersExpanded ?? true}
        onFiltersExpandedChange={(filtersExpanded) => ui?.update({ filtersExpanded })}
        onSearch={search.runSearch}
        onCancel={search.cancelSearch}
        prefill={searchPrefill}
        queryLimit={search.queryLimit}
      />

      <TabBar
        active={view.kind}
        hasTreemap={!!view.treemap}
        activeJobCount={activeCount(jobs.model)}
        onSelect={selectTab}
        onDirectoryMenu={menus.onDirectoryTabMenu}
        loadBlockedReason={loadBlocked}
        onReload={reload}
      />

      <MainLayout
        view={view}
        setView={setView}
        client={client}
        ui={ui}
        catalogs={catalogList}
        roots={catalogsState.roots}
        selection={selection}
        search={search}
        colours={colours}
        jobs={jobs}
        menus={menus}
        diffEpoch={diffEpoch}
        collapseNonce={collapseNonce}
        selectedCatalogId={selectedCatalogId}
        staleAfterDays={staleAfterDays}
        now={now}
        onSelectCatalog={setSelectedCatalogId}
        onNewCatalog={() => void newCatalog.pick()}
        onExplore={shell.exploreEntry}
        onStatus={setStatus}
      />

      <StatusBar
        catalogs={catalogs}
        resultCount={search.results.length}
        searchTiming={sidecar.status || search.timing}
        activeJobCount={activeCount(jobs.model)}
        onShowJobs={() => setView((v) => showView(v, "jobs"))}
        legacyNotice={legacyNotice(catalogs?.legacyCatalogs ?? [])}
        onMigrate={legacy.show}
        staleCount={staleIds.length}
        staleAfterDays={staleAfterDays}
        onRescanStale={() => void jobs.rescanMany(staleIds)}
      />
      <ContextMenu menu={menus.menu} onClose={menus.close} />
      <RootUnreachableModal rootPath={jobs.unreachableRoot} onClose={jobs.clearUnreachable} />
      <AboutModal opened={aboutOpen} onClose={() => setAboutOpen(false)} />
      <SearchSyntaxModal opened={syntaxOpen} onClose={() => setSyntaxOpen(false)} />
      <MigrateLegacyDialog
        opened={legacy.open}
        legacy={catalogs?.legacyCatalogs ?? []}
        folder={catalogs?.catalogDir ?? ""}
        blockedReason={jobs.busy ? "Unavailable while catalog jobs are queued, running or finishing; try again when they finish." : null}
        client={client}
        onNotNow={() => legacy.dismiss(catalogs?.catalogDir)}
        onFinished={() => {
          // The report has been seen: don't open again by itself here (failures stay in the status-bar notice).
          legacy.dismiss(catalogs?.catalogDir);
          reload();
        }}
      />
      <EditDescriptionDialog catalog={editingDescription} onSave={catalogsState.saveDescription} onClose={() => setEditingDescription(null)} />
      <NewCatalogDialog
        rootPath={newCatalog.root}
        client={client}
        catalogs={catalogList}
        onChangeRoot={() => void newCatalog.pick()}
        onCreate={(form) => {
          newCatalog.close();
          void jobs.createCatalog(form);
        }}
        onRescanExisting={(catalogId) => {
          newCatalog.close();
          void jobs.rescanCatalog(catalogId);
        }}
        onClose={newCatalog.close}
      />
    </div>
  );
}
