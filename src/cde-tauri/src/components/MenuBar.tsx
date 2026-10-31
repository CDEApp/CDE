import { useEffect, useMemo, useRef, useState } from "react";
import { closeMenus, hoverMenu, keyMenu, toggleMenu, type MenuState } from "../menuModel";

interface Props {
  onNewCatalog: () => void;
  onOpenFolder: () => void;
  onReload: () => void;
  onExit: () => void;
  onAbout: () => void;
  onSearchSyntax: () => void;
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
 * A lightweight native-style menu bar: a list of menus (File, Help), one open at a time. Escape or an
 * outside click closes; hovering another title while one is open switches to it; Left/Right move between
 * menus. "File ▸ New Catalog…" scans a folder into a new catalog; "Open Folder…" picks a catalog directory.
 */
export function MenuBar({ onNewCatalog, onOpenFolder, onReload, onExit, onAbout, onSearchSyntax, loadDisabledReason }: Props) {
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
        id: "help",
        title: "Help",
        entries: [
          { kind: "item", label: "Search Syntax…", action: onSearchSyntax, title: "Operators, modifiers and filters for Everything search" },
          { kind: "separator" },
          { kind: "item", label: "About CDE…", action: onAbout },
        ],
      },
    ],
    [onNewCatalog, onOpenFolder, onReload, onExit, onAbout, onSearchSyntax, loadBlocked, loadTitle],
  );
  // Latest ids live in a ref so the window listeners below subscribe only when a menu opens or closes,
  // not on every App render (inline handler props change identity each render).
  const idsRef = useRef<string[]>([]);
  idsRef.current = menus.map((m) => m.id);

  const isOpen = state.openId !== null;
  useEffect(() => {
    if (!isOpen) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setState(closeMenus());
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        setState((s) => keyMenu(s, e.key));
      } else if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
        // Only when focus is inside the menu bar, so carets in text inputs keep working.
        if (!ref.current?.contains(document.activeElement)) return;
        e.preventDefault();
        setState((s) => keyMenu(s, e.key, idsRef.current));
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
  }, [isOpen]);

  return (
    <div className="menubar" ref={ref}>
      {menus.map((menu) => {
        const open = state.openId === menu.id;
        return (
          <div className="menu" key={menu.id}>
            <button
              className={`menu-title${open ? " open" : ""}`}
              aria-expanded={open}
              onClick={() => setState((s) => toggleMenu(s, menu.id))}
              onMouseEnter={() => setState((s) => hoverMenu(s, menu.id))}
            >
              {menu.title}
            </button>
            {open && (
              <ul className="menu-dropdown">
                {menu.entries.map((entry, i) =>
                  entry.kind === "separator" ? (
                    <li key={i} className="menu-separator" role="separator" />
                  ) : (
                    <li
                      key={i}
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
