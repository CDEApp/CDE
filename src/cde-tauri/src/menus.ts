import type { MenuItem } from "./components/ContextMenu";
import type { DirectoryNode, EntryRef, ShellAction } from "./api/types";
import { rescanMenuItem, type RescanState } from "./rescan";

export interface ShellCommand {
  id: number;
  label: string;
}

export interface EntryMenuContext {
  commands: readonly ShellCommand[];
  runShell: (ref: EntryRef, action: ShellAction, commandId?: number) => void;
  showTreemap: (ref: EntryRef) => void;
  copyPath: (fullPath: string) => void;
  rescanFor: (catalogId: number) => MenuItem;
}

export interface MenuEntry {
  ref: EntryRef;
  fullPath: string;
  isDirectory: boolean;
  onViewInTree?: () => void;
}

export function collapseAllItem(onCollapseAll: () => void): MenuItem {
  return { label: "Collapse all", onClick: onCollapseAll };
}

export function rescanItem(
  rescans: RescanState,
  catalogId: number,
  rootPath: string | undefined,
  onRescan: (catalogId: number) => void,
): MenuItem {
  const m = rescanMenuItem(rescans, catalogId);
  return {
    label: m.label,
    disabled: m.disabled,
    title: m.title ?? (rootPath ? `Rescan ${rootPath} in the background` : undefined),
    onClick: () => onRescan(catalogId),
  };
}

export function entryMenu(ctx: EntryMenuContext, { ref, fullPath, isDirectory, onViewInTree }: MenuEntry): MenuItem[] {
  const items: MenuItem[] = [
    { label: "Open", onClick: () => ctx.runShell(ref, "open") },
    { label: "Explore", onClick: () => ctx.runShell(ref, "explore") },
    { label: "Properties", onClick: () => ctx.runShell(ref, "properties") },
  ];
  for (const c of ctx.commands) {
    items.push({ label: c.label, onClick: () => ctx.runShell(ref, "custom", c.id) });
  }
  items.push({ label: "", onClick: () => {}, separator: true });
  if (isDirectory) items.push({ label: "Show TreeMap", onClick: () => ctx.showTreemap(ref) });
  items.push({ label: "Copy Full Path", onClick: () => ctx.copyPath(fullPath) });
  if (onViewInTree) items.push({ label: "View in Tree", onClick: onViewInTree });
  if (isDirectory) items.push(ctx.rescanFor(ref.catalogId));
  return items;
}

export function treeNodeMenu(ctx: EntryMenuContext, ref: EntryRef, fullPath: string, onCollapseAll: () => void): MenuItem[] {
  return [...entryMenu(ctx, { ref, fullPath, isDirectory: true }), collapseAllItem(onCollapseAll)];
}

export function listMenu(ctx: EntryMenuContext, node: DirectoryNode): MenuItem[] {
  return entryMenu(ctx, { ref: node.ref, fullPath: node.fullPath, isDirectory: node.isDirectory });
}

export function treeBackgroundMenu(
  selected: EntryRef | null,
  showTreemap: (ref: EntryRef) => void,
  onCollapseAll: () => void,
): MenuItem[] {
  return [
    { label: "Show TreeMap", disabled: !selected, onClick: () => selected && showTreemap(selected) },
    collapseAllItem(onCollapseAll),
  ];
}
