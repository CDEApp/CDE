import { useEffect, useId, useRef, useState } from "react";

interface Props {
  value: string;
  onChange: (value: string) => void;
  /** Enter pressed in the text box while the history list is closed. */
  onEnter: () => void;
  /** Previous patterns, most recent first. */
  history: string[];
  placeholder?: string;
}

/**
 * Typeable combo box for the search pattern: a free-text input plus a ▾ button that drops down the
 * recent-pattern history. Down/Alt+Down opens the list, arrows move, Enter picks (without
 * searching), Escape or leaving the box closes it. Picking only fills the box; Enter then searches.
 */
export function PatternCombo({ value, onChange, onEnter, history, placeholder }: Props) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const listId = useId();

  const canOpen = history.length > 0;

  function openList() {
    if (!canOpen) return;
    setActive(Math.max(0, history.indexOf(value)));
    setOpen(true);
  }

  function pick(item: string) {
    onChange(item);
    setOpen(false);
    inputRef.current?.focus();
  }

  // Keep the highlighted item visible while arrowing through a scrolled list.
  useEffect(() => {
    if (!open || active < 0) return;
    (listRef.current?.children[active] as HTMLElement | undefined)?.scrollIntoView({ block: "nearest" });
  }, [open, active]);

  function onKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (!open) {
      if (e.key === "ArrowDown") {
        e.preventDefault();
        openList();
      } else if (e.key === "Enter") {
        onEnter();
      }
      return;
    }

    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        if (e.altKey) setOpen(false);
        else setActive((i) => Math.min(history.length - 1, i + 1));
        break;
      case "ArrowUp":
        e.preventDefault();
        setActive((i) => Math.max(0, i - 1));
        break;
      case "Home":
      case "End":
        e.preventDefault();
        setActive(e.key === "Home" ? 0 : history.length - 1);
        break;
      case "Enter":
        e.preventDefault();
        if (active >= 0 && active < history.length) pick(history[active]);
        else setOpen(false);
        break;
      case "Escape":
        e.preventDefault();
        setOpen(false);
        break;
      case "Tab":
        setOpen(false);
        break;
    }
  }

  return (
    <div className="pattern-combo">
      <input
        ref={inputRef}
        className="pattern"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="none"
        aria-activedescendant={open && active >= 0 ? `${listId}-${active}` : undefined}
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={onKeyDown}
        onBlur={() => setOpen(false)}
      />
      <button
        type="button"
        className="combo-toggle"
        tabIndex={-1}
        aria-label="Previous searches"
        title="Previous searches"
        disabled={!canOpen}
        // Keep focus in the text box so its blur doesn't immediately close the list.
        onMouseDown={(e) => e.preventDefault()}
        onClick={() => {
          if (open) setOpen(false);
          else openList();
          inputRef.current?.focus();
        }}
      >
        ▾
      </button>
      {open && (
        <ul ref={listRef} id={listId} role="listbox" className="combo-list">
          {history.map((h, i) => (
            <li
              key={h}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === active}
              className={i === active ? "active" : undefined}
              title={h}
              onMouseDown={(e) => e.preventDefault()}
              onMouseEnter={() => setActive(i)}
              onClick={() => pick(h)}
            >
              {h}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
