import { useCallback, useRef, useState } from "react";
import { open } from "@tauri-apps/plugin-dialog";
import type { CdeApiClient } from "../api/client";
import type { CatalogsResponse } from "../api/types";
import type { TreeRoot } from "../components/CatalogTree";
import { catalogCreated } from "../catalogColumns";
import { catalogFile, catalogName } from "../format";
import type { UiStateManager } from "../state/uiState";

export interface UseCatalogsOptions {
  getClient: () => CdeApiClient;
  getUi: () => UiStateManager | null;
  setStatus: (text: string) => void;
  /** True while catalog jobs block a load; Reload and Open Folder then do nothing. */
  isBlocked: () => boolean;
  /** Runs before every reload: refs and expansion from the old load are no longer trusted. */
  onBeforeReload: () => void;
  /** Runs with the catalog list after each successful load. */
  onLoaded: (catalogs: CatalogsResponse) => void;
}

/** The loaded catalogs, their tree roots and the working folder, and loading them through the sidecar. */
export function useCatalogs({ getClient, getUi, setStatus, isBlocked, onBeforeReload, onLoaded }: UseCatalogsOptions) {
  const [catalogs, setCatalogs] = useState<CatalogsResponse | null>(null);
  const [roots, setRoots] = useState<TreeRoot[]>([]);
  const [catalogDir, setCatalogDir] = useState<string | null>(null);
  // Per-catalog counter that remounts a tree root after a rescan (drops its cached children).
  const treeGenRef = useRef<Record<number, number>>({});
  // Bumped on every reload: the Directory view stays mounted across tab switches (#80), so a reload
  // must drop the tree's cached expansion + children itself.
  const reloadEpochRef = useRef(0);

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
        scannedAt: catalogCreated(c),
        generation: (treeGenRef.current[c.catalogId] ?? 0) + reloadEpochRef.current,
      })),
    );
    return cat;
  }, []);

  // The first load: the last-used catalog folder, or the sidecar's default folder. The sidecar loads nothing
  // on its own, so its handshake never waits on catalogs (a folder of legacy .cde files is slow).
  const loadInitial = useCallback(
    async (savedDir: string | undefined) => {
      const client = getClient();
      if (savedDir) setCatalogDir(savedDir);
      setStatus("Loading catalogs…");
      await client.reload({ onProgress: (p) => setStatus(p.message) }, savedDir);
      onLoaded(await refreshCatalogs(client));
    },
    [getClient, setStatus, onLoaded, refreshCatalogs],
  );

  const reload = useCallback(
    async (dir?: string | null) => {
      if (isBlocked()) return;
      const client = getClient();
      setStatus("Loading catalogs…");
      // Reset BEFORE the call, so it also happens when the reload fails part-way (the sidecar may
      // already have dropped the old catalogs), and in-flight navigation responses cannot land after it.
      onBeforeReload();
      reloadEpochRef.current += 1;
      try {
        await client.reload({}, dir ?? undefined);
        onLoaded(await refreshCatalogs(client));
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
    [getClient, setStatus, isBlocked, onBeforeReload, onLoaded, refreshCatalogs],
  );

  // File ▸ Open Folder…: native folder picker, then load the catalogs in it. True once a folder was loaded.
  const openFolder = useCallback(async (): Promise<boolean> => {
    if (isBlocked()) return false;
    const picked = await open({ directory: true, multiple: false, title: "Select catalog folder" });
    if (typeof picked !== "string") return false;
    setCatalogDir(picked);
    getUi()?.update({ catalogDir: picked });
    await reload(picked);
    return true;
  }, [isBlocked, getUi, reload]);

  const saveDescription = useCallback(
    async (catalogId: number, description: string) => {
      const client = getClient();
      await client.setDescription(catalogId, description);
      await refreshCatalogs(client);
    },
    [getClient, refreshCatalogs],
  );

  // After a rescan swapped this catalog's file: its tree root remounts on the next refresh.
  const remountRoot = useCallback((catalogId: number) => {
    treeGenRef.current[catalogId] = (treeGenRef.current[catalogId] ?? 0) + 1;
  }, []);

  return { catalogs, roots, catalogDir, refreshCatalogs, loadInitial, reload, openFolder, saveDescription, remountRoot };
}
