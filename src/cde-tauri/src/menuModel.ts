/** Pure open/close state for a menu bar holding several menus; at most one is open. */
export interface MenuState {
  openId: string | null;
}

export const closeMenus = (): MenuState => ({ openId: null });

/** Clicking a title opens its menu (closing any other) or closes it if already open. */
export function toggleMenu(state: MenuState, id: string): MenuState {
  return { openId: state.openId === id ? null : id };
}

/** Hovering a title switches menus, but only while one is already open (standard menu bars). */
export function hoverMenu(state: MenuState, id: string): MenuState {
  return state.openId === null || state.openId === id ? state : { openId: id };
}

/** Escape closes; Left/Right move between menus while one is open (wrapping). */
export function keyMenu(state: MenuState, key: string, ids: readonly string[] = []): MenuState {
  if (state.openId === null) return state;
  if (key === "Escape") return closeMenus();
  if ((key === "ArrowRight" || key === "ArrowLeft") && ids.length > 0) {
    const i = ids.indexOf(state.openId);
    if (i < 0) return state;
    const step = key === "ArrowRight" ? 1 : ids.length - 1;
    return { openId: ids[(i + step) % ids.length] };
  }
  return state;
}
