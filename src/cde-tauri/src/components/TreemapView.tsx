import { useEffect, useMemo, useRef, useState } from "react";
import { defineChart, type ChartInteractionController } from "@tanstack/charts";
import { treemap, type TreemapNode } from "@tanstack/charts/hierarchy/treemap";
import { Chart } from "@tanstack/charts/react";
import { CdeApiClient } from "../api/client";
import { DirectoryNode, EntryRef, TreemapResponse, TreemapRow } from "../api/types";
import { toHRString } from "../format";
import {
  branchColors,
  branchIdOf,
  drillTargetId,
  fullPathOf,
  MERGED_COLOR,
  minFractionFor,
  mix,
  tileColor,
} from "../treemapModel";

export const TREEMAP_DEPTHS = [1, 2, 3, 4, 5];

interface Props {
  client: CdeApiClient;
  /** The folder to map; a new `nonce` re-maps even when the ref is unchanged. */
  request: { ref: EntryRef; nonce: number };
  depth: number;
  onDepthChange: (depth: number) => void;
  onStatus: (text: string) => void;
  onTileMenu: (row: TreemapRow, fullPath: string, x: number, y: number) => void;
}

type Node = TreemapNode<TreemapRow>;

const DIM_TARGET = "#2a2a2a"; // tiles outside a highlighted branch fade toward the pane colour

/**
 * Treemap of a folder: tile area is size, colour is the top-level branch. Click a tile to drill
 * into its branch; the breadcrumb, ⬆ Up and Backspace go back up. Rendered with TanStack Charts'
 * treemap mark over flat rows from GET /entries/{ref}/treemap.
 */
