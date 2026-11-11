import type { MenuItem } from "./components/ContextMenu";
import type { DirectoryNode, EntryRef, SearchResultRow, ShellAction } from "./api/types";
import { rowsToClipboardText } from "./copyText";
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
  copyText: (text: string) => void;
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
  /** The selected search results this menu acts on. Adds the copy variants, and with several rows disables the single-entry items. */
  selection?: readonly SearchResultRow[];
}

const ONE_ITEM_ONLY = "(one item only)";

function oneItemOnly(item: MenuItem): MenuItem {
  return { ...item, label: `${item.label} ${ONE_ITEM_ONLY}`, disabled: true, title: "Select a single result to use this" };
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
  { ref, fullPath, isDirectory, onViewInTree, canEditDescription = true, selection }: MenuEntry,
): MenuItem[] {
  const multi = !!selection && selection.length > 1;
  const single = (item: MenuItem) => (multi ? oneItemOnly(item) : item);
  const items: MenuItem[] = [
    single({ label: "Open", onClick: () => ctx.runShell(ref, "open") }),
    single({ label: "Explore", onClick: () => ctx.runShell(ref, "explore") }),
    single({ label: "Properties", onClick: () => ctx.runShell(ref, "properties") }),
  ];
  for (const c of ctx.commands) {
    items.push(single({ label: c.label, onClick: () => ctx.runShell(ref, "custom", c.id) }));
  }
  items.push({ label: "", onClick: () => {}, separator: true });
  if (isDirectory) items.push(single({ label: "Show TreeMap", onClick: () => ctx.showTreemap(ref) }));
  if (isDirectory) items.push(single({ label: "Search from here", onClick: () => ctx.searchFromHere(ref, fullPath) }));
  if (selection) {
    const copy = (label: string, kind: "path" | "name" | "parent") => ({
      label,
      onClick: () => ctx.copyText(rowsToClipboardText(selection, kind)),
    });
    items.push(copy(multi ? "Copy Full Paths" : "Copy Full Path", "path"));
    items.push(copy(multi ? "Copy Names" : "Copy Name", "name"));
    items.push(copy(multi ? "Copy Parent Folders" : "Copy Parent Folder", "parent"));
  } else {
    items.push({ label: "Copy Full Path", onClick: () => ctx.copyPath(fullPath) });
  }
  if (onViewInTree) items.push(single({ label: "View in Tree", onClick: onViewInTree }));
  if (isDirectory) items.push(single(ctx.rescanFor(ref.catalogId)));
  const edit = editDescriptionItem(() => ctx.editDescription(ref.catalogId));
  items.push(
    canEditDescription
      ? single(edit)
      : { ...edit, disabled: true, title: "This result is from a catalog set that was reloaded. Search again." },
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
