import { useReducer, useState } from "react";
import { EMPTY_SELECTION, selectionReducer } from "../rowSelection";

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
