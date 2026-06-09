import { useEffect, useRef, useState } from "react";

interface Props {
  onOpenFolder: () => void;
  onReload: () => void;
}

/** A lightweight native-style menu bar. "File ▸ Open Folder…" picks a catalog directory. */
export function MenuBar({ onOpenFolder, onReload }: Props) {
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

  return (
    <div className="menubar" ref={ref}>
      <button className={`menu-title${open ? " open" : ""}`} onClick={() => setOpen((o) => !o)}>
        File
      </button>
      {open && (
        <ul className="menu-dropdown">
          <li
            onClick={() => {
              setOpen(false);
              onOpenFolder();
            }}
          >
            Open Folder…
          </li>
          <li
            onClick={() => {
              setOpen(false);
              onReload();
            }}
          >
            Reload Catalogs
          </li>
        </ul>
      )}
    </div>
  );
}
