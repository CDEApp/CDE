import { type KeyboardEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CdeApiClient } from "../api/client";
import { DirectoryNode, EntryRef, refToString } from "../api/types";
import { splitRowAndNameStyle, type StyleResolver } from "../colourRules";
import { toHRString } from "../format";
import { changedCatalogs, flattenTree, nextSelection, type TreeRoot, type TreeRow } from "../treeModel";
import { StyledName } from "./StyledName";
import { VirtualList, type VirtualListHandle } from "./VirtualList";

export type { TreeRoot };

const ROW_HEIGHT = 22;
const TREE_KEYS = new Set(["ArrowDown", "ArrowUp", "ArrowLeft", "ArrowRight", "Home", "End"]);
const rowDomId = (key: string) => `tree-row-${key}`;

// Tree glyphs as inline SVG (crisp at any DPI, unlike the old ▸ / · text characters).
function Chevron({ expanded }: { expanded: boolean }) {
  return (
    <svg className={`chevron${expanded ? " expanded" : ""}`} viewBox="0 0 16 16" aria-hidden="true">
      <path d="M6 3.5 10.5 8 6 12.5" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function NodeIcon({ kind }: { kind: "drive" | "folder" | "folder-open" }) {
  if (kind === "drive") {
    return (
      <svg className="node-icon drive" viewBox="0 0 16 16" aria-hidden="true">
        <rect x="1.5" y="4.5" width="13" height="7" rx="1.5" fill="currentColor" opacity=".25" stroke="currentColor" />
        <circle cx="11.5" cy="8" r="1" fill="currentColor" />
      </svg>
    );
  }
  return kind === "folder-open" ? (
    <svg className="node-icon folder" viewBox="0 0 16 16" aria-hidden="true">
      <path d="M1.5 3.5h4l1.5 1.5h6v1.5H4.2L1.5 12.5z" fill="currentColor" opacity=".7" />
      <path d="M4.2 6.5h11l-2.6 6.5H1.5z" fill="currentColor" />
    </svg>
  ) : (
    <svg className="node-icon folder" viewBox="0 0 16 16" aria-hidden="true">
      <path d="M1.5 3.5h4l1.5 1.5h7.5v8h-13z" fill="currentColor" />
    </svg>
  );
}

interface Props {
  client: CdeApiClient;
  roots: TreeRoot[];
  selectedKey: string | null;
  revealKey?: string | null; // when this key is selected, centre it in the pane (vs just nearest)
  /** Bumped for each deliberate reveal, so revealing the already-selected node scrolls again. */
  revealNonce?: number;
  expandTo?: EntryRef[]; // path to auto-expand
  onSelect: (ref: EntryRef, fullPath: string) => void;
  onContextMenu: (ref: EntryRef, fullPath: string, x: number, y: number) => void;
  /** Right-click on blank tree space (rows stop their own right-clicks from reaching here). */
  onBackgroundContextMenu?: (x: number, y: number) => void;
  /** Name-rule styles (built with `[folder]` skipped). Selection colours override them. */
  resolveStyle: StyleResolver;
}

/**
 * Folder tree, flattened to its visible rows and virtualized (issue #86): expanding a folder with
 * tens of thousands of sub-folders, or changing the selection under one, only touches the rows on
 * screen. Child folders are fetched on first expand and kept for later expands.
 */
export function CatalogTree({
  client, roots, selectedKey, revealKey, revealNonce, expandTo, onSelect, onContextMenu, onBackgroundContextMenu, resolveStyle,
}: Props) {
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());
  const [children, setChildren] = useState<ReadonlyMap<string, DirectoryNode[]>>(() => new Map());
  // Fetches by node key, so a node is fetched once however often it is asked for.
  const loads = useRef(new Map<string, Promise<void>>());
  const listRef = useRef<VirtualListHandle>(null);

  // A rescan, or a reload that puts a different catalog under the same id, invalidates that catalog's
  // refs: drop its expansion and loaded children (other catalogs keep theirs).
  const prevRoots = useRef<TreeRoot[]>([]);
  useEffect(() => {
    const changed = changedCatalogs(prevRoots.current, roots);
    prevRoots.current = roots;
    if (changed.size === 0) return;
    const stale = (key: string) => changed.has(Number(key.slice(0, key.indexOf("-"))));
    for (const key of [...loads.current.keys()]) if (stale(key)) loads.current.delete(key);
    setChildren((prev) => new Map([...prev].filter(([key]) => !stale(key))));
    setExpanded((prev) => new Set([...prev].filter((key) => !stale(key))));
  }, [roots]);

  const ensureChildren = useCallback(
    (ref: EntryRef) => {
      const key = refToString(ref);
      let load = loads.current.get(key);
      if (!load) {
        const current = client.children(ref, { foldersOnly: true, sort: "name" }).then((kids) => {
          // Dropped (rescan / reload) while in flight: these children belong to the old catalog.
          if (loads.current.get(key) === current) setChildren((prev) => new Map(prev).set(key, kids));
        });
        current.catch(() => {
          if (loads.current.get(key) === current) loads.current.delete(key); // a failed fetch may be retried
        });
        loads.current.set(key, current);
        load = current;
      }
      return load;
    },
    [client],
  );

  const setOpen = (key: string, open: boolean) =>
    setExpanded((prev) => {
      if (prev.has(key) === open) return prev;
      const next = new Set(prev);
      if (open) next.add(key);
      else next.delete(key);
      return next;
    });

  async function toggle(row: TreeRow) {
    if (row.expanded) {
      setOpen(row.key, false);
    } else {
      try {
        await ensureChildren(row.ref);
      } catch (e) {
        console.error("tree: loading sub-folders failed", e);
        return;
      }
      setOpen(row.key, true);
    }
  }

  // Auto-expand along a requested path (view-in-tree / go-to-parent), top down. Expansion ONLY: the
  // selection is set by the initiating handler (selectNode) and shown via selectedKey.
  useEffect(() => {
    if (!expandTo || expandTo.length === 0) return;
    let cancelled = false;
    void (async () => {
      for (const ref of expandTo) {
        try {
          await ensureChildren(ref);
        } catch (e) {
          console.error("tree: auto-expand failed", e);
          return;
        }
        if (cancelled) return;
        setOpen(refToString(ref), true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [expandTo, ensureChildren]);

  const rows = useMemo(() => flattenTree(roots, expanded, children), [roots, expanded, children]);

  // Scroll the selected node into view once it is selected and has a row (an auto-expand may still
  // be loading its parents). A deliberate reveal (key === revealKey) centres it; a plain selection
  // uses `nearest`, a no-op when the row is already on screen so clicks don't jump.
  const pendingScroll = useRef<{ key: string; align: "nearest" | "center" } | null>(null);
  useEffect(() => {
    pendingScroll.current = selectedKey
      ? { key: selectedKey, align: revealKey === selectedKey ? "center" : "nearest" }
      : null;
  }, [selectedKey, revealKey, revealNonce]);
  useEffect(() => {
    const pending = pendingScroll.current;
    if (!pending) return;
    const index = rows.findIndex((r) => r.key === pending.key);
    if (index < 0) return;
    listRef.current?.scrollToIndex(index, pending.align);
    pendingScroll.current = null;
  }, [rows, selectedKey, revealKey, revealNonce]);

  // Keyboard navigation (issue #89). Focus stays on the host (rows unmount under virtualization);
  // the selected row is exposed through aria-activedescendant. A key selects exactly as a click does
  // (same onSelect), so the listing follows and the existing scroll-on-selection keeps it in view.
  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.target !== e.currentTarget || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return;
    const nav = nextSelection(rows, selectedKey, e.key);
    // Tree keys never scroll the pane, even when they do nothing (e.g. Left on a root).
    if (TREE_KEYS.has(e.key)) e.preventDefault();
    if (!nav) return;
    if ("expand" in nav) {
      const row = rows.find((r) => r.key === nav.expand);
      if (row) void toggle(row);
      return;
    }
    if ("collapse" in nav) {
      setOpen(nav.collapse, false); // the selection stays on the collapsed folder
      return;
    }
    const row = rows.find((r) => r.key === nav.select);
    if (row) onSelect(row.ref, row.fullPath);
  };

  return (
    <div
      className="tree-host"
      role="tree"
      aria-label="Directory tree"
      tabIndex={0}
      aria-activedescendant={selectedKey && rows.some((r) => r.key === selectedKey) ? rowDomId(selectedKey) : undefined}
      onKeyDown={onKeyDown}
      onMouseDown={(e) => {
        if (e.button === 0) e.currentTarget.focus({ preventScroll: true });
      }}
      onContextMenu={(e) => {
        if (!onBackgroundContextMenu) return;
        e.preventDefault();
        onBackgroundContextMenu(e.clientX, e.clientY);
      }}
    >
      <VirtualList<TreeRow>
        className="tree"
        items={rows}
        rowHeight={ROW_HEIGHT}
        handleRef={listRef}
        renderRow={(row, _i, style) => {
          const isSelected = row.key === selectedKey;
          // Selected rows keep the selection look: no rule styles (transparent text must not hide the label).
          const { row: rowStyle, name: nameStyle } = isSelected
            ? { row: undefined, name: undefined }
            : splitRowAndNameStyle(resolveStyle(row.label, true));
          return (
            <div // NOSONAR S6852: aria-activedescendant pattern (#89), focus stays on the container as rows unmount under virtualization
              key={row.key}
              id={rowDomId(row.key)}
              role="treeitem"
              aria-level={row.depth + 1}
              aria-expanded={row.hasChildren ? row.expanded : undefined}
              aria-selected={isSelected}
              className={`tree-row${isSelected ? " selected" : ""}`}
              style={{
                ...style,
                paddingLeft: 6 + row.depth * 16,
                ...rowStyle,
              }}
              onClick={() => onSelect(row.ref, row.fullPath)}
              onDoubleClick={() => {
                if (row.hasChildren) void toggle(row);
              }}
              onContextMenu={(e) => {
                e.preventDefault();
                e.stopPropagation();
                onContextMenu(row.ref, row.fullPath, e.clientX, e.clientY);
              }}
            >
              {/* Expander for folders with sub-folders; an equal-width blank keeps leaf labels aligned. */}
              <span
                className={`twisty${row.hasChildren ? "" : " leaf"}`}
                onClick={(e) => {
                  e.stopPropagation();
                  if (row.hasChildren) void toggle(row);
                }}
                onDoubleClick={(e) => e.stopPropagation()}
              >
                {row.hasChildren && <Chevron expanded={row.expanded} />}
              </span>
              <NodeIcon kind={row.isRoot ? "drive" : row.expanded && row.hasChildren ? "folder-open" : "folder"} />
              <span className="tree-label" title={row.catalogName ? `${row.fullPath} [${row.catalogName}]` : row.fullPath}>
                <StyledName nameStyle={nameStyle}>{row.label}</StyledName>
              </span>
              {/* Outside the label so a narrow pane truncates the path, not the name telling roots apart. */}
              {row.catalogName && <span className="tree-catalog" title={row.catalogName}>[{row.catalogName}]</span>}
              <span className="tree-size">{toHRString(row.size)}</span>
            </div>
          );
        }}
      />
    </div>
  );
}
