import { useCallback, useRef, useState } from "react";
import { notifications } from "@mantine/notifications";
import type { CdeApiClient } from "../api/client";
import { refToString, type DirectoryNode, type EntryRef } from "../api/types";
import { resolveSurvivingFolder, rowStillResolves } from "../rescan";
import { createRequestGuard } from "../requestGuard";

export interface UseCatalogSelectionOptions {
  getClient: () => CdeApiClient;
  /** Switch to the Directory view. Called synchronously on a user's navigation intent, before any async load. */
  showDirectory: () => void;
}

/** The selection captured before a rescan, so it can be restored once the new catalog is in. */
export interface SelectionSnapshot {
  selectedKey: string | null;
  dirPath: string;
}

// Inverse of refToString ("catalog-index").
export function parseRef(key: string): EntryRef | null {
  const [catalogId, entryIndex] = key.split("-").map(Number);
  return Number.isInteger(catalogId) && Number.isInteger(entryIndex) ? { catalogId, entryIndex } : null;
}

/**
 * The root-to-folder chain for the folder at `fullPath`, in the catalog as it is now. `ref` is tried
 * first, but a ref from before a rescan can name another entry or none, so the folder is then found
 * by its path: its nearest surviving parent when it is gone (`exact` false). Empty when the catalog
 * is no longer loaded.
 */
export async function folderChain(
  client: CdeApiClient,
  ref: EntryRef,
  fullPath: string,
): Promise<{ chain: DirectoryNode[]; exact: boolean }> {
  const chain = await client.path(ref).catch((): DirectoryNode[] => []);
  if (rowStillResolves(fullPath, chain)) return { chain, exact: true };
  const info = (await client.catalogs()).catalogs.find((c) => c.catalogId === ref.catalogId);
  if (!info) return { chain: [], exact: false };
  const found = await resolveSurvivingFolder(
    { catalogId: ref.catalogId, entryIndex: 0 },
    info.rootPath,
    fullPath,
    (r) => client.children(r, { foldersOnly: true, sort: "name" }),
  );
  return { chain: await client.path(found.ref), exact: found.exact };
}

