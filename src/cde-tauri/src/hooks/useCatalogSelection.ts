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

// Root -> entry ref chain (for tree auto-expand).
export async function refsToPath(client: CdeApiClient, ref: EntryRef): Promise<EntryRef[]> {
  const chain = await client.path(ref);
  return chain.map((n) => n.ref);
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
  // Returns false when the response was dropped (a newer selection or a reload came first).
  const selectNode = useCallback(
    async (ref: EntryRef, fullPath: string): Promise<boolean> => {
      const client = getClient();
      const token = navGuard.begin();
      setSelectedKey(refToString(ref));
      setDirPath(fullPath);
      const nodes = await client.children(ref, { sort: "name", hasChildren: false });
      if (!navGuard.isCurrent(token)) return false;
      setDirNodes(nodes);
      return true;
    },
    [getClient, navGuard],
  );

  // drill into a directory row (already in the directory view)
  const activateDir = useCallback(
    async (node: DirectoryNode) => {
      if (!node.isDirectory) return;
      const token = navGuard.begin();
      const path = await refsToPath(getClient(), node.ref);
      if (!navGuard.isCurrent(token)) return;
      setExpandTo(path);
      await selectNode(node.ref, node.fullPath);
    },
    [getClient, selectNode, navGuard],
  );

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
      const info = (await client.catalogs()).catalogs.find((c) => c.catalogId === catalogId);
      if (!info) return;
      const found = await resolveSurvivingFolder(
        { catalogId, entryIndex: 0 },
        info.rootPath,
        before.dirPath,
        (ref) => client.children(ref, { foldersOnly: true, sort: "name" }),
      );
      const expand = await refsToPath(client, found.ref);
      if (!navGuard.isCurrent(token)) return;
      setExpandTo(expand);
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
    viewInTree,
    openRoot,
    selectFromTree,
    resetForReload,
    snapshot,
    restoreAfterRescan,
  };
}
