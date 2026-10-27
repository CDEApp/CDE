import { memo } from "react";
import { CdeApiClient } from "../api/client";
import { DirectoryNode, EntryRef } from "../api/types";
import type { StyleResolver } from "../colourRules";
import type { ColumnWidths } from "../columnWidths";
import type { DirectorySort, SortColumn } from "../directorySort";
import { CatalogTree, type TreeRoot } from "./CatalogTree";
import { DirectoryList } from "./DirectoryList";
import { SplitPane } from "./SplitPane";

interface Props {
  client: CdeApiClient;
  roots: TreeRoot[];
  selectedKey: string | null;
  revealKey: string | null;
  revealNonce: number;
  expandTo: EntryRef[] | undefined;
  dirPath: string;
  dirNodes: DirectoryNode[];
  sort: DirectorySort;
  columnWidths?: unknown;
  onColumnWidthsChange: (widths: ColumnWidths) => void;
  initialLeftWidth: number;
  resolveTreeStyle: StyleResolver;
  resolveStyle: StyleResolver;
  onResizeEnd: (width: number) => void;
  onSelect: (ref: EntryRef, fullPath: string) => void;
  onTreeContextMenu: (ref: EntryRef, fullPath: string, x: number, y: number) => void;
  onBackgroundContextMenu: (x: number, y: number) => void;
  onSort: (column: SortColumn) => void;
  onActivate: (node: DirectoryNode) => void;
  onListContextMenu: (node: DirectoryNode, x: number, y: number) => void;
}

/**
 * The Directory tab (tree + listing). It stays mounted while other tabs show (issue #80), so it is
 * memoised: App passes only stable props, and a search streaming results on another tab (which
 * re-renders App on every flush) does not re-render the hidden tree or listing.
 */
export const DirectoryPane = memo(function DirectoryPane(p: Props) {
  return (
    <SplitPane
      initialLeftWidth={p.initialLeftWidth}
      onResizeEnd={p.onResizeEnd}
      left={
        <CatalogTree
          client={p.client}
          roots={p.roots}
          selectedKey={p.selectedKey}
          revealKey={p.revealKey}
          revealNonce={p.revealNonce}
          expandTo={p.expandTo}
          onSelect={p.onSelect}
          onContextMenu={p.onTreeContextMenu}
          onBackgroundContextMenu={p.onBackgroundContextMenu}
          resolveStyle={p.resolveTreeStyle}
        />
      }
      right={
        <DirectoryList
          path={p.dirPath}
          nodes={p.dirNodes}
          sort={p.sort}
          onSort={p.onSort}
          columnWidths={p.columnWidths}
          onColumnWidthsChange={p.onColumnWidthsChange}
          resolveStyle={p.resolveStyle}
          onActivate={p.onActivate}
          onContextMenu={p.onListContextMenu}
        />
      }
    />
  );
});