export function TreemapView({ client, request, depth, onDepthChange, onStatus, onTileMenu }: Props) {
  const [root, setRoot] = useState<EntryRef>(request.ref);
  const [refreshNonce, setRefreshNonce] = useState(0);
  const [data, setData] = useState<TreemapResponse | null>(null);
  const [crumbs, setCrumbs] = useState<DirectoryNode[]>([]);
  const [highlight, setHighlight] = useState<string | null>(null);
  const [hover, setHover] = useState<Node | null>(null);

  const bodyRef = useRef<HTMLDivElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);
  const interactionRef = useRef<ChartInteractionController<Node> | null>(null);
  const [size, setSize] = useState({ width: 0, height: 0 });

  // A new "Show TreeMap" request (a new nonce) re-roots the view. Keyed on the nonce alone: a
  // caller passing a fresh-but-equal ref object each render must not re-fetch in a loop.
  useEffect(
    () => setRoot(request.ref),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [request.nonce],
  );

  useEffect(() => {
    const el = bodyRef.current;
    if (!el) return;
    const measure = () => setSize({ width: el.clientWidth, height: el.clientHeight });
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  // Fetch on root/depth/refresh only — a resize just re-tiles (the merge threshold was taken from
  // the size at fetch time). Starting a new fetch aborts the previous one.
  const sizeRef = useRef(size);
  sizeRef.current = size;
  useEffect(() => {
    const abort = new AbortController();
    let settled = false;
    onStatus("Building treemap…");
    const { width, height } = sizeRef.current;
    Promise.all([
      client.treemap(root, { depth, minFraction: minFractionFor(width, height), signal: abort.signal }),
      client.path(root),
    ])
      .then(([map, path]) => {
        if (abort.signal.aborted) return;
        settled = true;
        setData(map);
        setCrumbs(path);
        setHighlight(null);
        setHover(null);
        onStatus("");
      })
      .catch((e: Error) => {
        if (abort.signal.aborted || e.name === "AbortError") return;
        settled = true;
        onStatus(`TreeMap failed: ${e.message}`);
      });
    return () => {
      abort.abort();
      // Only clear our own "Building…" message; never another component's status.
      if (!settled) onStatus("");
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [client, root, depth, refreshNonce]);

  const rows = data?.rows ?? [];
  const rowsById = useMemo(() => new Map(rows.map((r) => [r.nodeId, r])), [rows]);
  const rootRow = useMemo(() => rows.find((r) => r.parentId == null), [rows]);
  const branches = useMemo(
    () => rows.filter((r) => r.parentId === rootRow?.nodeId).sort((a, b) => b.size - a.size),
    [rows, rootRow],
  );
  const colors = useMemo(() => branchColors(branches), [branches]);

  // The mark throws on an empty hierarchy ("no root"), so there is no definition until rows arrive.
  const definition = useMemo(
    () =>
      rows.length > 1 &&
      defineChart({
        marks: [
          treemap(rows, {
            id: "folder-treemap",
            nodeId: "nodeId",
            parentId: "parentId",
            value: "value",
            paddingOuter: 2,
            paddingInner: 1,
            inset: 0.5,
            fill: (n: Node) => {
              const branch = branchIdOf(n);
              const base = n.data?.mergedCount
                ? MERGED_COLOR
                : tileColor((branch && colors.get(branch)) || MERGED_COLOR, n.depth);
              return highlight && branch !== highlight ? mix(base, DIM_TARGET, 0.7) : base;
            },
            stroke: "#1e1e1e",
            strokeWidth: 0.5,
            label: (n: Node) => n.data?.name ?? null,
            labelFill: "#141414",
            labelFontSize: 11,
          }),
        ],
        scales: { x: null, y: null },
        guides: false,
        margin: 0,
        tooltip: false,
      }),
    [rows, colors, highlight],
  );

  const up = crumbs.length > 1 ? crumbs[crumbs.length - 2].ref : null;

  // Backspace = up one level (ignored while typing in a field).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Backspace" || !up) return;
      const t = e.target as HTMLElement | null;
      if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.isContentEditable)) return;
      e.preventDefault();
      setRoot(up);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [up]);

  function nodeAt(clientX: number, clientY: number): Node | null {
    return (interactionRef.current?.resolvePointer(clientX, clientY)?.point.datum as Node | undefined) ?? null;
  }

  function drill(nodeId: string | null) {
    const ref = nodeId ? rowsById.get(nodeId)?.ref : null;
    if (ref) setRoot(ref);
  }

  const rootSize = data?.rootSize ?? 0;
  const pct = (bytes: number) => (rootSize > 0 ? `${((100 * bytes) / rootSize).toFixed(1)}%` : "");
  const hoverRow = hover?.data ?? null;

  return (
    <div className="treemap-view">
      <div className="treemap-toolbar">
        <button title="Up one level (Backspace)" disabled={!up} onClick={() => up && setRoot(up)}>
          ⬆ Up
        </button>
        <nav className="treemap-crumbs" aria-label="Treemap location">
          {crumbs.map((c, i) =>
            i === crumbs.length - 1 ? (
              <span key={i} className="crumb current">{c.name}</span>
            ) : (
              <span key={i}>
                <button className="crumb link" onClick={() => setRoot(c.ref)}>{c.name}</button>
                <span className="crumb-sep">›</span>
              </span>
            ),
          )}
        </nav>
        {data?.truncated && (
          <span className="treemap-note" title="The row limit was reached; smaller items are merged">
            Showing the largest {rows.length.toLocaleString()} items
          </span>
        )}
        <label className="treemap-depth">
          Depth
          <select value={depth} onChange={(e) => onDepthChange(Number(e.target.value))}>
            {TREEMAP_DEPTHS.map((d) => (
              <option key={d} value={d}>{d}</option>
            ))}
          </select>
        </label>
        <button title="Re-fetch at the current size" onClick={() => setRefreshNonce((n) => n + 1)}>
          ⟳ Refresh
        </button>
      </div>

      <div className="treemap-content">
        <div
          ref={bodyRef}
          className="treemap-body"
          onMouseMove={(e) => {
            const tip = tooltipRef.current;
            const box = bodyRef.current?.getBoundingClientRect();
            if (!tip || !box) return;
            // Keep the tooltip inside the pane: flip left/up near the right/bottom edges.
            const x = e.clientX - box.left;
            const y = e.clientY - box.top;
            tip.style.left = `${x + 14 + tip.offsetWidth > box.width ? x - tip.offsetWidth - 10 : x + 14}px`;
            tip.style.top = `${y + 16 + tip.offsetHeight > box.height ? y - tip.offsetHeight - 10 : y + 16}px`;
          }}
          onMouseLeave={() => setHover(null)}
          onClick={(e) => {
            const node = nodeAt(e.clientX, e.clientY);
            if (node) drill(drillTargetId(node, rowsById));
          }}
          onContextMenu={(e) => {
            e.preventDefault();
            e.stopPropagation();
            const row = nodeAt(e.clientX, e.clientY)?.data;
            if (row?.ref && data) onTileMenu(row, fullPathOf(row.nodeId, rowsById, data.rootFullPath), e.clientX, e.clientY);
          }}
        >
          {data && rows.length <= 1 && <div className="treemap-empty">Nothing to show — this folder has no sized entries.</div>}
          {data && definition && size.height > 0 && (
            <Chart
              definition={definition}
              ariaLabel={`Treemap of ${data.rootFullPath}`}
              height={size.height}
              onFocusChange={(point) => setHover((point?.datum as Node | undefined) ?? null)}
              onRender={(ctx) => {
                interactionRef.current = ctx.interaction as unknown as ChartInteractionController<Node>;
              }}
            />
          )}
          <div ref={tooltipRef} className="treemap-tooltip" hidden={!hoverRow}>
            {hoverRow && data && (
              <>
                <div className="tt-path">
                  {hoverRow.mergedCount > 0
                    ? `${fullPathOf(hoverRow.parentId ?? "", rowsById, data.rootFullPath)} — ${hoverRow.name}`
                    : fullPathOf(hoverRow.nodeId, rowsById, data.rootFullPath)}
                </div>
                <div>
                  {toHRString(hoverRow.size)} · {pct(hoverRow.size)} of {rootRow?.name}
                </div>
                {hoverRow.mergedCount > 0 && <div>{hoverRow.mergedCount.toLocaleString()} smaller items merged</div>}
              </>
            )}
          </div>
        </div>

        <ul className="treemap-legend" aria-label="Top-level folders">
          {branches.map((b) => {
            const canDrill = b.isDirectory && b.ref != null;
            return (
              <li
                key={b.nodeId}
                className={`${highlight === b.nodeId ? "active" : ""}${canDrill ? " drill" : ""}`}
                title={canDrill ? "Click to open" : undefined}
                onMouseEnter={() => setHighlight(b.nodeId)}
                onMouseLeave={() => setHighlight(null)}
                onClick={() => canDrill && drill(b.nodeId)}
              >
                <span className="swatch" style={{ background: colors.get(b.nodeId) }} />
                <span className="legend-name">{b.name}</span>
                <span className="legend-size">{toHRString(b.size)}</span>
                <span className="legend-pct">{pct(b.size)}</span>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}
