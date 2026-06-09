import { useEffect } from "react";

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
  useEffect(() => {
    if (!menu) return;
    const close = () => onClose();
    window.addEventListener("click", close);
    window.addEventListener("contextmenu", close);
    return () => {
      window.removeEventListener("click", close);
      window.removeEventListener("contextmenu", close);
    };
  }, [menu, onClose]);

  if (!menu) return null;

  return (
    <ul className="context-menu" style={{ left: menu.x, top: menu.y }}>
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
