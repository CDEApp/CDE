import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { open, save } from "@tauri-apps/plugin-dialog";
import { writeFile } from "@tauri-apps/plugin-fs";
import { notifications } from "@mantine/notifications";
import { CdeApiClient, CreateFailure, ShellError } from "./api/client";
import {
  anyJobRunning,
  beginCreate,
  emptyCreateJobs,
  endCreate,
  jobStatusLines,
  loadBlockedReason,
  parseJobKey,
  updateCreate,
  type CreateJobs,
} from "./catalogJobs";
import { findCatalogByTarget } from "./newCatalog";
import { NewCatalogDialog, type NewCatalogForm } from "./components/NewCatalogDialog";
import { EditDescriptionDialog } from "./components/EditDescriptionDialog";
import { RootUnreachableModal } from "./components/RootUnreachableModal";
import { AboutModal } from "./components/AboutModal";
import { SearchSyntaxModal } from "./components/SearchSyntaxModal";
import { MigrateLegacyDialog } from "./components/MigrateLegacyDialog";
import { dismissFolder, legacyNotice, shouldPromptLegacy } from "./legacyMigration";
import {
  CatalogInfo,
  CatalogsResponse,
  CreateResult,
  DirectoryNode,
  EntryRef,
  refToString,
  ShellAction,
  TreemapRow,
} from "./api/types";
import { startSidecar, Sidecar } from "./sidecar";
import { UiStateManager } from "./state/uiState";
import { catalogFile, catalogName, fileBaseName, skipSummary } from "./format";
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
import { createRuleResolver, parseColourRules } from "./colourRules";
import type { CatalogColumnWidths } from "./catalogColumns";
import type { ColumnWidths } from "./columnWidths";
import { viewPaneClass } from "./viewPane";
import { createSerialSaver, SerialSaver } from "./serialSaver";
import { parseRef, useCatalogSelection } from "./hooks/useCatalogSelection";
import { useRescan } from "./hooks/useRescan";
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
  collapseAllItem,
  editDescriptionItem,
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
  // Colour rules text (file-backed via the sidecar). Parsed + compiled once per change; the
  // resolvers (with their per-name caches) are rebuilt whenever the text changes.
  const [rulesText, setRulesText] = useState("");
  // Saves live here, not in ColoursView, so a save flushed as the tab closes still runs in order
  // and its error is still shown when the tab reopens.
  const rulesSaverRef = useRef<SerialSaver | null>(null);
  const [rulesSaveError, setRulesSaveError] = useState<string | null>(null);
  const rules = useMemo(() => parseColourRules(rulesText).rules, [rulesText]);
  const resolveStyle = useMemo(() => createRuleResolver(rules), [rules]);
  const resolveTreeStyle = useMemo(() => createRuleResolver(rules, { skipFolder: true }), [rules]);
  const [commands, setCommands] = useState<{ id: number; label: string }[]>([]);

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
  // The migrate dialog for the legacy .cde catalogs the last load listed (the sidecar does not load them).
  const [legacyOpen, setLegacyOpen] = useState(false);
  // Per-catalog counter that remounts a tree root after a rescan (drops its cached children).
  const treeGenRef = useRef<Record<number, number>>({});
  // --- new catalog: the folder awaiting confirmation in the dialog, and the creates running in the background ---
  const [newCatalogRoot, setNewCatalogRoot] = useState<string | null>(null);
  const [creates, setCreates] = useState<CreateJobs>(emptyCreateJobs);
  const createAborts = useRef(new Map<number, AbortController>());
  const createSeq = useRef(0);
  // Bumped on every Reload / Open Folder: the Directory view stays mounted across tab switches (#80),
  // so a reload must drop the tree's cached expansion + children itself.
  const reloadEpochRef = useRef(0);
  // Remounts the Diff view (which stays mounted across tabs) when a reload or rescan invalidates its refs.
  const [diffEpoch, setDiffEpoch] = useState(0);
  // Latest values for async completions (a create finishes long after the click that started it).
  const latest = useRef({ catalogs, creates });
  latest.current = { catalogs, creates };

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
        rulesSaverRef.current = createSerialSaver((text) => client.putColourRules(text), setRulesSaveError);

        const ui = await uiRef.current.load();
        setCommands(await client.shellCommands());
        try {
          setRulesText(await client.getColourRules());
        } catch (e) {
          console.error("colour rules failed to load; continuing without custom colours", e);
        }
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

  // After a load: open the migrate dialog when the folder has legacy catalogs, unless "Not now" was chosen for it.
  const promptForLegacy = useCallback((cat: CatalogsResponse) => {
    if (shouldPromptLegacy(cat.legacyCatalogs ?? [], cat.catalogDir, uiRef.current?.get().legacyMigrateDismissed)) {
      setLegacyOpen(true);
    }
  }, []);

  // Remember the folder so the dialog no longer opens by itself there; the status-bar notice stays.
  const dismissLegacy = useCallback(() => {
    const folder = latest.current.catalogs?.catalogDir;
    const ui = uiRef.current;
    if (folder && ui) ui.update({ legacyMigrateDismissed: dismissFolder(ui.get().legacyMigrateDismissed, folder) });
    setLegacyOpen(false);
  }, []);

  // Stable accessors so the hooks' callbacks keep a stable identity.
  const getClient = useCallback(() => {
    const client = clientRef.current;
    if (!client) throw new Error("The catalog service is not ready yet.");
    return client;
  }, []);
  const getUi = useCallback(() => uiRef.current, []);
  const getCatalogs = useCallback(() => latest.current.catalogs, []);
  const showDirectory = useCallback(() => setView((v) => showView(v, "directory")), []);
  const showResults = useCallback(() => setView((v) => showView(v, "results")), []);

  const selection = useCatalogSelection({ getClient, showDirectory });
  const { results, busy, timing, runSearch, cancelSearch, searchAgain, markRescanned, staleBanner: staleBannerFor } = useSearch({
    getClient,
    getUi,
    onStart: showResults,
  });
  const { selectNode, viewInTree, openRoot, resetForReload, selectedKeyRef, selectFromTree, activateDir } = selection;

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
  const { rescans, getRescans, unreachableRoot, clearUnreachable, rescanCatalog, cancelRescan } = useRescan({
    getClient,
    getCatalogs,
    afterSwap: afterRescan,
  });

  const reloadCatalogs = useCallback(
    async (dir?: string | null) => {
      if (anyJobRunning(getRescans(), latest.current.creates)) return; // the menus are disabled too; belt and braces
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
    [refreshCatalogs, promptForLegacy, resetForReload, getRescans],
  );

  // Re-read the rules file each time the Customize tab opens (picks up hand edits). Fetch first, then
  // switch, so the editor never shows stale text that a keystroke could then overwrite. Wait for
  // pending saves first, or a save flushed as the tab last closed could land after this read.
  const openColours = useCallback(async () => {
    try {
      await rulesSaverRef.current?.idle();
      setRulesText(await clientRef.current!.getColourRules());
    } catch (e) {
      console.error("colour rules reload failed", e);
    }
    setView((v) => showView(v, "colours"));
  }, []);

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
    if (anyJobRunning(getRescans(), latest.current.creates)) return;
    const picked = await open({ directory: true, multiple: false, title: "Select catalog folder" });
    if (typeof picked !== "string") return; // cancelled
    setCatalogDir(picked);
    uiRef.current?.update({ catalogDir: picked });
    await reloadCatalogs(picked);
    setView((v) => showView(v, "catalogs"));
  }, [reloadCatalogs, getRescans]);

  // --- new catalog ---
  // Native folder picker starting at the last root used for a new catalog (saved in UI state).
  const pickNewCatalogRoot = useCallback(async (): Promise<string | null> => {
    const last = uiRef.current?.get().lastNewCatalogRoot;
    const picked = await open({
      directory: true,
      multiple: false,
      title: "Select the folder to catalog",
      defaultPath: typeof last === "string" && last ? last : undefined,
    });
    if (typeof picked !== "string") return null; // cancelled
    uiRef.current?.update({ lastNewCatalogRoot: picked });
    return picked;
  }, []);

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

  // Start (File menu / Catalogs tab) and "Change…" in the dialog: both just (re)pick the root.
  const pickNewRoot = useCallback(async () => {
    const picked = await pickNewCatalogRoot();
    if (picked) setNewCatalogRoot(picked);
  }, [pickNewCatalogRoot]);

  // The toast's [Open]: jump to the new root in the Directory tab (only now, never automatically).
  const openNewRoot = useCallback(
    (result: CreateResult) => openRoot({ catalogId: result.catalogId, entryIndex: 0 }, result.rootPath),
    [openRoot],
  );

  const createCatalog = useCallback(
    async (form: NewCatalogForm) => {
      const client = clientRef.current!;
      const id = ++createSeq.current;
      const abort = new AbortController();
      createAborts.current.set(id, abort);
      const job = { id, rootPath: form.rootPath, fileName: form.fileName, targetPath: form.targetPath };
      latest.current.creates = beginCreate(latest.current.creates, job);
      setCreates((s) => beginCreate(s, job));
      setNewCatalogRoot(null); // close the dialog: progress lives in the status bar now
      try {
        // Phase 1: the create's own outcome decides the success / failure toast.
        let result: CreateResult | null = null;
        try {
          result = await client.createCatalog(
            {
              rootPath: form.rootPath,
              fileName: form.fileName,
              description: form.description,
              followJunctions: form.followJunctions,
            },
            {
              signal: abort.signal,
              onProgress: (p) =>
                setCreates((s) => updateCreate(s, id, { phase: p.phase, entriesScanned: p.entriesScanned })),
            },
          );
          if (!result && form.targetPath) {
            // Cancelled. If the cancel lost the race with the final rename the catalog was added: detect that.
            try {
              // Exact match on the path the sidecar reported: no case guessing on the client.
              const found = findCatalogByTarget((await client.catalogs()).catalogs, form.targetPath);
              if (found) {
                result = {
                  catalogId: found.catalogId,
                  rootPath: found.rootPath,
                  entries: found.dirEntryCount + found.fileEntryCount,
                  savedPath: found.sourcePath!,
                };
              }
            } catch {
              /* could not check: report it as cancelled */
            }
          }
        } catch (e) {
          const code = e instanceof CreateFailure ? e.error.code : "";
          notifications.show({
            color: "red",
            title: "New catalog failed",
            message:
              `${form.rootPath}: ${e instanceof Error ? e.message : String(e)}` +
              (code === "file-exists" ? "" : " Nothing was written."),
          });
          return;
        }

        if (!result) {
          notifications.show({
            color: "gray",
            title: "New catalog cancelled",
            message: `${form.rootPath} was not cataloged; nothing was written.`,
          });
          return;
        }

        // Phase 2: the catalog exists. Refresh the lists only: ids, tree state and the Directory tab stay as they are.
        const done = result;
        const skipped = skipSummary(done.excludedFolderCount, done.accessDeniedFolderCount, done.unreadableFolderCount);
        const toastId = `new-catalog-${done.catalogId}-${id}`;
        let refreshed = true;
        try {
          await refreshCatalogs(client);
        } catch {
          refreshed = false;
        }
        notifications.show({
          id: toastId,
          color: refreshed ? "green" : "orange",
          title: "Catalog created",
          autoClose: 15000,
          message: (
            <span>
              {refreshed
                ? `Created ${form.fileName}.cdex — ${done.entries.toLocaleString()} entries${skipped ? `; ${skipped}` : ""}. `
                : `Created ${form.fileName}.cdex, but refreshing the view failed — use Reload. `}
              {refreshed && (
                <button
                  className="toast-action"
                  onClick={() => {
                    notifications.hide(toastId);
                    void openNewRoot(done);
                  }}
                >
                  Open
                </button>
              )}
            </span>
          ),
        });
      } finally {
        createAborts.current.delete(id);
        latest.current.creates = endCreate(latest.current.creates, id);
        setCreates((s) => endCreate(s, id));
      }
    },
    [refreshCatalogs, openNewRoot],
  );

  // Status-bar Cancel for a running job: "rescan:<catalogId>" or "create:<jobId>".
  const cancelJob = useCallback((key: string) => {
    const job = parseJobKey(key);
    if (!job) return;
    if (job.kind === "rescan") cancelRescan(job.id);
    else createAborts.current.get(job.id)?.abort();
  }, [cancelRescan]);

  // --- shell actions / context menu ---
  const runShell = useCallback(async (ref: EntryRef, action: ShellAction, commandId?: number) => {
    try {
      await clientRef.current!.shell({ ref: refToString(ref), action, commandId });
    } catch (e) {
      // 501/422 carry the reason the OS action did not happen; anything else mirrors cdeWin, where
      // shell actions are silent no-ops when the entry isn't on this filesystem.
      if (e instanceof ShellError && e.status !== 400) {
        notifications.show({ color: "red", title: "Could not run the action", message: e.message });
      }
    }
  }, []);
  const exploreEntry = useCallback((ref: EntryRef) => void runShell(ref, "explore"), [runShell]);

  const showTreemap = useCallback((ref: EntryRef) => setView((v) => showTreemapView(v, ref)), []);

  const rescanItem = useCallback(
    (catalogId: number) =>
      rescanMenuEntry(
        rescans,
        catalogId,
        catalogs?.catalogs.find((c) => c.catalogId === catalogId)?.rootPath,
        (id) => void rescanCatalog(id),
      ),
    [rescans, catalogs, rescanCatalog],
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
      commands,
      runShell: (ref, action, commandId) => void runShell(ref, action, commandId),
      showTreemap,
      searchFromHere,
      copyPath: (fullPath) => void navigator.clipboard.writeText(fullPath),
      rescanFor: rescanItem,
      editDescription: editCatalogDescription,
    }),
    [commands, runShell, showTreemap, searchFromHere, rescanItem, editCatalogDescription],
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
  const loadBlockedReasonText = loadBlockedReason(rescans, creates);
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
            rescanningIds={new Set(rescans.keys())}
            sort={catalogSort}
            selectedId={selectedCatalogId}
            onSelect={setSelectedCatalogId}
            onSort={(column) => setCatalogSort((s) => nextCatalogSort(s, column))}
            columnWidths={uiRef.current?.get().catalogColumnWidths}
            onColumnWidthsChange={onCatalogColumnWidthsChange}
            onNewCatalog={() => void pickNewRoot()}
            onContextMenu={(c, x, y) =>
              setMenu({ x, y, items: [editDescriptionItem(() => setEditingDescription(c)), rescanItem(c.catalogId)] })
            }
            onActivate={(c: CatalogInfo) => {
              showDirectory();
              void selectNode({ catalogId: c.catalogId, entryIndex: 0 }, c.rootPath);
            }}
          />
        );
      case "colours":
        return (
          <ColoursView
            text={rulesText}
            onChange={setRulesText}
            save={(text) => rulesSaverRef.current?.save(text)}
            saveError={rulesSaveError}
            getDefaults={() => clientRef.current!.getColourRuleDefaults()}
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
        onNewCatalog={() => void pickNewRoot()}
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
        jobs={jobStatusLines(rescans, creates)}
        onCancelJob={cancelJob}
        legacyNotice={legacyNotice(catalogs?.legacyCatalogs ?? [])}
        onMigrate={() => setLegacyOpen(true)}
      />
      <ContextMenu menu={menu} onClose={() => setMenu(null)} />
      <RootUnreachableModal rootPath={unreachableRoot} onClose={clearUnreachable} />
      <AboutModal opened={aboutOpen} onClose={() => setAboutOpen(false)} />
      <SearchSyntaxModal opened={syntaxOpen} onClose={() => setSyntaxOpen(false)} />
      <MigrateLegacyDialog
        opened={legacyOpen}
        legacy={catalogs?.legacyCatalogs ?? []}
        folder={catalogs?.catalogDir ?? ""}
        blockedReason={
          anyJobRunning(rescans, creates)
            ? "Unavailable while a catalog is being rescanned or created; try again when that finishes."
            : null
        }
        client={clientRef.current}
        onNotNow={dismissLegacy}
        onFinished={() => {
          // The report has been seen: don't open again by itself here (failures stay in the status-bar notice).
          dismissLegacy();
          void reloadCatalogs(catalogDir);
        }}
      />
      <EditDescriptionDialog
        catalog={editingDescription}
        onSave={saveDescription}
        onClose={() => setEditingDescription(null)}
      />
      <NewCatalogDialog
        rootPath={newCatalogRoot}
        client={clientRef.current}
        catalogs={catalogs?.catalogs ?? []}
        onChangeRoot={() => void pickNewRoot()}
        onCreate={(form) => void createCatalog(form)}
        onRescanExisting={(catalogId) => {
          setNewCatalogRoot(null);
          void rescanCatalog(catalogId);
        }}
        onClose={() => setNewCatalogRoot(null)}
      />
    </div>
  );
}
