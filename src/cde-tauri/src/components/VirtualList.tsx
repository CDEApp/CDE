import { CSSProperties, ReactNode, useEffect, useRef, useState } from "react";

interface Props<T> {
  items: T[];
  rowHeight: number;
  header: ReactNode;
  /** Render one row. Spread `style` onto the row element (it positions the row absolutely). */
  renderRow: (item: T, index: number, style: CSSProperties) => ReactNode;
  className?: string;
  overscan?: number;
  /** Extra style for the inner spacer — e.g. `minWidth` to drive horizontal scroll when
   *  fixed-width columns exceed the viewport. Both header and rows then share that scroll. */
  innerStyle?: CSSProperties;
}

/**
 * Fixed-row-height virtual list. Only the rows intersecting the viewport (plus an overscan margin)
 * are mounted; a spacer of `items.length * rowHeight` drives the scrollbar. Keeps the DOM tiny and
 * constant regardless of result count (10 rows or 1,000,000).
 */
export function VirtualList<T>({ items, rowHeight, header, renderRow, className, overscan = 8, innerStyle }: Props<T>) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [viewport, setViewport] = useState(0);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const measure = () => setViewport(el.clientHeight);
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const total = items.length;
  const start = Math.max(0, Math.floor(scrollTop / rowHeight) - overscan);
  const end = Math.min(total, Math.ceil((scrollTop + viewport) / rowHeight) + overscan);

  const rows: ReactNode[] = [];
  for (let i = start; i < end; i++) {
    rows.push(
      renderRow(items[i], i, {
        position: "absolute",
        top: i * rowHeight,
        left: 0,
        right: 0,
        height: rowHeight,
      }),
    );
  }

  return (
    <div className={`vlist ${className ?? ""}`}>
      <div
        className="vlist-scroll"
        ref={scrollRef}
        onScroll={(e) => setScrollTop(e.currentTarget.scrollTop)}
      >
        {/* Header lives inside the scroll area (sticky) so it scrolls horizontally with the rows
            when columns are wider than the viewport, while staying pinned vertically. */}
        {header}
        <div className="vlist-inner" style={{ height: total * rowHeight, ...innerStyle }}>
          {rows}
        </div>
      </div>
    </div>
  );
}
