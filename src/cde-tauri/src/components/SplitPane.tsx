import { ReactNode, useCallback, useEffect, useRef, useState } from "react";

interface Props {
  left: ReactNode;
  right: ReactNode;
  initialLeftWidth?: number;
  minLeft?: number;
  minRight?: number;
  /** Called once when a drag ends, with the final left-pane width (for persistence). */
  onResizeEnd?: (width: number) => void;
}

const KEY_STEP = 16;
const KEY_STEP_LARGE = 64;

/** Two horizontal panes with a draggable divider that resizes them. */
export function SplitPane({
  left,
  right,
  initialLeftWidth = 320,
  minLeft = 150,
  minRight = 200,
  onResizeEnd,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [leftWidth, setLeftWidth] = useState(initialLeftWidth);
  const widthRef = useRef(initialLeftWidth);
  const draggingRef = useRef(false);

  const [containerWidth, setContainerWidth] = useState(0);
  const maxLeft = containerWidth > 0 ? Math.max(minLeft, containerWidth - minRight) : undefined;

  useEffect(() => {
    const el = containerRef.current;
    if (!el || typeof ResizeObserver === "undefined") return;
    const measure = () => setContainerWidth(el.getBoundingClientRect().width);
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  // Keyboard resizing for the splitter (WAI-ARIA window splitter): arrows nudge, Shift takes a big
  // step, Home/End jump to the limits. Each change is reported like the end of a drag.
  const onKeyDown = (e: React.KeyboardEvent) => {
    const step = e.shiftKey ? KEY_STEP_LARGE : KEY_STEP;
    let target: number;
    switch (e.key) {
      case "ArrowLeft": target = widthRef.current - step; break;
      case "ArrowRight": target = widthRef.current + step; break;
      case "Home": target = minLeft; break;
      case "End": target = maxLeft ?? widthRef.current; break;
      default: return;
    }
    e.preventDefault();
    const w = Math.max(minLeft, Math.min(target, maxLeft ?? Infinity));
    if (w === widthRef.current) return;
    widthRef.current = w;
    setLeftWidth(w);
    onResizeEnd?.(w);
  };

  const onMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    draggingRef.current = true;
    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";
  }, []);

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!draggingRef.current || !containerRef.current) return;
      const rect = containerRef.current.getBoundingClientRect();
      const w = Math.max(minLeft, Math.min(e.clientX - rect.left, rect.width - minRight));
      widthRef.current = w;
      setLeftWidth(w);
    };
    const onUp = () => {
      if (!draggingRef.current) return;
      draggingRef.current = false;
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
      onResizeEnd?.(widthRef.current);
    };
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
    return () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
  }, [minLeft, minRight, onResizeEnd]);

  return (
    <div
      className="split"
      ref={containerRef}
      style={{ gridTemplateColumns: `${leftWidth}px 6px 1fr` }}
    >
      <div className="pane tree-pane">{left}</div>
      <div
        className="splitter"
        role="separator"
        aria-orientation="vertical"
        aria-label="Resize panes"
        aria-valuenow={Math.round(leftWidth)}
        aria-valuemin={minLeft}
        aria-valuemax={maxLeft === undefined ? undefined : Math.round(maxLeft)}
        tabIndex={0}
        onMouseDown={onMouseDown}
        onKeyDown={onKeyDown}
        title="Drag or use the arrow keys to resize"
      />
      <div className="pane list-pane">{right}</div>
    </div>
  );
}
