import { useCallback, useEffect, useId, useMemo, useRef, useState } from "react";
import type { CdeApiClient } from "../api/client";
import type { CatalogInfo, DirectoryNode, EntryRef, SubtreeSummary } from "../api/types";
import { splitRowAndNameStyle, type StyleResolver } from "../colourRules";
import {
  alignChildren,
  type ChildLists,
  DIFF_FILTERS,
  DIFF_MENU_FILTERS,
  type DiffFilterId,
  type DiffRow,
  flattenVisible,
  relativeSegments,
} from "../diffModel";
import { catalogFile, fileBaseName, formatDate, sizeCell, sizeTitle } from "../format";
import { createRequestGuard } from "../requestGuard";
import { CatalogSelect } from "./CatalogSelect";
import { ContextMenu, type MenuState } from "./ContextMenu";
import { StyledName } from "./StyledName";
import { VirtualList } from "./VirtualList";

interface Props {
  client: CdeApiClient;
  catalogs: CatalogInfo[];
  resolveStyle: StyleResolver;
  onExplore: (ref: EntryRef) => void;
}

const ROW_HEIGHT = 22;
const INDENT = 14;
const sameRef = (a: EntryRef | null, b: EntryRef | null) =>
  a === b || (!!a && !!b && a.catalogId === b.catalogId && a.entryIndex === b.entryIndex);

/** Walk `children` by name from the catalog root; throws a message fit to show under the path box. */
async function resolveFolder(client: CdeApiClient, catalog: CatalogInfo, path: string): Promise<EntryRef> {
  const segments = relativeSegments(catalog.rootPath, path);
  if (!segments) throw new Error("The path is not inside this catalog.");
  let ref: EntryRef = { catalogId: catalog.catalogId, entryIndex: 0 };
  let isDirectory = true;
  for (const segment of segments) {
    if (!isDirectory) throw new Error(`"${segment}" is inside a file.`);
    const wanted = segment.toLowerCase();
    const kids = await client.children(ref, { hasChildren: false });
    const hit = kids.find((k) => k.name.toLowerCase() === wanted);
    if (!hit) throw new Error(`"${segment}" was not found in this catalog.`);
    ref = hit.ref;
    isDirectory = hit.isDirectory;
  }
  if (!isDirectory) throw new Error("The path is a file, not a folder.");
  return ref;
}

interface Side {
  catalogId: number | null;
  pathText: string;
  error: string | null;
  ref: EntryRef | null;
  setPathText: (text: string) => void;
  selectCatalog: (catalogId: number | null) => void;
  applyPath: () => void;
  setRoot: (node: DirectoryNode) => void;
}

