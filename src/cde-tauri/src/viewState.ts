import type { EntryRef } from "./api/types";

export type PlainView = "directory" | "results" | "catalogs" | "colours" | "diff";

export interface TreemapRequest {
  ref: EntryRef;
  nonce: number;
}

export type ViewState = { treemapDepth: number | null } & (
  | { kind: PlainView; treemap: TreemapRequest | null }
  | { kind: "treemap"; treemap: TreemapRequest }
);

export type ViewKind = ViewState["kind"];

export const initialView: ViewState = { kind: "directory", treemap: null, treemapDepth: null };

export function showView(state: ViewState, kind: PlainView): ViewState {
  return { kind, treemap: state.treemap, treemapDepth: state.treemapDepth };
}

export function selectTreemap(state: ViewState): ViewState {
  return state.treemap ? { kind: "treemap", treemap: state.treemap, treemapDepth: state.treemapDepth } : state;
}

export function showTreemap(state: ViewState, ref: EntryRef): ViewState {
  const nonce = (state.treemap?.nonce ?? 0) + 1;
  return { kind: "treemap", treemap: { ref, nonce }, treemapDepth: state.treemapDepth };
}

export function setTreemapDepth(state: ViewState, treemapDepth: number): ViewState {
  return { ...state, treemapDepth };
}

export function dropTreemap(state: ViewState): ViewState {
  return { kind: state.kind === "treemap" ? "directory" : state.kind, treemap: null, treemapDepth: state.treemapDepth };
}
