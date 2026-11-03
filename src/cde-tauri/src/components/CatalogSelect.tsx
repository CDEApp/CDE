import { useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties, type KeyboardEvent } from "react";
import type { CatalogInfo } from "../api/types";
import { catalogFile, catalogName } from "../format";

interface Props {
  label: string;
  catalogs: CatalogInfo[];
  value: number | null;
  onChange: (catalogId: number | null) => void;
}

const PLACEHOLDER = "Select a catalog…";
const VIEWPORT_MARGIN = 8;

const nameOf = (c: CatalogInfo) => catalogName(catalogFile(c));

/** Closed-control text: `rootPath [name]`, with the bracket part omitted when the catalog has no file name. */
function summary(c: CatalogInfo): string {
  const name = nameOf(c);
  return name ? `${c.rootPath} [${name}]` : c.rootPath;
}

/**
 * A listbox dropdown standing in for a native select, because an `<option>` cannot lay out the root path
 * flush left and the catalog name flush right. `catalogs` is shown in the order given.
 */
export function CatalogSelect({ label, catalogs, value, onChange }: Props) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const [popupStyle, setPopupStyle] = useState<CSSProperties>({});
  const root = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const popup = useRef<HTMLUListElement>(null);
  const listId = useId();

  const selected = catalogs.find((c) => c.catalogId === value) ?? null;
  // Row 0 is the placeholder (clears the selection); catalog rows follow.
  const ids: (number | null)[] = [null, ...catalogs.map((c) => c.catalogId)];

  const close = () => {
    setOpen(false);
    button.current?.focus();
  };
  const choose = (index: number) => {
    onChange(ids[index]);
    close();
  };
  const openList = () => {
    setActive(Math.max(0, ids.indexOf(value)));
    setOpen(true);
  };

  // At least as wide as the control, at most the viewport, and shifted left if it would run off the right edge.
  useLayoutEffect(() => {
    if (!open || !button.current || !popup.current) return;
    const rect = button.current.getBoundingClientRect();
    const maxWidth = window.innerWidth - 2 * VIEWPORT_MARGIN;
    const width = Math.min(popup.current.offsetWidth, maxWidth);
    const left = Math.max(VIEWPORT_MARGIN, Math.min(rect.left, window.innerWidth - VIEWPORT_MARGIN - width));
    setPopupStyle({ top: rect.bottom, left, minWidth: rect.width, maxWidth });
  }, [open, catalogs]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (!root.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  useEffect(() => {
    if (open) popup.current?.children[active]?.scrollIntoView?.({ block: "nearest" });
  }, [open, active]);

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key === "Escape" && open) {
      e.preventDefault();
      close();
    } else if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (!open) return openList();
      const step = e.key === "ArrowDown" ? 1 : -1;
      setActive((a) => Math.min(ids.length - 1, Math.max(0, a + step)));
    } else if (e.key === "Home" || e.key === "End") {
      if (!open) return;
      e.preventDefault();
      setActive(e.key === "Home" ? 0 : ids.length - 1);
    } else if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      if (open) choose(active);
      else openList();
    } else if (e.key === "Tab" && open) {
      setOpen(false);
    }
  };

  return (
    <div className="catalog-select" ref={root} onKeyDown={onKeyDown}>
      <button
        type="button"
        ref={button}
        className="catalog-select-button"
        aria-label={`${label} catalog`}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        onClick={() => (open ? close() : openList())}
      >
        <span className="catalog-select-value">{selected ? summary(selected) : PLACEHOLDER}</span>
        <span className="catalog-select-caret" aria-hidden="true">▾</span>
      </button>
      {open && (
        <ul
          id={listId}
          ref={popup}
          className="catalog-select-list"
          role="listbox"
          aria-label={`${label} catalogs`}
          style={popupStyle}
        >
          <li
            role="option"
            aria-selected={value === null}
            className={`catalog-select-option${active === 0 ? " active" : ""}`}
            onMouseEnter={() => setActive(0)}
            onClick={() => choose(0)}
          >
            <span className="catalog-select-path">{PLACEHOLDER}</span>
          </li>
          {catalogs.map((c, i) => {
            const name = nameOf(c);
            return (
              <li
                key={c.catalogId}
                role="option"
                aria-selected={c.catalogId === value}
                className={`catalog-select-option${active === i + 1 ? " active" : ""}`}
                onMouseEnter={() => setActive(i + 1)}
                onClick={() => choose(i + 1)}
              >
                <span className="catalog-select-path">{c.rootPath}</span>
                {name && <span className="catalog-select-name">[{name}]</span>}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