function useSide(client: CdeApiClient, catalogs: CatalogInfo[]): Side {
  const [catalogId, setCatalogId] = useState<number | null>(null);
  const [pathText, setPathText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [ref, setRef] = useState<EntryRef | null>(null);
  const guard = useRef(createRequestGuard()).current;

  const selectCatalog = useCallback(
    (id: number | null) => {
      guard.invalidate();
      const catalog = catalogs.find((c) => c.catalogId === id);
      setCatalogId(catalog ? catalog.catalogId : null);
      setPathText(catalog?.rootPath ?? "");
      setError(null);
      setRef(catalog ? { catalogId: catalog.catalogId, entryIndex: 0 } : null);
    },
    [catalogs, guard],
  );

  const applyPath = useCallback(() => {
    const catalog = catalogs.find((c) => c.catalogId === catalogId);
    if (!catalog) return;
    const token = guard.begin();
    resolveFolder(client, catalog, pathText).then(
      (next) => {
        if (!guard.isCurrent(token)) return;
        setError(null);
        setRef((prev) => (sameRef(prev, next) ? prev : next));
      },
      (e: unknown) => {
        if (guard.isCurrent(token)) setError(e instanceof Error ? e.message : String(e));
      },
    );
  }, [catalogs, catalogId, client, guard, pathText]);

  const setRoot = useCallback(
    (node: DirectoryNode) => {
      guard.invalidate();
      setPathText(node.fullPath);
      setError(null);
      setRef(node.ref);
    },
    [guard],
  );

  return { catalogId, pathText, error, ref, setPathText, selectCatalog, applyPath, setRoot };
}

/** Dropdown order: the catalogs' on-disk file names, ascending (case-insensitive). */
function sortByFileName(catalogs: CatalogInfo[]): CatalogInfo[] {
  const key = (c: CatalogInfo) => fileBaseName(catalogFile(c)).toLowerCase();
  return [...catalogs].sort((a, b) => key(a).localeCompare(key(b)) || a.catalogId - b.catalogId);
}

function SideHeader({ side, catalogs, label }: { side: Side; catalogs: CatalogInfo[]; label: string }) {
  const pathId = useId();
  return (
    <div className="diff-side">
      <span className="diff-field-label" aria-hidden="true">Catalog</span>
      <CatalogSelect label={label} catalogs={sortByFileName(catalogs)} value={side.catalogId} onChange={side.selectCatalog} />
      <label className="diff-field-label" htmlFor={pathId}>Base</label>
      <input
        id={pathId}
        aria-label={`${label} path`}
        className={side.error ? "invalid" : undefined}
        value={side.pathText}
        disabled={side.catalogId === null}
        spellCheck={false}
        onChange={(e) => side.setPathText(e.target.value)}
        onBlur={side.applyPath}
        onKeyDown={(e) => {
          if (e.key === "Enter") side.applyPath();
        }}
      />
      {side.error && <div className="diff-error" role="alert">{side.error}</div>}
    </div>
  );
}

function Chevron({ expanded }: { expanded: boolean }) {
  return (
    <svg className={`chevron${expanded ? " expanded" : ""}`} viewBox="0 0 16 16" aria-hidden="true">
      <path d="M6 3.5 10.5 8 6 12.5" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

interface HalfProps {
  node: DirectoryNode | null;
  row: DiffRow;
  expanded: boolean;
  onToggle: () => void;
  resolveStyle: StyleResolver;
  onFolderMenu: (node: DirectoryNode, x: number, y: number) => void;
}

/** One side of a row. A missing entry renders empty cells so the other side keeps its position. */
function Half({ node, row, expanded, onToggle, resolveStyle, onFolderMenu }: HalfProps) {
  if (!node) return <div className="diff-half empty" aria-hidden="true"><div className="cell" /></div>;
  const { name } = splitRowAndNameStyle(resolveStyle(node.name, node.isDirectory));
  return (
    <div
      className="diff-half"
      onContextMenu={
        node.isDirectory
          ? (e) => {
              e.preventDefault();
              e.stopPropagation();
              onFolderMenu(node, e.clientX, e.clientY);
            }
          : undefined
      }
    >
      <div className="cell diff-name" style={{ paddingLeft: 8 + row.depth * INDENT }}>
        {row.isDirectory ? (
          <button
            type="button"
            className="twisty"
            aria-label={`${expanded ? "Collapse" : "Expand"} ${node.name}`}
            aria-expanded={expanded}
            onClick={onToggle}
          >
            <Chevron expanded={expanded} />
          </button>
        ) : (
          <span className="twisty leaf" />
        )}
        <StyledName nameStyle={name}>{node.name}</StyledName>
      </div>
      <div className="cell num" title={sizeTitle(node.size)}>{sizeCell(node.isDirectory, node.size, node.isReparsePoint)}</div>
      <div className="cell">{formatDate(node.modified, node.isModifiedBad)}</div>
    </div>
  );
}

const EMPTY: readonly DirectoryNode[] = [];
const SUMMARY_BATCH = 100;

/**
 * Two directories side by side in ONE virtual list, so a single scroll container keeps both sides
 * aligned and an expand/collapse applies to both halves of the row.
 */
export function DiffView({ client, catalogs, resolveStyle, onExplore }: Props) {
  const left = useSide(client, catalogs);
  const right = useSide(client, catalogs);
  const [filter, setFilter] = useState<DiffFilterId>("all");
  const [diffFilter, setDiffFilter] = useState<DiffFilterId>("differences");
  const [menu, setMenu] = useState<MenuState | null>(null);
  const closeMenu = useCallback(() => setMenu(null), []);
  const folderMenu = (side: Side) => (node: DirectoryNode, x: number, y: number) =>
    setMenu({ x, y, items: [
        { label: "Set as base", onClick: () => side.setRoot(node) },
        { label: "Explore", onClick: () => onExplore(node.ref) },
      ],
    });
  const leftFolderMenu = folderMenu(left);
  const rightFolderMenu = folderMenu(right);
  const [rootRows, setRootRows] = useState<DiffRow[]>([]);
  const [childrenByKey, setChildrenByKey] = useState<ReadonlyMap<string, ChildLists>>(() => new Map());
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());
  const [loadError, setLoadError] = useState<string | null>(null);
  // One token per pair of roots: expansions that finish after the roots changed are dropped.
  const guard = useRef(createRequestGuard()).current;
  const generation = useRef(0);
  // Exact subtree summaries by row key, requested once per folder pair; aborted when the roots change.
  const [summaries, setSummaries] = useState<ReadonlyMap<string, SubtreeSummary>>(() => new Map());
  const requested = useRef(new Set<string>());
  const summaryAbort = useRef<AbortController | null>(null);

  const leftRef = left.ref;
  const rightRef = right.ref;
  useEffect(() => {
    generation.current = guard.begin();
    const abort = new AbortController();
    summaryAbort.current = abort;
    requested.current = new Set();
    setSummaries(new Map());
    setRootRows([]);
    setChildrenByKey(new Map());
    setExpanded(new Set());
    setLoadError(null);
    if (!leftRef || !rightRef) return () => abort.abort();
    const token = generation.current;
    Promise.all([client.children(leftRef, { hasChildren: false }), client.children(rightRef, { hasChildren: false })]).then(
      ([l, r]) => {
        if (guard.isCurrent(token)) setRootRows(alignChildren(l, r, 0, ""));
      },
      (e: unknown) => {
        if (guard.isCurrent(token)) setLoadError(e instanceof Error ? e.message : String(e));
      },
    );
    return () => abort.abort();
  }, [client, guard, leftRef, rightRef]);

  useEffect(() => {
    const abort = summaryAbort.current;
    if (!abort) return;
    const pending: { key: string; left: EntryRef; right: EntryRef }[] = [];
    const collect = (rows: readonly DiffRow[]) => {
      for (const row of rows) {
        if (!row.isDirectory || !row.left || !row.right || requested.current.has(row.key)) continue;
        requested.current.add(row.key);
        pending.push({ key: row.key, left: row.left.ref, right: row.right.ref });
      }
    };
    collect(rootRows);
    for (const [key, kids] of childrenByKey) collect(alignChildren(kids.left, kids.right, 0, key));
    if (pending.length === 0) return;
    const token = generation.current;
    void (async () => {
      for (let i = 0; i < pending.length; i += SUMMARY_BATCH) {
        const batch = pending.slice(i, i + SUMMARY_BATCH);
        try {
          const pairs = batch.map(({ left, right }) => ({ left, right }));
          const result = await client.compareSummaries(pairs, { signal: abort.signal });
          if (!guard.isCurrent(token)) return;
          setSummaries((prev) => {
            const next = new Map(prev);
            batch.forEach((p, n) => {
              if (result[n]) next.set(p.key, result[n]);
            });
            return next;
          });
        } catch (e) {
          if (!abort.signal.aborted && guard.isCurrent(token)) console.error("compare summaries failed", e);
          return;
        }
      }
    })();
  }, [rootRows, childrenByKey, client, guard]);

  const toggle = useCallback(
    (row: DiffRow) => {
      const opening = !expanded.has(row.key);
      setExpanded((prev) => {
        const next = new Set(prev);
        if (opening) next.add(row.key);
        else next.delete(row.key);
        return next;
      });
      if (!opening || childrenByKey.has(row.key)) return;
      const token = generation.current;
      Promise.all([
        row.left ? client.children(row.left.ref, { hasChildren: false }) : EMPTY,
        row.right ? client.children(row.right.ref, { hasChildren: false }) : EMPTY,
      ]).then(
        ([l, r]) => {
          if (guard.isCurrent(token)) setChildrenByKey((prev) => new Map(prev).set(row.key, { left: l, right: r }));
        },
        (e: unknown) => {
          if (guard.isCurrent(token)) setLoadError(e instanceof Error ? e.message : String(e));
        },
      );
    },
    [childrenByKey, client, expanded, guard],
  );

  const rows = useMemo(
    () => flattenVisible(expanded, childrenByKey, rootRows, filter, summaries),
    [expanded, childrenByKey, rootRows, filter, summaries],
  );
  const inDiffMenu = filter !== "all" && filter !== "same";
  const ready = leftRef !== null && rightRef !== null;

  return (
    <div className="diff-view">
      <div className="diff-toolbar" role="group" aria-label="Show">
        <button type="button" className={filter === "all" ? "active" : ""} aria-pressed={filter === "all"} onClick={() => setFilter("all")}>
          All
        </button>
        <span className="diff-split">
          <button
            type="button"
            className={inDiffMenu ? "active" : ""}
            aria-pressed={inDiffMenu}
            title={DIFF_FILTERS[diffFilter].label}
            onClick={() => setFilter(diffFilter)}
          >
            Diffs
          </button>
          <button
            type="button"
            className={`diff-split-arrow${inDiffMenu ? " active" : ""}`}
            aria-label="Diffs options"
            aria-haspopup="menu"
            onClick={(e) => {
              const box = e.currentTarget.parentElement!.getBoundingClientRect();
              setMenu({
                x: box.left,
                y: box.bottom,
                items: DIFF_MENU_FILTERS.map((id) => ({
                  label: `${diffFilter === id ? "✓ " : "  "}${DIFF_FILTERS[id].label}`,
                  onClick: () => {
                    setDiffFilter(id);
                    setFilter(id);
                  },
                })),
              });
            }}
          >
            {"▾"}
          </button>
        </span>
        <button type="button" className={filter === "same" ? "active" : ""} aria-pressed={filter === "same"} onClick={() => setFilter("same")}>
          Same
        </button>
      </div>
      <ContextMenu menu={menu} onClose={closeMenu} />
      <div className="diff-sides">
        <SideHeader side={left} catalogs={catalogs} label="Left" />
        <SideHeader side={right} catalogs={catalogs} label="Right" />
      </div>
      {loadError && <div className="diff-error" role="alert">{loadError}</div>}
      {ready ? (
        <VirtualList<DiffRow>
          className="diff-cols"
          items={rows}
          rowHeight={ROW_HEIGHT}
          role="grid"
          ariaLabel="Directory comparison"
          header={
            <div className="vlist-header" role="row">
              {[0, 1].map((side) => (
                <div key={side} className="diff-half">
                  <span className="hcell">Name</span>
                  <span className="hcell num">Size</span>
                  <span className="hcell">Modified</span>
                </div>
              ))}
            </div>
          }
          renderRow={(row, _i, style) => (
            <div // NOSONAR S6852: double-click is a mouse shortcut; keyboard users toggle the row with the expander button in each Half
              key={row.key}
              className={`vlist-row diff-row ${row.status}`}
              role="row"
              style={style}
              onDoubleClick={row.isDirectory ? () => toggle(row) : undefined}
            >
              <Half node={row.left} row={row} expanded={expanded.has(row.key)} onToggle={() => toggle(row)} resolveStyle={resolveStyle} onFolderMenu={leftFolderMenu} />
              <Half node={row.right} row={row} expanded={expanded.has(row.key)} onToggle={() => toggle(row)} resolveStyle={resolveStyle} onFolderMenu={rightFolderMenu} />
            </div>
          )}
        />
      ) : (
        <div className="diff-empty">Choose a catalog on each side to compare two folders.</div>
      )}
    </div>
  );
}
