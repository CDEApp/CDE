import { CdeApiClient } from "../api/client";

export interface UiState {
  searchHistory?: string[];
  includePath?: boolean;
  includeFiles?: boolean;
  includeFolders?: boolean;
  regexMode?: boolean;
  limitResultCount?: number;
  lastView?: string;
  catalogDir?: string;
  [key: string]: unknown;
}

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
  pushSearchHistory(pattern: string, cap = 50): void {
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
