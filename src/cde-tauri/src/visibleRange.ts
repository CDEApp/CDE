/**
 * Fixed-row-height windowing: the half-open row range [start, end) to mount for a scroll position,
 * widened by `overscan` rows each side and clamped to the list. Pure, so it is unit-tested in Node.
 */
export function visibleRange(p: {
  scrollTop: number;
  viewport: number;
  rowHeight: number;
  total: number;
  overscan: number;
}): { start: number; end: number } {
  const start = Math.max(0, Math.floor(p.scrollTop / p.rowHeight) - p.overscan);
  const end = Math.min(p.total, Math.ceil((p.scrollTop + p.viewport) / p.rowHeight) + p.overscan);
  return { start: Math.min(start, end), end };
}

/**
 * The scrollTop that shows row `index`: `nearest` scrolls only as far as needed (no change when the
 * row is fully visible, like scrollIntoView's `nearest`); `center` puts it mid-viewport.
 */
export function scrollTopFor(p: {
  index: number;
  rowHeight: number;
  viewport: number;
  scrollTop: number;
  align: "nearest" | "center";
}): number {
  const top = p.index * p.rowHeight;
  if (p.align === "center") return Math.max(0, top - (p.viewport - p.rowHeight) / 2);
  if (top < p.scrollTop) return top;
  if (top + p.rowHeight > p.scrollTop + p.viewport) return top + p.rowHeight - p.viewport;
  return p.scrollTop;
}
