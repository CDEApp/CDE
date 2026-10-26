import { useEffect, useRef } from "react";

export interface MenuItem {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  separator?: boolean;
}

export interface MenuState {
  x: number;
  y: number;
  items: MenuItem[];
}

interface Props {
  menu: MenuState | null;
  onClose: () => void;
}

export function ContextMenu({ menu, onClose }: Props) {
  const ref = useRef<HTMLUListElement>(null);

  useEffect(() => {
    if (!menu) return;

    const onDown = (e: MouseEvent) => {
      // Clicks (left or right) outside the menu dismiss it; clicks inside are handled by the item.
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };

    // Attach on the next tick so the right-click that opened this menu cannot immediately close it.
    const id = window.setTimeout(() => {
      window.addEventListener("mousedown", onDown, true);
      window.addEventListener("keydown", onKey);
      window.addEventListener("resize", onClose);
    }, 0);

    return () => {
      window.clearTimeout(id);
      window.removeEventListener("mousedown", onDown, true);
      window.removeEventListener("keydown", onKey);
      window.removeEventListener("resize", onClose);
    };
  }, [menu, onClose]);

  if (!menu) return null;

  // Keep the menu inside the viewport (rough width/height estimate is fine for clamping).
  const left = Math.min(menu.x, window.innerWidth - 200);
  const top = Math.min(menu.y, window.innerHeight - menu.items.length * 26 - 10);

  return (
    <ul ref={ref} className="context-menu" style={{ left: Math.max(0, left), top: Math.max(0, top) }}>
      {menu.items.map((item, i) =>
        item.separator ? (
          <li key={i} className="separator" />
        ) : (
          <li
            key={i}
            className={item.disabled ? "disabled" : ""}
            onClick={() => {
              if (!item.disabled) {
                item.onClick();
                onClose();
              }
            }}
          >
            {item.label}
          </li>
        ),
      )}
    </ul>
  );
}
