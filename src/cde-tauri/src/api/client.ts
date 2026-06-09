import {
  CatalogsResponse,
  DirectoryNode,
  EntryRef,
  refToString,
  SearchProgress,
  SearchQuery,
  SearchResultRow,
  ShellRequest,
  CatalogLoadProgress,
} from "./types";

export interface SearchHandlers {
  onResult: (row: SearchResultRow) => void;
  onProgress?: (p: SearchProgress) => void;
  onDone?: (count: number) => void;
  signal?: AbortSignal;
}

export interface ReloadHandlers {
  onProgress?: (p: CatalogLoadProgress) => void;
  onDone?: (catalogs: number, entries: number) => void;
  signal?: AbortSignal;
}

/**
 * HTTP + SSE client for the cdeApi sidecar. Every request carries the handshake token. SSE is read
 * over fetch's streaming body (EventSource can't set the auth header) and parsed event-by-event.
 */
export class CdeApiClient {
  constructor(private readonly baseUrl: string, private readonly token: string) {}

  private headers(extra?: Record<string, string>): Record<string, string> {
    return { "X-CDE-Token": this.token, ...extra };
  }

  async health(): Promise<boolean> {
    const r = await fetch(`${this.baseUrl}/health`, { headers: this.headers() });
    return r.ok;
  }

  async catalogs(): Promise<CatalogsResponse> {
    const r = await fetch(`${this.baseUrl}/catalogs`, { headers: this.headers() });
    if (!r.ok) throw new Error(`catalogs: ${r.status}`);
    return r.json();
  }

  async children(
    ref: EntryRef,
    opts?: { foldersOnly?: boolean; skip?: number; take?: number; sort?: string },
  ): Promise<DirectoryNode[]> {
    const q = new URLSearchParams();
    if (opts?.foldersOnly) q.set("foldersOnly", "true");
    if (opts?.skip != null) q.set("skip", String(opts.skip));
    if (opts?.take != null) q.set("take", String(opts.take));
    if (opts?.sort) q.set("sort", opts.sort);
    const qs = q.toString();
    const r = await fetch(
      `${this.baseUrl}/entries/${refToString(ref)}/children${qs ? `?${qs}` : ""}`,
      { headers: this.headers() },
    );
    if (!r.ok) throw new Error(`children: ${r.status}`);
    return r.json();
  }

  async path(ref: EntryRef): Promise<DirectoryNode[]> {
    const r = await fetch(`${this.baseUrl}/entries/${refToString(ref)}/path`, {
      headers: this.headers(),
    });
    if (!r.ok) throw new Error(`path: ${r.status}`);
    return r.json();
  }

  async shellCommands(): Promise<{ id: number; label: string }[]> {
    const r = await fetch(`${this.baseUrl}/shell/commands`, { headers: this.headers() });
    if (!r.ok) throw new Error(`shell/commands: ${r.status}`);
    return r.json();
  }

  async shell(req: ShellRequest): Promise<void> {
    const r = await fetch(`${this.baseUrl}/shell`, {
      method: "POST",
      headers: this.headers({ "Content-Type": "application/json" }),
      body: JSON.stringify(req),
    });
    if (!r.ok) throw new Error(`shell: ${r.status} ${await r.text()}`);
  }

  async getUiState(): Promise<Record<string, unknown>> {
    const r = await fetch(`${this.baseUrl}/ui-state`, { headers: this.headers() });
    if (!r.ok) throw new Error(`ui-state: ${r.status}`);
    return r.json();
  }

  async putUiState(state: Record<string, unknown>): Promise<void> {
    await fetch(`${this.baseUrl}/ui-state`, {
      method: "PUT",
      headers: this.headers({ "Content-Type": "application/json" }),
      body: JSON.stringify(state),
    });
  }

  /** Reload catalogs, streaming load progress then a final done event. */
  async reload(handlers: ReloadHandlers): Promise<void> {
    await this.streamSse(
      `${this.baseUrl}/session/reload`,
      { method: "POST", headers: this.headers(), signal: handlers.signal },
      (event, data) => {
        if (event === "progress") handlers.onProgress?.(JSON.parse(data));
        else if (event === "done") {
          const d = JSON.parse(data);
          handlers.onDone?.(d.catalogs, d.entries);
        }
      },
    );
  }

  /**
   * Run a search. Returns when the stream completes (done) or is aborted. Throws with the server's
   * 400 validation message when the query is invalid.
   */
  async search(query: SearchQuery, handlers: SearchHandlers): Promise<void> {
    await this.streamSse(
      `${this.baseUrl}/search`,
      {
        method: "POST",
        headers: this.headers({ "Content-Type": "application/json" }),
        body: JSON.stringify(query),
        signal: handlers.signal,
      },
      (event, data) => {
        if (event === "result") handlers.onResult(JSON.parse(data));
        else if (event === "progress") handlers.onProgress?.(JSON.parse(data));
        else if (event === "done") handlers.onDone?.(JSON.parse(data).count);
      },
    );
  }

  // --- minimal SSE-over-fetch parser ---
  private async streamSse(
    url: string,
    init: RequestInit,
    onEvent: (event: string, data: string) => void,
  ): Promise<void> {
    const resp = await fetch(url, init);
    if (!resp.ok) {
      const msg = await resp.text();
      throw new Error(msg || `request failed: ${resp.status}`);
    }
    if (!resp.body) return;

    const reader = resp.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });

        let sep: number;
        // Events are separated by a blank line.
        while ((sep = buffer.indexOf("\n\n")) >= 0) {
          const chunk = buffer.slice(0, sep);
          buffer = buffer.slice(sep + 2);
          this.dispatchSse(chunk, onEvent);
        }
      }
    } catch (e) {
      // An aborted stream surfaces as an AbortError — treat as a clean stop.
      if ((e as Error)?.name !== "AbortError") throw e;
    }
  }

  private dispatchSse(chunk: string, onEvent: (event: string, data: string) => void): void {
    let event = "message";
    let data = "";
    for (const line of chunk.split("\n")) {
      if (line.startsWith("event:")) event = line.slice(6).trim();
      else if (line.startsWith("data:")) data += line.slice(5).trim();
    }
    if (data) onEvent(event, data);
  }
}
