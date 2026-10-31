import { CdeApiClient } from "../api/client";

export interface UiState {
  searchHistory?: string[];
  includePath?: boolean;
  includeFiles?: boolean;
  includeFolders?: boolean;
  regexMode?: boolean;
  everythingMode?: boolean;
  limitResultCount?: number;
  lastView?: string;
  catalogDir?: string;
  /** Catalog folders where "Not now" was chosen for migrating legacy catalogs: the dialog no longer opens by itself. */
  legacyMigrateDismissed?: string[];
  /** The last root folder picked for File > New Catalog… (the picker starts there). */
  lastNewCatalogRoot?: string;
  treePaneWidth?: number;
  filtersExpanded?: boolean;
  /** Search-results column ids in display order. */
  searchResultColumnOrder?: string[];
  /** Directory-listing column widths in px, keyed by column id; absent = default. */
  directoryColumnWidths?: Record<string, number>;
  /** TreeMap levels shown below the mapped folder (1–5). */
  treemapDepth?: number;
  [key: string]: unknown;
}

/** Number of previous search patterns kept for the pattern drop-down (matches cdeWin's PatternHistoryMaximum). */
export const SEARCH_HISTORY_MAX = 50;

/**
 * Loads UI state from the sidecar on startup and persists changes via PUT — debounced, with a flush
 * on close. Window geometry is NOT here (that is the Tauri window-state plugin's job, D14).
 */
export class UiStateManager {
  private state: UiState = {};
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly client: CdeApiClient, private readonly debounceMs = 800) {}

  async load(): Promise<UiState> {
    this.state = (await this.client.getUiState()) as UiState;
    // Hand-edited or older state may hold a longer history; trim to the cap on load.
    if ((this.state.searchHistory?.length ?? 0) > SEARCH_HISTORY_MAX) {
      this.state.searchHistory = this.state.searchHistory!.slice(0, SEARCH_HISTORY_MAX);
    }
    return this.state;
  }

  get(): UiState {
    return this.state;
  }

  update(patch: Partial<UiState>): void {
    this.state = { ...this.state, ...patch };
    this.schedule();
  }

  /** Append a search to history (most-recent first, de-duplicated, capped). */
  pushSearchHistory(pattern: string, cap = SEARCH_HISTORY_MAX): void {
    if (!pattern) return;
    const history = (this.state.searchHistory ?? []).filter((h) => h !== pattern);
    history.unshift(pattern);
    this.update({ searchHistory: history.slice(0, cap) });
  }

  private schedule(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.flush(), this.debounceMs);
  }

  async flush(): Promise<void> {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    await this.client.putUiState(this.state as Record<string, unknown>);
  }
}
