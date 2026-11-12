import { useEffect, useRef, type KeyboardEvent as ReactKeyboardEvent } from "react";

export interface MenuItem {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  /** Tooltip, e.g. why the item is disabled. */
  title?: string;
  separator?: boolean;
  /** Renders a check mark (true) or a blank (false) before the label; omit for a plain item. */
  checked?: boolean;
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

  // Keyboard users land on the first item, and focus goes back to whatever opened the menu when it closes.
  useEffect(() => {
    if (!menu) return;
    const opener = document.activeElement as HTMLElement | null;
    ref.current?.querySelector<HTMLElement>('[role^="menuitem"]')?.focus();
    return () => {
      if (document.activeElement === document.body || document.activeElement === null) opener?.focus();
    };
  }, [menu]);

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

  function activate(item: MenuItem) {
    if (item.disabled) return;
    item.onClick();
    onClose();
  }

  function onKeyDown(e: ReactKeyboardEvent<HTMLUListElement>) {
    const items = Array.from(ref.current?.querySelectorAll<HTMLElement>('[role^="menuitem"]') ?? []);
    const at = items.indexOf(document.activeElement as HTMLElement);
    const focus = (i: number) => items[(i + items.length) % items.length]?.focus();
    if (e.key === "ArrowDown") focus(at + 1);
    else if (e.key === "ArrowUp") focus(at < 0 ? -1 : at - 1);
    else if (e.key === "Home") focus(0);
    else if (e.key === "End") focus(-1);
    else if (e.key === "Tab") onClose();
    else if (e.key === "Enter" || e.key === " ") {
      const index = Number((document.activeElement as HTMLElement | null)?.dataset.index);
      if (Number.isInteger(index)) activate(menu!.items[index]);
    } else return;
    e.preventDefault();
  }

  // Keep the menu inside the viewport (rough width/height estimate is fine for clamping).
  const left = Math.min(menu.x, window.innerWidth - 200);
  const top = Math.min(menu.y, window.innerHeight - menu.items.length * 26 - 10);

  return (
    <ul
      ref={ref}
      role="menu"
      className="context-menu"
      style={{ left: Math.max(0, left), top: Math.max(0, top) }}
      onKeyDown={onKeyDown}
    >
      {menu.items.map((item, i) =>
        item.separator ? (
          <li key={i} role="separator" className="separator" />
        ) : (
          <li
            key={i}
            data-index={i}
            tabIndex={-1}
            className={item.disabled ? "disabled" : ""}
            title={item.title}
            role={item.checked === undefined ? "menuitem" : "menuitemcheckbox"}
            aria-checked={item.checked}
            aria-disabled={item.disabled || undefined}
            onClick={() => activate(item)}
          >
            {item.checked !== undefined && <span className="menu-check">{item.checked ? "✓" : ""}</span>}
            {item.label}
          </li>
        ),
      )}
    </ul>
  );
}
