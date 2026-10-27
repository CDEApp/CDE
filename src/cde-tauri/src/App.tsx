import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { open } from "@tauri-apps/plugin-dialog";
import { notifications } from "@mantine/notifications";
import { CdeApiClient, CreateFailure, RescanFailure } from "./api/client";
import {
  beginRescan,
  emptyRescanState,
  endRescan,
  isRescanning,
  rescanMenuItem,
  resolveSurvivingFolder,
  rowStillResolves,
  staleResultsBanner,
  updateRescan,
  type RescanState,
} from "./rescan";
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
import { RootUnreachableModal } from "./components/RootUnreachableModal";
import { AboutModal } from "./components/AboutModal";
import {
  CatalogInfo,
  CatalogsResponse,
  CreateResult,
  DirectoryNode,
  EntryRef,
  refToString,
  SearchQuery,
  SearchResultRow,
  ShellAction,
  TreemapRow,
} from "./api/types";
import { startSidecar, Sidecar } from "./sidecar";
import { UiStateManager } from "./state/uiState";
import { catalogName } from "./format";
import type { TreeRoot } from "./components/CatalogTree";
import { DEFAULT_SORT, type DirectorySort, type SortColumn, nextSort } from "./directorySort";
import { DirectoryPane } from "./components/DirectoryPane";
import { ResultsList } from "./components/ResultsList";
import { CatalogList } from "./components/CatalogList";
import { SearchBar } from "./components/SearchBar";
import { StatusBar } from "./components/StatusBar";
import { MenuBar } from "./components/MenuBar";
import { ContextMenu, MenuItem, MenuState } from "./components/ContextMenu";
import { TreemapView } from "./components/TreemapView";
import { ColoursView } from "./components/ColoursView";
import { createRuleResolver, parseColourRules } from "./colourRules";
import { viewPaneClass } from "./viewPane";
import { createRequestGuard } from "./requestGuard";
import { createSerialSaver, SerialSaver } from "./serialSaver";

type View = "directory" | "results" | "catalogs" | "colours" | "treemap";

const DEFAULT_TREEMAP_DEPTH = 3;

