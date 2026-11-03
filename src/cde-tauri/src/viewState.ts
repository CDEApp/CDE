import type { EntryRef } from "./api/types";

export type PlainView = "directory" | "results" | "catalogs" | "colours" | "diff";

export interface TreemapTab {
  request: { ref: EntryRef; nonce: number };
  depth: number | null;
}

export type ViewState =
  | { kind: PlainView; treemap: TreemapTab | null }
  | { kind: "treemap"; treemap: TreemapTab };

export type ViewKind = ViewState["kind"];

export const initialView: ViewState = { kind: "directory", treemap: null };

export function showView(state: ViewState, kind: PlainView): ViewState {
  return { kind, treemap: state.treemap };
}

export function selectTreemap(state: ViewState): ViewState {
  return state.treemap ? { kind: "treemap", treemap: state.treemap } : state;
}

export function showTreemap(state: ViewState, ref: EntryRef): ViewState {
  const nonce = (state.treemap?.request.nonce ?? 0) + 1;
  return { kind: "treemap", treemap: { request: { ref, nonce }, depth: state.treemap?.depth ?? null } };
}

export function setTreemapDepth(state: ViewState, depth: number): ViewState {
  if (!state.treemap) return state;
  return { ...state, treemap: { ...state.treemap, depth } } as ViewState;
}

export function dropTreemap(state: ViewState): ViewState {
  return { kind: state.kind === "treemap" ? "directory" : state.kind, treemap: null };
}
