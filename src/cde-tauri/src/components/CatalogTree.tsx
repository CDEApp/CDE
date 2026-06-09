import { useEffect, useState } from "react";
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
  expandTo?: EntryRef[]; // path to auto-expand/select
  onSelect: (ref: EntryRef, fullPath: string) => void;
}

function TreeItem({ client, nodeRef, label, fullPath, hasChildren, selectedKey, expandTo, onSelect }: TreeItemProps) {
  const [expanded, setExpanded] = useState(false);
  const [children, setChildren] = useState<DirectoryNode[] | null>(null);
  const key = refToString(nodeRef);

  async function ensureChildren() {
    if (children == null) {
      setChildren(await client.children(nodeRef, { foldersOnly: true, sort: "name" }));
    }
  }

  async function toggle() {
    if (!expanded) await ensureChildren();
    setExpanded((e) => !e);
  }

  // Auto-expand along a requested path (view-in-tree / go-to-parent).
  useEffect(() => {
    if (!expandTo || expandTo.length === 0) return;
    const [head, ...rest] = expandTo;
    if (head.catalogId === nodeRef.catalogId && head.entryIndex === nodeRef.entryIndex) {
      void ensureChildren().then(() => setExpanded(true));
      if (rest.length === 0) onSelect(nodeRef, fullPath);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expandTo]);

  const childExpandTo =
    expandTo && expandTo.length > 1 && expandTo[0].entryIndex === nodeRef.entryIndex
      ? expandTo.slice(1)
      : undefined;

  return (
    <li>
      <div
        className={`tree-row${selectedKey === key ? " selected" : ""}`}
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
  expandTo?: EntryRef[];
  onSelect: (ref: EntryRef, fullPath: string) => void;
}

export function CatalogTree({ client, roots, selectedKey, expandTo, onSelect }: Props) {
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
          expandTo={expandTo && expandTo[0]?.catalogId === r.ref.catalogId ? expandTo : undefined}
          onSelect={onSelect}
        />
      ))}
    </ul>
  );
}
