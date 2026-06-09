import { useEffect, useMemo, useRef, useState } from "react";
import { CdeApiClient } from "../api/client";
import { DirectoryNode, EntryRef, refToString } from "../api/types";

export interface TreeRoot {
  ref: EntryRef;
  label: string;
  fullPath: string;
}

interface TreeItemProps {
  client: CdeApiClient;
  nodeRef: EntryRef;
  label: string;
  fullPath: string;
  hasChildren: boolean;
  selectedKey: string | null;
  revealKey?: string | null; // when this key is selected, centre it in the pane (vs just nearest)
  expandTo?: EntryRef[]; // path to auto-expand/select
  onSelect: (ref: EntryRef, fullPath: string) => void;
}

function TreeItem({ client, nodeRef, label, fullPath, hasChildren, selectedKey, revealKey, expandTo, onSelect }: TreeItemProps) {
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
      >
        <span
          className={`twisty${hasChildren ? "" : " leaf"}`}
          onClick={(e) => {
            e.stopPropagation();
            if (hasChildren) void toggle();
          }}
        >
          {hasChildren ? (expanded ? "▾" : "▸") : "·"}
        </span>
        <span className="tree-label">{label}</span>
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
              hasChildren={c.hasChildren}
              selectedKey={selectedKey}
              revealKey={revealKey}
              expandTo={childExpandTo}
              onSelect={onSelect}
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
}

export function CatalogTree({ client, roots, selectedKey, revealKey, expandTo, onSelect }: Props) {
  return (
    <ul className="tree">
      {roots.map((r) => (
        <TreeItem
          key={refToString(r.ref)}
          client={client}
          nodeRef={r.ref}
          label={r.label}
          fullPath={r.fullPath}
          hasChildren
          selectedKey={selectedKey}
          revealKey={revealKey}
          expandTo={expandTo && expandTo[0]?.catalogId === r.ref.catalogId ? expandTo : undefined}
          onSelect={onSelect}
        />
      ))}
    </ul>
  );
}
