import { useCallback, useMemo, useRef, useState, type RefObject } from "react";
import type { CatalogInfo, DirectoryNode, EntryRef, SearchResultRow, ShellAction, TreemapRow } from "../api/types";
import type { MenuState } from "../components/ContextMenu";
import { catalogFile, fileBaseName } from "../format";
import type { JobsModel } from "../jobsModel";
import {
  catalogMenu,
  collapseAllItem,
  entryMenu,
  listMenu,
  rescanItem,
  treeBackgroundMenu,
  treeNodeMenu,
  type EntryMenuContext,
  type ShellCommand,
} from "../menus";
import { parseRef } from "./useCatalogSelection";

export interface UseContextMenusOptions {
  commands: readonly ShellCommand[];
  catalogs: readonly CatalogInfo[];
  jobs: JobsModel;
  /** The Directory tree's selected entry key, read when its background menu opens. */
  selectedKeyRef: RefObject<string | null>;
  runShell: (ref: EntryRef, action: ShellAction, commandId?: number) => void;
  showTreemap: (ref: EntryRef) => void;
  searchFromHere: (ref: EntryRef, fullPath: string) => void;
  findDuplicates: (contentHash: string) => void;
  viewInTree: (ref: EntryRef, expectedPath?: string) => void;
  rescanCatalog: (catalogId: number) => void;
  editDescription: (catalog: CatalogInfo) => void;
  collapseAll: () => void;
}

/** The open context menu, and the handlers that build one for each pane's entries. */
export function useContextMenus(o: UseContextMenusOptions) {
  const [menu, setMenu] = useState<MenuState | null>(null);
  const { commands, catalogs, jobs, runShell, showTreemap, searchFromHere, findDuplicates, rescanCatalog, editDescription, collapseAll } = o;

  const context = useMemo<EntryMenuContext>(
    () => ({
      commands,
      runShell,
      showTreemap,
      searchFromHere,
      findDuplicates,
      copyPath: (fullPath) => void navigator.clipboard.writeText(fullPath),
      copyText: (text) => void navigator.clipboard.writeText(text),
      rescanFor: (catalogId) =>
        rescanItem(jobs, catalogId, catalogs.find((c) => c.catalogId === catalogId)?.rootPath, rescanCatalog),
      editDescription: (catalogId) => {
        const c = catalogs.find((x) => x.catalogId === catalogId);
        if (c) editDescription(c);
      },
    }),
    [commands, catalogs, jobs, runShell, showTreemap, searchFromHere, findDuplicates, rescanCatalog, editDescription],
  );

  // The memoised DirectoryPane needs handlers with a stable identity, or every App render (e.g. each
  // streamed search flush) would re-render the hidden tree + listing. They read the latest context at event time.
  const contextRef = useRef(context);
  contextRef.current = context;
  const { selectedKeyRef } = o;

  const close = useCallback(() => setMenu(null), []);

  const onTreeContextMenu = useCallback(
    (ref: EntryRef, fullPath: string, x: number, y: number) =>
      setMenu({ x, y, items: treeNodeMenu(contextRef.current, ref, fullPath, collapseAll) }),
    [collapseAll],
  );
  const onTreeBackgroundMenu = useCallback(
    (x: number, y: number) => {
      const key = selectedKeyRef.current;
      setMenu({ x, y, items: treeBackgroundMenu(key ? parseRef(key) : null, showTreemap, collapseAll) });
    },
    [selectedKeyRef, showTreemap, collapseAll],
  );
  const onListContextMenu = useCallback(
    (n: DirectoryNode, x: number, y: number) => setMenu({ x, y, items: listMenu(contextRef.current, n) }),
    [],
  );

  const { viewInTree } = o;
  const onTreemapTileMenu = useCallback(
    (row: TreemapRow, fullPath: string, x: number, y: number) => {
      const ref = row.ref!;
      setMenu({
        x,
        y,
        items: entryMenu(contextRef.current, { ref, fullPath, isDirectory: row.isDirectory, onViewInTree: () => viewInTree(ref) }),
      });
    },
    [viewInTree],
  );

  const onResultContextMenu = useCallback(
    (row: SearchResultRow, selection: readonly SearchResultRow[], x: number, y: number) => {
      // Results outlive a reload and catalog ids are reused, so confirm the row's catalog is still at that id.
      const current = catalogs.find((c) => c.catalogId === row.ref.catalogId);
      const sameCatalog = !!current && fileBaseName(catalogFile(current)) === row.catalogName;
      setMenu({
        x,
        y,
        items: entryMenu(contextRef.current, {
          ref: row.ref,
          fullPath: row.fullPath,
          isDirectory: row.isDirectory,
          onViewInTree: () => viewInTree(row.ref, row.fullPath),
          canEditDescription: sameCatalog,
          selection,
        }),
      });
    },
    [catalogs, viewInTree],
  );

  const onCatalogContextMenu = useCallback(
    (c: CatalogInfo, x: number, y: number) =>
      setMenu({ x, y, items: catalogMenu(c.catalogId, showTreemap, () => editDescription(c), contextRef.current.rescanFor(c.catalogId)) }),
    [showTreemap, editDescription],
  );

  const onDirectoryTabMenu = useCallback(
    (x: number, y: number) => setMenu({ x, y, items: [collapseAllItem(collapseAll)] }),
    [collapseAll],
  );

  return {
    menu,
    close,
    onTreeContextMenu,
    onTreeBackgroundMenu,
    onListContextMenu,
    onTreemapTileMenu,
    onResultContextMenu,
    onCatalogContextMenu,
    onDirectoryTabMenu,
  };
}
