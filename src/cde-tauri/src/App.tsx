import { useCallback, useEffect, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
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
  const [expandTo, setExpandTo] = useState<EntryRef[] | undefined>(undefined);
  const [dirPath, setDirPath] = useState("");
  const [dirNodes, setDirNodes] = useState<DirectoryNode[]>([]);

  const [results, setResults] = useState<SearchResultRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [timing, setTiming] = useState("");
  const [menu, setMenu] = useState<MenuState | null>(null);

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

        await uiRef.current.load();
        setCommands(await client.shellCommands());
        await refreshCatalogs(client);
        setReady(true);
        setStatus("");
      } catch (e) {
        setStatus(`Failed to start: ${(e as Error).message}`);
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
      try {
        await uiRef.current?.flush();
      } catch {
        /* ignore */
      }
      await sidecarRef.current?.kill();
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

  // --- tree selection -> directory listing ---
  const selectNode = useCallback(async (ref: EntryRef, fullPath: string) => {
    const client = clientRef.current!;
    setSelectedKey(refToString(ref));
    setDirPath(fullPath);
    setDirNodes(await client.children(ref, { sort: "name" }));
    setView("directory");
  }, []);

  // drill into a directory row
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
      const client = clientRef.current!;
      const chain = await client.path(ref);
      const refs = chain.map((n) => n.ref);
      // expand to the parent (drop the entry itself), then select the parent
      const parent = refs.length > 1 ? refs[refs.length - 2] : refs[0];
      setExpandTo(refs.slice(0, -1));
      const parentNode = chain[chain.length - 2] ?? chain[chain.length - 1];
      await selectNode(parent, parentNode.fullPath);
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
        <button
          className="reload"
          onClick={async () => {
            setStatus("Reloading…");
            await clientRef.current!.reload({});
            await refreshCatalogs(clientRef.current!);
            setStatus("");
          }}
        >
          Reload
        </button>
      </div>

      <div className="main">
        {view === "directory" && (
          <div className="split">
            <div className="pane tree-pane">
              <CatalogTree
                client={clientRef.current!}
                roots={roots}
                selectedKey={selectedKey}
                expandTo={expandTo}
                onSelect={selectNode}
              />
            </div>
            <div className="pane list-pane">
              <DirectoryList
                path={dirPath}
                nodes={dirNodes}
                onActivate={activateDir}
                onContextMenu={(n, x, y) =>
                  setMenu({ x, y, items: buildMenu(n.ref, n.fullPath) })
                }
              />
            </div>
          </div>
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
            onActivate={(c: CatalogInfo) =>
              void selectNode({ catalogId: c.catalogId, entryIndex: 0 }, c.rootPath)
            }
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
