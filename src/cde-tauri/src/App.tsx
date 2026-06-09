import { useCallback, useEffect, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { open } from "@tauri-apps/plugin-dialog";
import { CdeApiClient } from "./api/client";
import {
  CatalogInfo,
  CatalogsResponse,
  DirectoryNode,
  EntryRef,
  refToString,
  SearchQuery,
  SearchResultRow,
  ShellAction,
} from "./api/types";
import { startSidecar, Sidecar } from "./sidecar";
import { UiStateManager } from "./state/uiState";
import { CatalogTree, TreeRoot } from "./components/CatalogTree";
import { DirectoryList } from "./components/DirectoryList";
import { ResultsList } from "./components/ResultsList";
import { CatalogList } from "./components/CatalogList";
import { SearchBar } from "./components/SearchBar";
import { StatusBar } from "./components/StatusBar";
import { MenuBar } from "./components/MenuBar";
import { SplitPane } from "./components/SplitPane";
import { ContextMenu, MenuItem, MenuState } from "./components/ContextMenu";

type View = "directory" | "results" | "catalogs";

export default function App() {
  const clientRef = useRef<CdeApiClient | null>(null);
  const sidecarRef = useRef<Sidecar | null>(null);
  const uiRef = useRef<UiStateManager | null>(null);
  const searchAbort = useRef<AbortController | null>(null);

  const [status, setStatus] = useState("Starting…");
  const [ready, setReady] = useState(false);
  const [view, setView] = useState<View>("directory");
  const [catalogs, setCatalogs] = useState<CatalogsResponse | null>(null);
  const [roots, setRoots] = useState<TreeRoot[]>([]);
  const [commands, setCommands] = useState<{ id: number; label: string }[]>([]);

  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [revealKey, setRevealKey] = useState<string | null>(null);
  const [expandTo, setExpandTo] = useState<EntryRef[] | undefined>(undefined);
  const [dirPath, setDirPath] = useState("");
  const [dirNodes, setDirNodes] = useState<DirectoryNode[]>([]);

  const [results, setResults] = useState<SearchResultRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [timing, setTiming] = useState("");
  const [menu, setMenu] = useState<MenuState | null>(null);
  const [catalogDir, setCatalogDir] = useState<string | null>(null);

  // --- bootstrap: spawn sidecar, build client, load catalogs + ui-state ---
  useEffect(() => {
    let disposed = false;
    (async () => {
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
        setCommands(await client.shellCommands());
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
      })),
    );
  }, []);

  const reloadCatalogs = useCallback(
    async (dir?: string | null) => {
      const client = clientRef.current!;
      setStatus("Loading catalogs…");
      try {
        await client.reload({}, dir ?? undefined);
        await refreshCatalogs(client);
        setStatus("");
      } catch (e) {
        setStatus(`Reload failed: ${(e as Error).message}`);
      }
    },
    [refreshCatalogs],
  );

  const onTreePaneResize = useCallback((width: number) => {
    uiRef.current?.update({ treePaneWidth: Math.round(width) });
  }, []);

  // File ▸ Open Folder… — native folder picker, then load .cdex catalogs from it.
  const openFolder = useCallback(async () => {
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
  const selectNode = useCallback(async (ref: EntryRef, fullPath: string) => {
    const client = clientRef.current!;
    setSelectedKey(refToString(ref));
    setDirPath(fullPath);
    setDirNodes(await client.children(ref, { sort: "name" }));
  }, []);

  // drill into a directory row (already in the directory view)
  const activateDir = useCallback(
    async (node: DirectoryNode) => {
      if (!node.isDirectory) return;
      setExpandTo(await refsToPath(clientRef.current!, node.ref));
      await selectNode(node.ref, node.fullPath);
    },
    [selectNode],
  );

  // view a search result in the directory tree (expand to its parent)
  const viewInTree = useCallback(
    async (ref: EntryRef) => {
      setView("directory"); // synchronous: this is the user's navigation intent
      const client = clientRef.current!;
      const chain = await client.path(ref);
      const refs = chain.map((n) => n.ref);
      // expand to the parent (drop the entry itself), then select the parent
      const parent = refs.length > 1 ? refs[refs.length - 2] : refs[0];
      setExpandTo(refs.slice(0, -1));
      const parentNode = chain[chain.length - 2] ?? chain[chain.length - 1];
      await selectNode(parent, parentNode.fullPath);
      setRevealKey(refToString(parent)); // centre this node in the tree (it's the reveal target)
    },
    [selectNode],
  );

  // --- search ---
  const runSearch = useCallback(async (query: SearchQuery) => {
    const client = clientRef.current!;
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

  // --- shell actions / context menu ---
  const runShell = useCallback(async (ref: EntryRef, action: ShellAction, commandId?: number) => {
    try {
      await clientRef.current!.shell({ ref: refToString(ref), action, commandId });
    } catch {
      // Mirrors cdeWin: shell actions are no-ops when the entry isn't on this filesystem.
    }
  }, []);

  const buildMenu = useCallback(
    (ref: EntryRef, fullPath: string, onViewInTree?: () => void): MenuItem[] => {
      const items: MenuItem[] = [
        { label: "Open", onClick: () => void runShell(ref, "open") },
        { label: "Explore", onClick: () => void runShell(ref, "explore") },
        { label: "Properties", onClick: () => void runShell(ref, "properties") },
      ];
      for (const c of commands) {
        items.push({ label: c.label, onClick: () => void runShell(ref, "custom", c.id) });
      }
      items.push({ label: "", onClick: () => {}, separator: true });
      items.push({
        label: "Copy Full Path",
        onClick: () => void navigator.clipboard.writeText(fullPath),
      });
      if (onViewInTree) items.push({ label: "View in Tree", onClick: onViewInTree });
      return items;
    },
    [commands, runShell],
  );

  if (!ready) {
    return <div className="boot">{status || "Loading…"}</div>;
  }

  return (
    <div className="app">
      <MenuBar onOpenFolder={openFolder} onReload={() => void reloadCatalogs(catalogDir)} />

      <SearchBar
        busy={busy}
        history={(uiRef.current?.get().searchHistory as string[]) ?? []}
        defaults={uiRef.current?.get() ?? {}}
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
        <button className="reload" onClick={() => void reloadCatalogs(catalogDir)}>
          Reload
        </button>
      </div>

      <div className="main">
        {view === "directory" && (
          <SplitPane
            initialLeftWidth={(uiRef.current?.get().treePaneWidth as number) ?? 320}
            onResizeEnd={onTreePaneResize}
            left={
              <CatalogTree
                client={clientRef.current!}
                roots={roots}
                selectedKey={selectedKey}
                revealKey={revealKey}
                expandTo={expandTo}
                onSelect={(ref, fp) => {
                  setRevealKey(null); // manual click: don't centre, just keep it visible
                  void selectNode(ref, fp);
                }}
              />
            }
            right={
              <DirectoryList
                path={dirPath}
                nodes={dirNodes}
                onActivate={activateDir}
                onContextMenu={(n, x, y) =>
                  setMenu({ x, y, items: buildMenu(n.ref, n.fullPath) })
                }
              />
            }
          />
        )}

        {view === "results" && (
          <ResultsList
            rows={results}
            onActivate={(row) => void viewInTree(row.ref)}
            onContextMenu={(row, x, y) =>
              setMenu({
                x,
                y,
                items: buildMenu(row.ref, row.fullPath, () => void viewInTree(row.ref)),
              })
            }
          />
        )}

        {view === "catalogs" && (
          <CatalogList
            catalogs={catalogs?.catalogs ?? []}
            onActivate={(c: CatalogInfo) => {
              setView("directory"); // synchronous navigation intent before the async listing load
              void selectNode({ catalogId: c.catalogId, entryIndex: 0 }, c.rootPath);
            }}
          />
        )}
      </div>

      <StatusBar catalogs={catalogs} resultCount={results.length} searchTiming={status || timing} />
      <ContextMenu menu={menu} onClose={() => setMenu(null)} />
    </div>
  );
}

// Root -> entry ref chain (for tree auto-expand).
async function refsToPath(client: CdeApiClient, ref: EntryRef): Promise<EntryRef[]> {
  const chain = await client.path(ref);
  return chain.map((n) => n.ref);
}
