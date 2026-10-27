import { useEffect, useMemo, useRef, useState } from "react";
import { parseColourRules } from "../colourRules";

interface Props {
  /** Current rules text (held by App so the other tabs restyle live as this changes). */
  text: string;
  onChange: (text: string) => void;
  /** Queue a save. Owned by App so a save flushed on unmount still runs, in order. */
  save: (text: string) => void;
  /** Error from the most recent save, or null. */
  saveError: string | null;
  getDefaults: () => Promise<string>;
}

const AUTOSAVE_MS = 500;

/**
 * Colours tab: a plain monospace editor for the colour rules, a validation list (line number +
 * problem) and a per-rule style preview. Edits apply live via `onChange` and are saved ~500 ms
 * after typing stops; a pending save is flushed when the tab is left.
 */
export function ColoursView({ text, onChange, save, saveError, getDefaults }: Props) {
  const parsed = useMemo(() => parseColourRules(text), [text]);
  const [defaultsError, setDefaultsError] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pending = useRef<string | null>(null);

  function flush() {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    if (pending.current !== null) {
      const value = pending.current;
      pending.current = null;
      save(value);
    }
  }

  function edit(value: string) {
    onChange(value);
    pending.current = value;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(flush, AUTOSAVE_MS);
  }

  // Leaving the tab must not lose an edit made within the debounce window.
  useEffect(() => flush, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function resetToDefaults() {
    try {
      edit(await getDefaults());
      setDefaultsError(null);
    } catch (e) {
      setDefaultsError((e as Error).message);
    }
  }

  return (
    <div className="colours-view">
      <div className="colours-toolbar">
        <span className="colours-hint">
          One rule per line: <code>pattern  style</code>. First match wins. Saved automatically.
        </span>
        <button onClick={() => void resetToDefaults()}>Reset to defaults</button>
        {saveError && <span className="colours-error">Save failed: {saveError}</span>}
        {defaultsError && <span className="colours-error">Reset failed: {defaultsError}</span>}
      </div>
      <textarea
        className="colours-editor"
        value={text}
        spellCheck={false}
        wrap="off"
        onChange={(e) => edit(e.target.value)}
      />
      <div className="colours-report">
        <div className="colours-problems">
          {parsed.problems.length === 0 ? (
            <div className="colours-ok">No problems.</div>
          ) : (
            parsed.problems.map((p, i) => (
              <div key={i} className="colours-problem">
                line {p.line}: {p.message}
              </div>
            ))
          )}
        </div>
        <div className="colours-preview">
          {parsed.rules.map((r) => (
            <div key={r.line} className="colours-preview-row">
              <span className="colours-preview-line">line {r.line}</span>
              <span className="name-text" style={r.style}>{r.isFolder ? "Folder name" : r.pattern}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
