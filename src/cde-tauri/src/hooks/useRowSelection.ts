import { useReducer, useState } from "react";
import { EMPTY_SELECTION, selectionReducer } from "../rowSelection";

/**
 * Row selection for the search results. `resetKey` is the search epoch: when it changes, the
 * selection is cleared in the same render, so the new results never show the old selection.
 */
export function useRowSelection(resetKey: number) {
  const [selection, dispatch] = useReducer(selectionReducer, EMPTY_SELECTION);
  const [seenKey, setSeenKey] = useState(resetKey);
  if (seenKey !== resetKey) {
    setSeenKey(resetKey);
    dispatch({ type: "clear" });
  }
  return { selection, dispatch };
}

export type RowSelectionApi = ReturnType<typeof useRowSelection>;
