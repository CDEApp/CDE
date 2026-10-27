import { CSSProperties, ReactNode, Ref, useEffect, useImperativeHandle, useRef, useState } from "react";
import { scrollTopFor, visibleRange } from "../visibleRange";

export interface VirtualListHandle {
  /** Scroll row `index` into view: `nearest` only if it is off screen, `center` mid-viewport. */
  scrollToIndex: (index: number, align: "nearest" | "center") => void;
}

interface Props<T> {
  items: T[];
  rowHeight: number;
  header?: ReactNode;
  /** Render one row. Spread `style` onto the row element (it positions the row absolutely). */
  renderRow: (item: T, index: number, style: CSSProperties) => ReactNode;
  className?: string;
  overscan?: number;
  /** Extra style for the inner spacer — e.g. `minWidth` to drive horizontal scroll when
   *  fixed-width columns exceed the viewport. Both header and rows then share that scroll. */
  innerStyle?: CSSProperties;
  handleRef?: Ref<VirtualListHandle>;
  /** Optional ARIA role (e.g. "grid") and label for the list root. */
  role?: string;
  ariaLabel?: string;
}

/**
 * Fixed-row-height virtual list. Only the rows intersecting the viewport (plus an overscan margin)
 * are mounted; a spacer of `items.length * rowHeight` drives the scrollbar. Keeps the DOM tiny and
 * constant regardless of result count (10 rows or 1,000,000).
 */
export function VirtualList<T>({
  items, rowHeight, header, renderRow, className, overscan = 8, innerStyle, handleRef, role, ariaLabel,
}: Props<T>) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const innerRef = useRef<HTMLDivElement>(null);
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

  useImperativeHandle(
    handleRef,
    () => ({
      scrollToIndex(index, align) {
        const el = scrollRef.current;
        if (!el) return;
        // Rows start below the (sticky) header, which also covers the top of the viewport.
        const offset = innerRef.current?.offsetTop ?? 0;
        el.scrollTop =
          offset +
          scrollTopFor({ index, rowHeight, viewport: el.clientHeight - offset, scrollTop: el.scrollTop - offset, align });
      },
    }),
    [rowHeight],
  );

  const { start, end } = visibleRange({ scrollTop, viewport, rowHeight, total: items.length, overscan });

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
    <div className={`vlist ${className ?? ""}`} role={role} aria-label={ariaLabel}>
      <div
        className="vlist-scroll"
        ref={scrollRef}
        onScroll={(e) => setScrollTop(e.currentTarget.scrollTop)}
      >
        {/* Header lives inside the scroll area (sticky) so it scrolls horizontally with the rows
            when columns are wider than the viewport, while staying pinned vertically. */}
        {header}
        <div className="vlist-inner" ref={innerRef} style={{ height: items.length * rowHeight, ...innerStyle }}>
          {rows}
        </div>
      </div>
    </div>
  );
}
