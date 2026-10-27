import { useEffect, useMemo, useRef, useState } from "react";
import { CdeApiClient } from "../api/client";
import { DirectoryNode, EntryRef, refToString } from "../api/types";
import { toHRString } from "../format";

export interface TreeRoot {
  ref: EntryRef;
  label: string;
  fullPath: string;
  /** Total size of everything under the root (CatalogInfo.rootSize). */
  size: number;
}

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

interface TreeItemProps {
  client: CdeApiClient;
  nodeRef: EntryRef;
  label: string;
  fullPath: string;
  /** Aggregated subtree size in bytes. */
  size: number;
  hasChildren: boolean;
  /** Catalog root (drawn with a drive icon rather than a folder). */
  isRoot?: boolean;
  selectedKey: string | null;
  revealKey?: string | null; // when this key is selected, centre it in the pane (vs just nearest)
  expandTo?: EntryRef[]; // path to auto-expand/select
  onSelect: (ref: EntryRef, fullPath: string) => void;
  onContextMenu: (ref: EntryRef, fullPath: string, x: number, y: number) => void;
}

function TreeItem({ client, nodeRef, label, fullPath, size, hasChildren, isRoot, selectedKey, revealKey, expandTo, onSelect, onContextMenu }: TreeItemProps) {
  const [expanded, setExpanded] = useState(false);
  const [children, setChildren] = useState<DirectoryNode[] | null>(null);
  const key = refToString(nodeRef);

  // Scroll the selected node into view when it becomes selected. A deliberate reveal
  // (view-in-tree, key === revealKey) centres it in the pane; a plain selection uses `nearest`,
  // which is a no-op when the node is already on screen so clicks don't jump.
  const isSelected = selectedKey === key;
  const rowRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (isSelected) {
      rowRef.current?.scrollIntoView({ block: revealKey === key ? "center" : "nearest" });
    }
  }, [isSelected, revealKey]);

  async function ensureChildren() {
    if (children == null) {
      setChildren(await client.children(nodeRef, { foldersOnly: true, sort: "name" }));
    }
  }

  async function toggle() {
    if (!expanded) await ensureChildren();
    setExpanded((e) => !e);
  }

  // Auto-expand along a requested path (view-in-tree / go-to-parent). Expansion ONLY — the
  // selection is set by the initiating handler (selectNode) and shown via selectedKey. Calling
  // onSelect here previously fought the user's clicks and reset the selection.
  useEffect(() => {
    if (!expandTo || expandTo.length === 0) return;
    if (expandTo[0].catalogId === nodeRef.catalogId && expandTo[0].entryIndex === nodeRef.entryIndex) {
      void ensureChildren().then(() => setExpanded(true));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expandTo]);

  // Memoised so children don't receive a brand-new array every render (which would otherwise
  // re-fire their [expandTo] effect on each parent re-render).
  const childExpandTo = useMemo(
    () =>
      expandTo && expandTo.length > 1 && expandTo[0].entryIndex === nodeRef.entryIndex
        ? expandTo.slice(1)
        : undefined,
    [expandTo, nodeRef.entryIndex],
  );

  return (
    <li>
      <div
        ref={rowRef}
        className={`tree-row${isSelected ? " selected" : ""}`}
        onClick={() => onSelect(nodeRef, fullPath)}
        onDoubleClick={() => {
          if (hasChildren) void toggle();
        }}
        onContextMenu={(e) => {
          e.preventDefault();
          e.stopPropagation();
          onContextMenu(nodeRef, fullPath, e.clientX, e.clientY);
        }}
      >
        {/* Expander for folders with sub-folders; an equal-width blank keeps leaf labels aligned. */}
        <span
          className={`twisty${hasChildren ? "" : " leaf"}`}
          onClick={(e) => {
            e.stopPropagation();
            if (hasChildren) void toggle();
          }}
          onDoubleClick={(e) => e.stopPropagation()}
        >
          {hasChildren && <Chevron expanded={expanded} />}
        </span>
        <NodeIcon kind={isRoot ? "drive" : expanded && hasChildren ? "folder-open" : "folder"} />
        <span className="tree-label">{label}</span>
        <span className="tree-size">{toHRString(size)}</span>
      </div>
      {expanded && children && (
        <ul>
          {children.map((c) => (
            <TreeItem
              key={refToString(c.ref)}
              client={client}
              nodeRef={c.ref}
              label={c.name}
              fullPath={c.fullPath}
              size={c.size}
              hasChildren={c.hasChildren}
              selectedKey={selectedKey}
              revealKey={revealKey}
              expandTo={childExpandTo}
              onSelect={onSelect}
              onContextMenu={onContextMenu}
            />
          ))}
        </ul>
      )}
    </li>
  );
}

interface Props {
  client: CdeApiClient;
  roots: TreeRoot[];
  selectedKey: string | null;
  revealKey?: string | null;
  expandTo?: EntryRef[];
  onSelect: (ref: EntryRef, fullPath: string) => void;
  onContextMenu: (ref: EntryRef, fullPath: string, x: number, y: number) => void;
  /** Right-click on blank tree space (rows stop their own right-clicks from reaching here). */
  onBackgroundContextMenu?: (x: number, y: number) => void;
}

export function CatalogTree({
  client, roots, selectedKey, revealKey, expandTo, onSelect, onContextMenu, onBackgroundContextMenu,
}: Props) {
  return (
    <div
      className="tree-host"
      onContextMenu={(e) => {
        if (!onBackgroundContextMenu) return;
        e.preventDefault();
        onBackgroundContextMenu(e.clientX, e.clientY);
      }}
    >
    <ul className="tree">
      {roots.map((r) => (
        <TreeItem
          key={refToString(r.ref)}
          client={client}
          nodeRef={r.ref}
          label={r.label}
          fullPath={r.fullPath}
          size={r.size}
          hasChildren
          isRoot
          selectedKey={selectedKey}
          revealKey={revealKey}
          expandTo={expandTo && expandTo[0]?.catalogId === r.ref.catalogId ? expandTo : undefined}
          onSelect={onSelect}
          onContextMenu={onContextMenu}
        />
      ))}
    </ul>
    </div>
  );
}