/** The Directory view's selection: selected node, its listing, tree reveal/expand, and restoring it after a rescan. */
export function useCatalogSelection({ getClient, showDirectory }: UseCatalogSelectionOptions) {
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [revealKey, setRevealKey] = useState<string | null>(null);
  // Bumped per View-in-Tree, so revealing the already-selected node again re-centres it (the tree stays mounted).
  const [revealNonce, setRevealNonce] = useState(0);
  const [expandTo, setExpandTo] = useState<EntryRef[] | undefined>(undefined);
  const [dirPath, setDirPath] = useState("");
  const [dirNodes, setDirNodes] = useState<DirectoryNode[]>([]);
  // Last-navigation-wins: drops a children/path response that a newer selection or a reload overtook.
  const [navGuard] = useState(createRequestGuard); // useState, not useMemo: the guard's token state must never be discarded
  // Latest values for handlers with a stable identity and for async completions.
  const selectedKeyRef = useRef(selectedKey);
  selectedKeyRef.current = selectedKey;
  const dirPathRef = useRef(dirPath);
  dirPathRef.current = dirPath;

  // Update the directory selection + listing. Deliberately does NOT change the view: the view is
  // switched synchronously by whichever handler initiated the navigation, so a late-resolving
  // listing fetch here can never clobber a newer tab click.
  // Returns false when the response was dropped (a newer selection or a reload came first) or the
  // listing failed (reported in a toast, with the old folder's rows cleared).
  const selectNode = useCallback(
    async (ref: EntryRef, fullPath: string): Promise<boolean> => {
      const client = getClient();
      const token = navGuard.begin();
      setSelectedKey(refToString(ref));
      setDirPath(fullPath);
      let nodes: DirectoryNode[];
      try {
        nodes = await client.children(ref, { sort: "name", hasChildren: false });
      } catch (e) {
        if (navGuard.isCurrent(token)) {
          setDirNodes([]);
          notifications.show({ color: "red", title: "Could not open the folder", message: (e as Error).message });
        }
        return false;
      }
      if (!navGuard.isCurrent(token)) return false;
      setDirNodes(nodes);
      return true;
    },
    [getClient, navGuard],
  );

  // Open the folder a listing row names, or its parent, and expand the tree to it. False when a
  // newer navigation won or the sidecar call failed (reported in a toast).
  const openFolder = useCallback(
    async (ref: EntryRef, fullPath: string, target: "folder" | "parent"): Promise<boolean> => {
      const token = navGuard.begin();
      try {
        const { chain, exact } = await folderChain(getClient(), ref, fullPath);
        if (!navGuard.isCurrent(token)) return false;
        // A folder the rescan removed resolves to its nearest surviving parent, which is already "up".
        const path = target === "parent" && exact ? chain.slice(0, -1) : chain;
        if (path.length === 0) return false;
        if (!exact && target === "folder") {
          notifications.show({
            color: "yellow",
            title: "Not found",
            message: `${fullPath} is no longer in the catalog (it may have been rescanned).`,
          });
        }
        const folder = path[path.length - 1];
        setExpandTo(path.map((n) => n.ref));
        return await selectNode(folder.ref, folder.fullPath);
      } catch (e) {
        if (navGuard.isCurrent(token)) {
          notifications.show({ color: "red", title: "Could not open the folder", message: (e as Error).message });
        }
        return false;
      }
    },
    [getClient, selectNode, navGuard],
  );

  // drill into a directory row (already in the directory view)
  const activateDir = useCallback(
    async (node: DirectoryNode) => {
      if (node.isDirectory) await openFolder(node.ref, node.fullPath, "folder");
    },
    [openFolder],
  );

  // The listing's ".." row: open the parent of the folder on screen.
  const goUp = useCallback(async (): Promise<boolean> => {
    const ref = selectedKeyRef.current ? parseRef(selectedKeyRef.current) : null;
    return ref ? openFolder(ref, dirPathRef.current, "parent") : false;
  }, [openFolder]);

  // view a search result in the directory tree (expand to its parent)
  const viewInTree = useCallback(
    async (ref: EntryRef, expectedPath?: string) => {
      showDirectory(); // synchronous: this is the user's navigation intent
      const client = getClient();
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
    [getClient, showDirectory, selectNode, navGuard],
  );

  // The toast's [Open]: jump to a catalog root (only now, never automatically).
  const openRoot = useCallback(
    async (ref: EntryRef, rootPath: string) => {
      showDirectory(); // synchronous: this is the user's navigation intent
      setExpandTo([]);
      await selectNode(ref, rootPath);
      setRevealKey(refToString(ref));
    },
    [showDirectory, selectNode],
  );

  // A manual tree click: don't centre, just keep it visible.
  const selectFromTree = useCallback(
    (ref: EntryRef, fullPath: string) => {
      setRevealKey(null);
      void selectNode(ref, fullPath);
    },
    [selectNode],
  );

  // Before a reload: refs and expansion from the old load are not trusted, and in-flight navigation
  // responses must not land after this.
  const resetForReload = useCallback(() => {
    navGuard.invalidate();
    setSelectedKey(null);
    setRevealKey(null);
    setExpandTo(undefined);
    setDirPath("");
    setDirNodes([]);
  }, [navGuard]);

  const snapshot = useCallback(
    (): SelectionSnapshot => ({ selectedKey: selectedKeyRef.current, dirPath: dirPathRef.current }),
    [],
  );

  // After a rescan swapped `catalogId` in: put the selection back, on the nearest surviving parent if its folder is gone.
  const restoreAfterRescan = useCallback(
    async (client: CdeApiClient, catalogId: number, before: SelectionSnapshot) => {
      const selected = before.selectedKey ? parseRef(before.selectedKey) : null;
      if (selected?.catalogId !== catalogId) return;
      // A click made while this restore is awaiting begins a newer token, which makes the restore stand down.
      const token = navGuard.begin();
      const { chain } = await folderChain(client, selected, before.dirPath);
      if (!navGuard.isCurrent(token) || chain.length === 0) return;
      const found = chain[chain.length - 1];
      setExpandTo(chain.map((n) => n.ref));
      await selectNode(found.ref, found.fullPath);
    },
    [selectNode, navGuard],
  );

  return {
    selectedKey,
    selectedKeyRef,
    revealKey,
    revealNonce,
    expandTo,
    dirPath,
    dirNodes,
    selectNode,
    activateDir,
    // Entry 0 is a catalog's root, which has no parent.
    canGoUp: (parseRef(selectedKey ?? "")?.entryIndex ?? 0) > 0,
    goUp,
    viewInTree,
    openRoot,
    selectFromTree,
    resetForReload,
    snapshot,
    restoreAfterRescan,
  };
}
