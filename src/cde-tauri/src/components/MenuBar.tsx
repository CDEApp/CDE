import { useEffect, useMemo, useRef, useState } from "react";
import { closeMenus, hoverMenu, keyMenu, toggleMenu, type MenuState } from "../menuModel";

interface Props {
  onNewCatalog: () => void;
  onOpenFolder: () => void;
  onReload: () => void;
  onExit: () => void;
  onAbout: () => void;
  /** When set, Open Folder and Reload are disabled and this is their tooltip (e.g. a rescan is running). */
  loadDisabledReason?: string | null;
}

type MenuEntry =
  | { kind: "item"; label: string; action: () => void; title?: string; disabled?: boolean }
  | { kind: "separator" };

interface MenuDef {
  id: string;
  title: string;
  entries: MenuEntry[];
}

/**
 * A lightweight native-style menu bar: a list of menus (File, About), one open at a time. Escape or an
 * outside click closes; hovering another title while one is open switches to it; Left/Right move between
 * menus. "File ▸ New Catalog…" scans a folder into a new catalog; "Open Folder…" picks a catalog directory.
 */
export function MenuBar({ onNewCatalog, onOpenFolder, onReload, onExit, onAbout, loadDisabledReason }: Props) {
  const [state, setState] = useState<MenuState>(closeMenus());
  const ref = useRef<HTMLDivElement>(null);
  const loadBlocked = !!loadDisabledReason;
  const loadTitle = loadDisabledReason ?? undefined;

  // A disabled item stays in the menu (greyed, with the reason as tooltip) and ignores clicks.
  const menus: MenuDef[] = useMemo(
    () => [
      {
        id: "file",
        title: "File",
        entries: [
          { kind: "item", label: "New Catalog…", action: onNewCatalog, title: "Scan a folder into a new catalog file in the catalog folder" },
          { kind: "item", label: "Open Folder…", action: onOpenFolder, disabled: loadBlocked, title: loadTitle },
          { kind: "item", label: "Reload Catalogs", action: onReload, disabled: loadBlocked, title: loadTitle },
          { kind: "separator" },
          { kind: "item", label: "Exit", action: onExit },
        ],
      },
      {
        id: "about",
        title: "About",
        entries: [{ kind: "item", label: "About CDE…", action: onAbout }],
      },
    ],
    [onNewCatalog, onOpenFolder, onReload, onExit, onAbout, loadBlocked, loadTitle],
  );
  const ids = useMemo(() => menus.map((m) => m.id), [menus]);

  const isOpen = state.openId !== null;
  useEffect(() => {
    if (!isOpen) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setState(closeMenus());
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" || e.key === "ArrowLeft" || e.key === "ArrowRight") {
        e.preventDefault();
        setState((s) => keyMenu(s, e.key, ids));
      }
    };
    const id = window.setTimeout(() => {
      window.addEventListener("mousedown", onDown, true);
      window.addEventListener("keydown", onKey);
    }, 0);
    return () => {
      window.clearTimeout(id);
      window.removeEventListener("mousedown", onDown, true);
      window.removeEventListener("keydown", onKey);
    };
  }, [isOpen, ids]);

  return (
    <div className="menubar" ref={ref}>
      {menus.map((menu) => {
        const open = state.openId === menu.id;
        return (
          <div className="menu" key={menu.id}>
            <button
              className={`menu-title${open ? " open" : ""}`}
              aria-haspopup="menu"
              aria-expanded={open}
              onClick={() => setState((s) => toggleMenu(s, menu.id))}
              onMouseEnter={() => setState((s) => hoverMenu(s, menu.id))}
            >
              {menu.title}
            </button>
            {open && (
              <ul className="menu-dropdown" role="menu">
                {menu.entries.map((entry, i) =>
                  entry.kind === "separator" ? (
                    <li key={i} className="menu-separator" role="separator" />
                  ) : (
                    <li
                      key={i}
                      role="menuitem"
                      className={entry.disabled ? "disabled" : undefined}
                      title={entry.title}
                      aria-disabled={!!entry.disabled}
                      onClick={() => {
                        if (entry.disabled) return;
                        setState(closeMenus());
                        entry.action();
                      }}
                    >
                      {entry.label}
                    </li>
                  ),
                )}
              </ul>
            )}
          </div>
        );
      })}
    </div>
  );
}
