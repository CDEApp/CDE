export interface RowSelection {
  readonly selected: ReadonlySet<string>;
  readonly active: string | null;
  readonly anchor: string | null;
}

export const EMPTY_SELECTION: RowSelection = { selected: new Set(), active: null, anchor: null };

export type ClickMode = "replace" | "toggle" | "range";

export type SelectionAction =
  | { type: "click"; key: string; mode: ClickMode; order: readonly string[] }
  | { type: "move"; to: { delta: number } | "start" | "end"; extend: boolean; order: readonly string[] }
  | { type: "selectAll"; order: readonly string[] }
  | { type: "toggleActive" }
  | { type: "clear" };

function only(key: string): RowSelection {
  return { selected: new Set([key]), active: key, anchor: key };
}

function toggled(selected: ReadonlySet<string>, key: string): Set<string> {
  const next = new Set(selected);
  if (!next.delete(key)) next.add(key);
  return next;
}

function range(order: readonly string[], from: number, to: number): Set<string> {
  const [lo, hi] = from <= to ? [from, to] : [to, from];
  return new Set(order.slice(lo, hi + 1));
}

function extendTo(state: RowSelection, order: readonly string[], key: string): RowSelection {
  const anchorIndex = state.anchor === null ? -1 : order.indexOf(state.anchor);
  const targetIndex = order.indexOf(key);
  if (anchorIndex < 0 || targetIndex < 0) return only(key);
  return { selected: range(order, anchorIndex, targetIndex), active: key, anchor: state.anchor };
}

export function selectionReducer(state: RowSelection, action: SelectionAction): RowSelection {
  switch (action.type) {
    case "click":
      if (action.mode === "toggle") {
        return { selected: toggled(state.selected, action.key), active: action.key, anchor: action.key };
      }
      return action.mode === "range" ? extendTo(state, action.order, action.key) : only(action.key);

    case "move": {
      const { order } = action;
      if (order.length === 0) return state;
      const last = order.length - 1;
      const current = state.active === null ? -1 : order.indexOf(state.active);
      let target: number;
      if (action.to === "start") target = 0;
      else if (action.to === "end") target = last;
      else if (current < 0) target = action.to.delta > 0 ? 0 : last;
      else target = Math.min(last, Math.max(0, current + action.to.delta));
      const key = order[target];
      if (!action.extend) return only(key);
      const anchored = state.anchor !== null && order.includes(state.anchor);
      return extendTo(anchored ? state : { ...state, anchor: state.active ?? key }, order, key);
    }

    case "selectAll":
      if (action.order.length === 0) return state;
      return {
        selected: new Set(action.order),
        active: state.active ?? action.order[0],
        anchor: state.anchor ?? action.order[0],
      };

    case "toggleActive":
      if (state.active === null) return state;
      return { selected: toggled(state.selected, state.active), active: state.active, anchor: state.active };

    case "clear":
      return state.selected.size === 0 && state.active === null ? state : EMPTY_SELECTION;
  }
}

export function selectedInOrder<T>(
  visible: readonly T[],
  keyOf: (item: T) => string,
  selection: RowSelection,
): T[] {
  if (selection.selected.size === 0) return [];
  return visible.filter((item) => selection.selected.has(keyOf(item)));
}
