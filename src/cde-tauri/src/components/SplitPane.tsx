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
      <div className="splitter" onMouseDown={onMouseDown} title="Drag to resize" />
      <div className="pane list-pane">{right}</div>
    </div>
  );
}
