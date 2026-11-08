import type { ViewKind } from "../viewState";

interface Props {
  active: ViewKind;
  /** The TreeMap tab exists only once a folder has been mapped. */
  hasTreemap: boolean;
  activeJobCount: number;
  onSelect: (kind: ViewKind) => void;
  /** Right-click on the Directory tab. */
  onDirectoryMenu: (x: number, y: number) => void;
  /** Why loading catalogs is unavailable right now (disables Reload), or null. */
  loadBlockedReason: string | null;
  onReload: () => void;
}

const TABS: readonly { kind: ViewKind; label: string }[] = [
  { kind: "directory", label: "Directory" },
  { kind: "results", label: "Search Results" },
  { kind: "catalogs", label: "Catalogs" },
  { kind: "diff", label: "Compare" },
  { kind: "jobs", label: "Jobs" },
  { kind: "colours", label: "Customize" },
  { kind: "treemap", label: "TreeMap" },
];

export function TabBar({ active, hasTreemap, activeJobCount, onSelect, onDirectoryMenu, loadBlockedReason, onReload }: Props) {
  return (
    <div className="tabs">
      {TABS.filter((t) => t.kind !== "treemap" || hasTreemap).map(({ kind, label }) => (
        <button
          key={kind}
          className={active === kind ? "active" : ""}
          onClick={() => onSelect(kind)}
          onContextMenu={
            kind === "directory"
              ? (e) => {
                  e.preventDefault();
                  onDirectoryMenu(e.clientX, e.clientY);
                }
              : undefined
          }
        >
          {kind === "jobs" && activeJobCount > 0 ? `${label} (${activeJobCount})` : label}
        </button>
      ))}
      <button
        className="reload"
        disabled={!!loadBlockedReason}
        title={loadBlockedReason ?? undefined}
        onClick={onReload}
      >
        Reload
      </button>
    </div>
  );
}