export default function App() {
  const clientRef = useRef<CdeApiClient | null>(null);
  const sidecarRef = useRef<Sidecar | null>(null);
  const uiRef = useRef<UiStateManager | null>(null);
  const searchAbort = useRef<AbortController | null>(null);
  const lastQueryRef = useRef<SearchQuery | null>(null); // for the stale-results "Search again"

  const [status, setStatus] = useState("Starting…");
  const [ready, setReady] = useState(false);
  const [view, setView] = useState<View>("directory");
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

  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [revealKey, setRevealKey] = useState<string | null>(null);
  // Bumped per View-in-Tree, so revealing the already-selected node again re-centres it (the tree stays mounted).
  const [revealNonce, setRevealNonce] = useState(0);
  const [expandTo, setExpandTo] = useState<EntryRef[] | undefined>(undefined);
  const [dirPath, setDirPath] = useState("");
  const [dirNodes, setDirNodes] = useState<DirectoryNode[]>([]);
  // Directory-listing sort: session-only (deliberately not in uiState) and kept across folders.
  const [dirSort, setDirSort] = useState<DirectorySort>(DEFAULT_SORT);

  const [results, setResults] = useState<SearchResultRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [timing, setTiming] = useState("");
  const [menu, setMenu] = useState<MenuState | null>(null);
  const [catalogDir, setCatalogDir] = useState<string | null>(null);
  // TreeMap tab: absent until the first "Show TreeMap"; each request bumps the nonce so mapping
  // the same folder again still re-fetches.
  const [treemapRequest, setTreemapRequest] = useState<{ ref: EntryRef; nonce: number } | null>(null);
  const [treemapDepth, setTreemapDepth] = useState<number | null>(null);

  // --- rescan: which catalogs are rescanning, their cancel handles, and what went stale ---
  const [rescans, setRescans] = useState<RescanState>(emptyRescanState);
  const rescanAborts = useRef(new Map<number, AbortController>());
  const [unreachableRoot, setUnreachableRoot] = useState<string | null>(null);
  const [aboutOpen, setAboutOpen] = useState(false);
  // Catalogs rescanned since the current results were produced (drives the stale banner).
  const [rescannedSinceSearch, setRescannedSinceSearch] = useState<ReadonlySet<number>>(new Set());
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
  // Last-navigation-wins: drops a children/path response that a newer selection or a reload overtook.
  const navGuard = useMemo(() => createRequestGuard(), []);
  // Latest values for async completions (a rescan finishes long after the click that started it).
  const latest = useRef({ catalogs, selectedKey, dirPath, rescans, creates, treemapRequest });
  latest.current = { catalogs, selectedKey, dirPath, rescans, creates, treemapRequest };

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
        // Re-open the last-used catalog folder, if any.
        const savedDir = typeof ui.catalogDir === "string" ? ui.catalogDir : undefined;
        if (savedDir) {
          setCatalogDir(savedDir);
          await client.reload({}, savedDir);
        }
        await refreshCatalogs(client);
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

  const refreshCatalogs = useCallback(async (client: CdeApiClient) => {
    const cat = await client.catalogs();
    setCatalogs(cat);
    setRoots(
      cat.catalogs.map<TreeRoot>((c) => ({
        ref: { catalogId: c.catalogId, entryIndex: 0 },
        label: c.rootPath,
        fullPath: c.rootPath,
        size: c.rootSize,
        catalogName: catalogName(c.actualFileName),
        generation: (treeGenRef.current[c.catalogId] ?? 0) + reloadEpochRef.current,
      })),
    );
  }, []);

  const reloadCatalogs = useCallback(
    async (dir?: string | null) => {
      if (anyJobRunning(latest.current.rescans, latest.current.creates)) return; // the menus are disabled too; belt and braces
      const client = clientRef.current!;
      setStatus("Loading catalogs…");
      // Reset BEFORE the call, so it also happens when the reload fails part-way (the sidecar may
      // already have dropped the old catalogs): refs and expansion from the old load are not
      // trusted, and in-flight navigation responses must not land after this.
      navGuard.invalidate();
      reloadEpochRef.current += 1;
      setSelectedKey(null);
      setRevealKey(null);
      setExpandTo(undefined);
      setDirPath("");
      setDirNodes([]);
      setTreemapRequest(null); // its ref belongs to the old catalog set
      setView((v) => (v === "treemap" ? "directory" : v));
      try {
        await client.reload({}, dir ?? undefined);
        await refreshCatalogs(client);
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
    [refreshCatalogs, navGuard],
  );

  // Re-read the rules file each time the Colours tab opens (picks up hand edits). Fetch first, then
  // switch, so the editor never shows stale text that a keystroke could then overwrite. Wait for
  // pending saves first, or a save flushed as the tab last closed could land after this read.
  const openColours = useCallback(async () => {
    try {
      await rulesSaverRef.current?.idle();
      setRulesText(await clientRef.current!.getColourRules());
    } catch (e) {
      console.error("colour rules reload failed", e);
    }
    setView("colours");
  }, []);

  const onTreePaneResize = useCallback((width: number) => {
    uiRef.current?.update({ treePaneWidth: Math.round(width) });
  }, []);

  // File ▸ Open Folder… — native folder picker, then load .cdex catalogs from it.
  const openFolder = useCallback(async () => {
    if (anyJobRunning(latest.current.rescans, latest.current.creates)) return;
    const picked = await open({ directory: true, multiple: false, title: "Select catalog folder" });
    if (typeof picked !== "string") return; // cancelled
    setCatalogDir(picked);
    uiRef.current?.update({ catalogDir: picked });
    await reloadCatalogs(picked);
    setView("catalogs");
  }, [reloadCatalogs]);

  // Update the directory selection + listing. Deliberately does NOT change `view`: the view is
  // switched synchronously by whichever handler initiated the navigation, so a late-resolving
  // listing fetch here can never clobber a newer tab click.
  // Returns false when the response was dropped (a newer selection or a reload came first).
  const selectNode = useCallback(
    async (ref: EntryRef, fullPath: string): Promise<boolean> => {
      const client = clientRef.current!;
      const token = navGuard.begin();
      setSelectedKey(refToString(ref));
      setDirPath(fullPath);
      const nodes = await client.children(ref, { sort: "name", hasChildren: false });
      if (!navGuard.isCurrent(token)) return false;
      setDirNodes(nodes);
      return true;
    },
    [navGuard],
  );

  // drill into a directory row (already in the directory view)
  const activateDir = useCallback(
    async (node: DirectoryNode) => {
      if (!node.isDirectory) return;
      const token = navGuard.begin();
      const path = await refsToPath(clientRef.current!, node.ref);
      if (!navGuard.isCurrent(token)) return;
      setExpandTo(path);
      await selectNode(node.ref, node.fullPath);
    },
    [selectNode, navGuard],
  );

  // view a search result in the directory tree (expand to its parent)
  const viewInTree = useCallback(
    async (ref: EntryRef, expectedPath?: string) => {
      setView("directory"); // synchronous: this is the user's navigation intent
      const client = clientRef.current!;
      const token = navGuard.begin();
      let chain: DirectoryNode[];
      try {
        chain = await client.path(ref);
      } catch {
        chain = [];
      }
      if (!navGuard.isCurrent(token)) return; // superseded, or the catalogs were reloaded meanwhile
      // A ref from before a rescan can be out of range or point at a different entry: verify by path.
      if (expectedPath !== undefined && !rowStillResolves(expectedPath, chain)) {
        notifications.show({
          color: "yellow",
          title: "Not found",
          message: `${expectedPath} is no longer in the catalog (it may have been rescanned). Search again.`,
        });
        return;
      }
      if (chain.length === 0) return;
      const refs = chain.map((n) => n.ref);
      // expand to the parent (drop the entry itself), then select the parent
      const parent = refs.length > 1 ? refs[refs.length - 2] : refs[0];
      setExpandTo(refs.slice(0, -1));
      const parentNode = chain[chain.length - 2] ?? chain[chain.length - 1];
      if (!(await selectNode(parent, parentNode.fullPath))) return;
      setRevealKey(refToString(parent)); // centre this node in the tree (it's the reveal target)
      setRevealNonce((n) => n + 1);
    },
    [selectNode, navGuard],
  );

  // --- search ---
  const runSearch = useCallback(async (query: SearchQuery) => {
    const client = clientRef.current!;
    lastQueryRef.current = query;
    uiRef.current?.pushSearchHistory(query.pattern);
    uiRef.current?.update({
      regexMode: query.regexMode,
      includePath: query.includePath,
      includeFiles: query.includeFiles,
      includeFolders: query.includeFolders,
      limitResultCount: query.limitResultCount,
    });

    setView("results");
    setResults([]);
    setRescannedSinceSearch(new Set()); // fresh results are not stale
    setBusy(true);
    setTiming("Searching…");

    const abort = new AbortController();
    searchAbort.current = abort;
    const start = performance.now();
    const buffer: SearchResultRow[] = [];
    let flush: ReturnType<typeof setTimeout> | null = null;
    const scheduleFlush = () => {
      if (flush) return;
      flush = setTimeout(() => {
        flush = null;
        setResults([...buffer]);
      }, 100);
    };

    try {
      await client.search(query, {
        signal: abort.signal,
        onResult: (row) => {
          buffer.push(row);
          scheduleFlush();
        },
        onProgress: (p) =>
          setTiming(`${p.total > 0 ? Math.round((100 * p.count) / p.total) : 0}%`),
        onDone: (count) => {
          setResults([...buffer]);
          setTiming(`${count.toLocaleString()} results in ${Math.round(performance.now() - start)} ms`);
        },
      });
    } catch (e) {
      setTiming(`Error: ${(e as Error).message}`);
    } finally {
      if (flush) clearTimeout(flush);
      setResults([...buffer]);
      setBusy(false);
      searchAbort.current = null;
    }
  }, []);

  const cancelSearch = useCallback(() => {
    searchAbort.current?.abort();
  }, []);

  // --- rescan ---
  // After the sidecar swapped the catalog in: refresh the lists, remount that catalog's tree root,
  // mark results into it stale, and put the selection back (nearest surviving parent if it is gone).
  const afterRescan = useCallback(
    async (client: CdeApiClient, catalogId: number) => {
      const { selectedKey: selKey, dirPath: selPath } = latest.current;
      const selected = selKey ? parseRef(selKey) : null;
      // Bookkeeping first: it must happen even if the refresh below fails.
      treeGenRef.current[catalogId] = (treeGenRef.current[catalogId] ?? 0) + 1;
      setRescannedSinceSearch((prev) => new Set(prev).add(catalogId));
      // Indexes belong to the old file: a TreeMap request for this catalog is no longer valid.
      if (latest.current.treemapRequest?.ref.catalogId === catalogId) {
        setTreemapRequest(null);
        setView((v) => (v === "treemap" ? "directory" : v));
      }
      await refreshCatalogs(client);

      if (selected?.catalogId !== catalogId) return;
      const info = (await client.catalogs()).catalogs.find((c) => c.catalogId === catalogId);
      if (!info) return;
      const found = await resolveSurvivingFolder(
        { catalogId, entryIndex: 0 },
        info.rootPath,
        selPath,
        (ref) => client.children(ref, { foldersOnly: true, sort: "name" }),
      );
      setExpandTo(await refsToPath(client, found.ref));
      await selectNode(found.ref, found.fullPath);
    },
    [refreshCatalogs, selectNode],
  );

  const rescanCatalog = useCallback(
    async (catalogId: number) => {
      const client = clientRef.current!;
      if (isRescanning(latest.current.rescans, catalogId)) return;
      const before = latest.current.catalogs?.catalogs.find((c) => c.catalogId === catalogId);
      const label = before?.rootPath ?? `catalog ${catalogId}`;
      const abort = new AbortController();
      rescanAborts.current.set(catalogId, abort);
      // Update the ref too: a second click in the same tick must see it as running.
      latest.current.rescans = beginRescan(latest.current.rescans, catalogId, label);
      setRescans((s) => beginRescan(s, catalogId, label));
      try {
        // Phase 1: the rescan's own outcome decides the success / failure toast.
        let finished = false;
        let entries = 0;
        try {
          const result = await client.rescan(catalogId, {
            signal: abort.signal,
            onProgress: (p) =>
              setRescans((s) => updateRescan(s, catalogId, { phase: p.phase, entriesScanned: p.entriesScanned })),
          });
          finished = result !== null;
          entries = result?.entries ?? 0;
          if (!finished) {
            // Cancelled. If the cancel lost the race with the swap the catalog did change: detect that.
            try {
              const now = (await client.catalogs()).catalogs.find((c) => c.catalogId === catalogId);
              if (now && before && now.scanEndUtcTicks !== before.scanEndUtcTicks) {
                finished = true;
                entries = now.dirEntryCount + now.fileEntryCount;
              }
            } catch {
              /* could not check: report it as cancelled */
            }
          }
        } catch (e) {
          if (e instanceof RescanFailure && e.rootNotAccessible) {
            setUnreachableRoot(e.error.rootPath || label);
          } else {
            notifications.show({
              color: "red",
              title: "Rescan failed",
              message: `${label}: ${e instanceof Error ? e.message : String(e)}. The catalog was left unchanged.`,
            });
          }
          return;
        }

        if (!finished) {
          notifications.show({ color: "gray", title: "Rescan cancelled", message: `${label} was left unchanged.` });
          return;
        }

        // Phase 2: the swap succeeded. A failed view refresh must not be reported as a failed rescan.
        try {
          await afterRescan(client, catalogId);
          notifications.show({
            color: "green",
            title: "Rescan complete",
            message: `Rescanned ${label} — ${entries.toLocaleString()} entries`,
          });
        } catch (e) {
          notifications.show({
            color: "orange",
            title: "Rescan complete, view not refreshed",
            message: `Rescanned ${label}, but refreshing the view failed: ${
              e instanceof Error ? e.message : String(e)
            } — use Reload.`,
          });
        }
      } finally {
        rescanAborts.current.delete(catalogId);
        latest.current.rescans = endRescan(latest.current.rescans, catalogId);
        setRescans((s) => endRescan(s, catalogId));
      }
    },
    [afterRescan],
  );

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

  // Start (File menu / Catalogs tab) and "Change…" in the dialog: both just (re)pick the root.
  const pickNewRoot = useCallback(async () => {
    const picked = await pickNewCatalogRoot();
    if (picked) setNewCatalogRoot(picked);
  }, [pickNewCatalogRoot]);

  // The toast's [Open]: jump to the new root in the Directory tab (only now, never automatically).
  const openNewRoot = useCallback(
    async (result: CreateResult) => {
      const ref: EntryRef = { catalogId: result.catalogId, entryIndex: 0 };
      setView("directory"); // synchronous: this is the user's navigation intent
      setExpandTo([]);
      await selectNode(ref, result.rootPath);
      setRevealKey(refToString(ref));
    },
    [selectNode],
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
                ? `Created ${form.fileName}.cdex — ${done.entries.toLocaleString()} entries. `
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
    (job.kind === "rescan" ? rescanAborts.current : createAborts.current).get(job.id)?.abort();
  }, []);

  // --- shell actions / context menu ---
  const runShell = useCallback(async (ref: EntryRef, action: ShellAction, commandId?: number) => {
    try {
      await clientRef.current!.shell({ ref: refToString(ref), action, commandId });
    } catch {
      // Mirrors cdeWin: shell actions are no-ops when the entry isn't on this filesystem.
    }
  }, []);

  const showTreemap = useCallback((ref: EntryRef) => {
    setTreemapRequest((prev) => ({ ref, nonce: (prev?.nonce ?? 0) + 1 }));
    setView("treemap");
  }, []);

  // The "Rescan" menu entry for a catalog: disabled (with a tooltip) while that catalog is rescanning.
  const rescanItem = useCallback(
    (catalogId: number): MenuItem => {
      const m = rescanMenuItem(rescans, catalogId);
      const root = catalogs?.catalogs.find((c) => c.catalogId === catalogId)?.rootPath;
      return {
        label: m.label,
        disabled: m.disabled,
        title: m.title ?? (root ? `Rescan ${root} in the background` : undefined),
        onClick: () => void rescanCatalog(catalogId),
      };
    },
    [rescans, catalogs, rescanCatalog],
  );

  const buildMenu = useCallback(
    (ref: EntryRef, fullPath: string, onViewInTree?: () => void, isDirectory = false): MenuItem[] => {
      const items: MenuItem[] = [
        { label: "Open", onClick: () => void runShell(ref, "open") },
        { label: "Explore", onClick: () => void runShell(ref, "explore") },
        { label: "Properties", onClick: () => void runShell(ref, "properties") },
      ];
      for (const c of commands) {
        items.push({ label: c.label, onClick: () => void runShell(ref, "custom", c.id) });
      }
      items.push({ label: "", onClick: () => {}, separator: true });
      if (isDirectory) items.push({ label: "Show TreeMap", onClick: () => showTreemap(ref) });
      items.push({
        label: "Copy Full Path",
        onClick: () => void navigator.clipboard.writeText(fullPath),
      });
      if (onViewInTree) items.push({ label: "View in Tree", onClick: onViewInTree });
      // A catalog root or any folder rescans its containing catalog.
      if (isDirectory) items.push(rescanItem(ref.catalogId));
      return items;
    },
    [commands, runShell, showTreemap, rescanItem],
  );

  // Right-click on blank tree space: map the current selection (greyed out when there is none).
  // Handlers for the memoised DirectoryPane must keep a stable identity, or every App render (e.g. each
  // streamed search flush) would re-render the hidden tree + listing. They read the latest
  // buildMenu / selection through refs at event time instead of closing over them.
  const buildMenuRef = useRef(buildMenu);
  buildMenuRef.current = buildMenu;
  const selectedKeyRef = useRef(selectedKey);
  selectedKeyRef.current = selectedKey;

  const treeBackgroundMenu = useCallback(
    (x: number, y: number) => {
      const key = selectedKeyRef.current;
      const selected = key ? parseRef(key) : null;
      setMenu({
        x,
        y,
        items: [{ label: "Show TreeMap", disabled: !selected, onClick: () => selected && showTreemap(selected) }],
      });
    },
    [showTreemap],
  );
  const onTreeSelect = useCallback(
    (ref: EntryRef, fp: string) => {
      setRevealKey(null); // manual click: don't centre, just keep it visible
      void selectNode(ref, fp);
    },
    [selectNode],
  );
  const onTreeContextMenu = useCallback(
    (ref: EntryRef, fp: string, x: number, y: number) =>
      setMenu({ x, y, items: buildMenuRef.current(ref, fp, undefined, true) }),
    [],
  );
  const onListContextMenu = useCallback(
    (n: DirectoryNode, x: number, y: number) =>
      setMenu({ x, y, items: buildMenuRef.current(n.ref, n.fullPath, undefined, n.isDirectory) }),
    [],
  );
  const onDirSort = useCallback((column: SortColumn) => setDirSort((cur) => nextSort(cur, column)), []);
  // Initial split width only (SplitPane owns it afterwards). A plain number, so it is a stable prop.
  const treePaneWidth = (uiRef.current?.get().treePaneWidth as number) ?? 320;

  const onTreemapTileMenu = useCallback(
    (row: TreemapRow, fullPath: string, x: number, y: number) => {
      const ref = row.ref!;
      setMenu({ x, y, items: buildMenu(ref, fullPath, () => void viewInTree(ref), row.isDirectory) });
    },
    [buildMenu, viewInTree],
  );

  if (!ready) {
    return <div className="boot">{status || "Loading…"}</div>;
  }

  const loadBlockedReasonText = loadBlockedReason(rescans, creates);
  const staleBanner = staleResultsBanner(results, rescannedSinceSearch, (id) => {
    const c = catalogs?.catalogs.find((x) => x.catalogId === id);
    return c ? c.rootPath : `#${id}`;
  });

  return (
    <div className="app">
      <MenuBar
        onNewCatalog={() => void pickNewRoot()}
        onOpenFolder={openFolder}
        onReload={() => void reloadCatalogs(catalogDir)}
        onExit={() => void getCurrentWindow().close()}
        onAbout={() => setAboutOpen(true)}
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
      />

      <div className="tabs">
        <button className={view === "directory" ? "active" : ""} onClick={() => setView("directory")}>
          Directory
        </button>
        <button className={view === "results" ? "active" : ""} onClick={() => setView("results")}>
          Search Results
        </button>
        <button className={view === "catalogs" ? "active" : ""} onClick={() => setView("catalogs")}>
          Catalogs
        </button>
        <button className={view === "colours" ? "active" : ""} onClick={() => void openColours()}>
          Colours
        </button>
        {treemapRequest && (
          <button className={view === "treemap" ? "active" : ""} onClick={() => setView("treemap")}>
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
        <div className={viewPaneClass<View>("directory", view)} aria-hidden={view !== "directory"}>
          <DirectoryPane
            client={clientRef.current!}
            roots={roots}
            selectedKey={selectedKey}
            revealKey={revealKey}
            revealNonce={revealNonce}
            expandTo={expandTo}
            dirPath={dirPath}
            dirNodes={dirNodes}
            sort={dirSort}
            initialLeftWidth={treePaneWidth}
            resolveTreeStyle={resolveTreeStyle}
            resolveStyle={resolveStyle}
            onResizeEnd={onTreePaneResize}
            onSelect={onTreeSelect}
            onTreeContextMenu={onTreeContextMenu}
            onBackgroundContextMenu={treeBackgroundMenu}
            onSort={onDirSort}
            onActivate={activateDir}
            onListContextMenu={onListContextMenu}
          />
        </div>

        {view === "results" && (
          <div className="results-pane">
            {staleBanner && (
              <div className="stale-banner" role="status">
                <span>{staleBanner}</span>
                <button
                  disabled={busy}
                  onClick={() => {
                    const q = lastQueryRef.current;
                    if (q) void runSearch(q);
                  }}
                >
                  Search again
                </button>
              </div>
            )}
            <div className="results-fill">
              <ResultsList
                rows={results}
                resolveStyle={resolveStyle}
                columnOrder={uiRef.current?.get().searchResultColumnOrder}
                onColumnOrderChange={(searchResultColumnOrder) => uiRef.current?.update({ searchResultColumnOrder })}
                onActivate={(row) => void viewInTree(row.ref, row.fullPath)}
                onContextMenu={(row, x, y) =>
                  setMenu({
                    x,
                    y,
                    items: buildMenu(row.ref, row.fullPath, () => void viewInTree(row.ref, row.fullPath), row.isDirectory),
                  })
                }
              />
            </div>
          </div>
        )}

        {view === "catalogs" && (
          <CatalogList
            catalogs={catalogs?.catalogs ?? []}
            rescanningIds={new Set(rescans.keys())}
            onNewCatalog={() => void pickNewRoot()}
            onContextMenu={(c, x, y) => setMenu({ x, y, items: [rescanItem(c.catalogId)] })}
            onActivate={(c: CatalogInfo) => {
              setView("directory"); // synchronous navigation intent before the async listing load
              void selectNode({ catalogId: c.catalogId, entryIndex: 0 }, c.rootPath);
            }}
          />
        )}

        {view === "colours" && (
          <ColoursView
            text={rulesText}
            onChange={setRulesText}
            save={(text) => rulesSaverRef.current?.save(text)}
            saveError={rulesSaveError}
            getDefaults={() => clientRef.current!.getColourRuleDefaults()}
          />
        )}

        {view === "treemap" && treemapRequest && (
          <TreemapView
            client={clientRef.current!}
            request={treemapRequest}
            depth={treemapDepth ?? (uiRef.current?.get().treemapDepth as number | undefined) ?? DEFAULT_TREEMAP_DEPTH}
            onDepthChange={(d) => {
              setTreemapDepth(d);
              uiRef.current?.update({ treemapDepth: d });
            }}
            onStatus={setStatus}
            onTileMenu={onTreemapTileMenu}
          />
        )}
      </div>

      <StatusBar
        catalogs={catalogs}
        resultCount={results.length}
        searchTiming={status || timing}
        jobs={jobStatusLines(rescans, creates)}
        onCancelJob={cancelJob}
      />
      <ContextMenu menu={menu} onClose={() => setMenu(null)} />
      <RootUnreachableModal rootPath={unreachableRoot} onClose={() => setUnreachableRoot(null)} />
      <AboutModal opened={aboutOpen} onClose={() => setAboutOpen(false)} />
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

// Inverse of refToString ("catalog-index").
function parseRef(key: string): EntryRef | null {
  const [catalogId, entryIndex] = key.split("-").map(Number);
  return Number.isInteger(catalogId) && Number.isInteger(entryIndex) ? { catalogId, entryIndex } : null;
}

// Root -> entry ref chain (for tree auto-expand).
async function refsToPath(client: CdeApiClient, ref: EntryRef): Promise<EntryRef[]> {
  const chain = await client.path(ref);
  return chain.map((n) => n.ref);
}
