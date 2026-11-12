import { useDeferredValue, useMemo, useRef, useState } from "react";
import type { SearchResultRow } from "../api/types";
import { compileFilter, type CompiledFilter, type FilterField, type FilterMode } from "../resultsFilter";

export function useResultsFilter(rows: SearchResultRow[], resetKey: number) {
  const [text, setText] = useState("");
  const [mode, setMode] = useState<FilterMode>("substring");
  const [field, setField] = useState<FilterField>("name");
  const [seenKey, setSeenKey] = useState(resetKey);
  if (seenKey !== resetKey) {
    setSeenKey(resetKey);
    setText("");
  }

  const current = useMemo(() => compileFilter(text, mode, field), [text, mode, field]);
  const deferredText = useDeferredValue(text);
  const applied = useMemo(() => compileFilter(deferredText, mode, field), [deferredText, mode, field]);
  const lastValid = useRef<CompiledFilter>({ kind: "none" });
  if (applied.kind !== "error") lastValid.current = applied;
  const effective = lastValid.current;

  const shown = useMemo(
    () => (effective.kind === "ok" ? rows.filter(effective.test) : rows),
    [rows, effective],
  );

  return {
    text,
    setText,
    mode,
    setMode,
    field,
    setField,
    shown,
    total: rows.length,
    active: effective.kind === "ok",
    error: current.kind === "error" ? current.message : null,
    searchable: current.kind === "ok" && text !== "",
  };
}

export type ResultsFilterApi = ReturnType<typeof useResultsFilter>;
