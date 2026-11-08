import type { MenuItem } from "./components/ContextMenu";
import type { DirectoryNode, EntryRef, ShellAction } from "./api/types";
import { rescanMenuItem, type JobsModel } from "./jobsModel";

export interface ShellCommand {
  id: number;
  label: string;
}

export interface EntryMenuContext {
  commands: readonly ShellCommand[];
  runShell: (ref: EntryRef, action: ShellAction, commandId?: number) => void;
  showTreemap: (ref: EntryRef) => void;
  /** Fill the search box with an Everything query scoped to this folder and its catalog. */
  searchFromHere: (ref: EntryRef, fullPath: string) => void;
  copyPath: (fullPath: string) => void;
  rescanFor: (catalogId: number) => MenuItem;
  /** Open the description editor for this catalog. */
  editDescription: (catalogId: number) => void;
}

export interface MenuEntry {
  ref: EntryRef;
  fullPath: string;
  isDirectory: boolean;
  onViewInTree?: () => void;
  /** False when the entry may belong to a catalog that was since reloaded away (stale search results). */
  canEditDescription?: boolean;
}

export function editDescriptionItem(onEdit: () => void): MenuItem {
  return { label: "Edit Description…", onClick: onEdit };
}

export function collapseAllItem(onCollapseAll: () => void): MenuItem {
  return { label: "Collapse all", onClick: onCollapseAll };
}

export function rescanItem(
  jobs: JobsModel,
  catalogId: number,
  rootPath: string | undefined,
  onRescan: (catalogId: number) => void,
): MenuItem {
  const m = rescanMenuItem(jobs, catalogId);
  return {
    label: m.label,
    disabled: m.disabled,
    title: m.title ?? (rootPath ? `Rescan ${rootPath} in the background` : undefined),
    onClick: () => onRescan(catalogId),
  };
}

export function entryMenu(
  ctx: EntryMenuContext,
  { ref, fullPath, isDirectory, onViewInTree, canEditDescription = true }: MenuEntry,
): MenuItem[] {
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
  if (isDirectory) items.push({ label: "Search from here", onClick: () => ctx.searchFromHere(ref, fullPath) });
  items.push({ label: "Copy Full Path", onClick: () => ctx.copyPath(fullPath) });
  if (onViewInTree) items.push({ label: "View in Tree", onClick: onViewInTree });
  if (isDirectory) items.push(ctx.rescanFor(ref.catalogId));
  const edit = editDescriptionItem(() => ctx.editDescription(ref.catalogId));
  items.push(
    canEditDescription ? edit : { ...edit, disabled: true, title: "This result is from a catalog set that was reloaded. Search again." },
  );
  return items;
}

export function treeNodeMenu(ctx: EntryMenuContext, ref: EntryRef, fullPath: string, onCollapseAll: () => void): MenuItem[] {
  return [...entryMenu(ctx, { ref, fullPath, isDirectory: true }), collapseAllItem(onCollapseAll)];
}

export function listMenu(ctx: EntryMenuContext, node: DirectoryNode): MenuItem[] {
  return entryMenu(ctx, { ref: node.ref, fullPath: node.fullPath, isDirectory: node.isDirectory });
}

export function catalogMenu(
  catalogId: number,
  showTreemap: (ref: EntryRef) => void,
  onEditDescription: () => void,
  rescan: MenuItem,
): MenuItem[] {
  return [
    { label: "Show TreeMap", onClick: () => showTreemap({ catalogId, entryIndex: 0 }) },
    editDescriptionItem(onEditDescription),
    rescan,
  ];
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
