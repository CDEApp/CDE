/**
 * Class for a view that stays mounted while another tab is active (the Directory view, issue #80).
 * Hidden panes use `visibility: hidden` + absolute positioning (see styles.css), NOT `display: none`:
 * they keep their layout box, so the virtualized lists keep their measured viewport and scrollTop
 * instead of seeing 0x0 and resetting.
 */
export function viewPaneClass<V extends string>(pane: V, active: V): string {
  return pane === active ? "view-pane" : "view-pane view-pane-hidden";
}
