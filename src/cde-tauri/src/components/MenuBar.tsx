import { useEffect, useRef, useState } from "react";

interface Props {
  onOpenFolder: () => void;
  onReload: () => void;
  onExit: () => void;
  /** When set, Open Folder and Reload are disabled and this is their tooltip (e.g. a rescan is running). */
  loadDisabledReason?: string | null;
}

/** A lightweight native-style menu bar. "File ▸ Open Folder…" picks a catalog directory. */
export function MenuBar({ onOpenFolder, onReload, onExit, loadDisabledReason }: Props) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
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
  }, [open]);

  const loadBlocked = !!loadDisabledReason;
  // A disabled item stays in the menu (greyed, with the reason as tooltip) and ignores clicks.
  const loadItem = (label: string, action: () => void) => (
    <li
      className={loadBlocked ? "disabled" : undefined}
      title={loadDisabledReason ?? undefined}
      aria-disabled={loadBlocked}
      onClick={() => {
        if (loadBlocked) return;
        setOpen(false);
        action();
      }}
    >
      {label}
    </li>
  );

  return (
    <div className="menubar" ref={ref}>
      <button className={`menu-title${open ? " open" : ""}`} onClick={() => setOpen((o) => !o)}>
        File
      </button>
      {open && (
        <ul className="menu-dropdown">
          {loadItem("Open Folder…", onOpenFolder)}
          {loadItem("Reload Catalogs", onReload)}
          <li className="menu-separator" role="separator" />
          <li
            onClick={() => {
              setOpen(false);
              onExit();
            }}
          >
            Exit
          </li>
        </ul>
      )}
    </div>
  );
}
