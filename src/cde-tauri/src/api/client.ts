import {
  CatalogsResponse,
  DirectoryNode,
  EntryRef,
  SearchProgress,
  SearchQuery,
  SearchResultRow,
  ShellRequest,
  CatalogLoadProgress,
  RescanErrorBody,
  RescanProgress,
  RescanResult,
  TreemapResponse,
  CreateCatalogRequest,
  CreateErrorBody,
  CreatePreflightRequest,
  CreatePreflightResult,
  CreateProgress,
  CreateResult,
  MigrateErrorBody,
  MigrateProgress,
  MigrateResultBody,
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

export interface RescanHandlers {
  onProgress?: (p: RescanProgress) => void;
  signal?: AbortSignal;
}

export interface CreateHandlers {
  onProgress?: (p: CreateProgress) => void;
  signal?: AbortSignal;
}

export interface MigrateHandlers {
  onProgress?: (p: MigrateProgress) => void;
  signal?: AbortSignal;
}

/** A non-2xx response from the sidecar; `body` is the raw response text. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly body: string,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/** A shell action the sidecar refused (501, unsupported here) or could not carry out (422); `message` is the reason. */
export class ShellError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ShellError";
  }
}

/** The reason in a failed /shell response: a JSON string, or the raw text when it is not JSON. */
async function shellReason(r: Response): Promise<string> {
  const text = await r.text();
  try {
    const parsed: unknown = JSON.parse(text);
    return typeof parsed === "string" ? parsed : text;
  } catch {
    return text;
  }
}

/** A rescan that did not complete: refused up front, or failed mid-way. The catalog is unchanged. */
export class RescanFailure extends Error {
  constructor(readonly error: RescanErrorBody) {
    super(error.message);
    this.name = "RescanFailure";
  }

  /** The root path can't be reached: the UI shows its modal instead of a toast. */
  get rootNotAccessible(): boolean {
    return this.error.code === "root-not-accessible";
  }
}

/** A create that did not complete: refused up front, or failed mid-way. Nothing was written or added. */
export class CreateFailure extends Error {
  constructor(readonly error: CreateErrorBody) {
    super(error.message);
    this.name = "CreateFailure";
  }
}

/** A migration that did not run: refused up front (busy, nothing to migrate), or failed mid-way. */
export class MigrateFailure extends Error {
  constructor(readonly error: MigrateErrorBody) {
    super(error.message);
    this.name = "MigrateFailure";
  }
}

function parseMigrateRefusal(e: ApiError): MigrateErrorBody {
  try {
    const parsed = JSON.parse(e.body);
    if (parsed && typeof parsed.code === "string") return parsed as MigrateErrorBody;
  } catch {
    /* not JSON: fall through */
  }
  return { code: "migrate-failed", message: e.body || `migrate: ${e.status}` };
}

function parseCreateRefusal(e: ApiError): CreateErrorBody {
  try {
    const parsed = JSON.parse(e.body);
    if (parsed && typeof parsed.code === "string") return parsed as CreateErrorBody;
  } catch {
    /* not JSON: fall through */
  }
  return { code: "create-failed", message: e.body || `create: ${e.status}` };
}

function parseRescanRefusal(e: ApiError): RescanErrorBody {
  try {
    const parsed = JSON.parse(e.body);
    if (parsed && typeof parsed.code === "string") return parsed as RescanErrorBody;
  } catch {
    /* not JSON: fall through */
  }
  return { code: "rescan-failed", message: e.body || `rescan: ${e.status}` };
}

/** Sort keys `GET /entries/{ref}/children` accepts; a leading `-` means descending. */
export type ChildrenSort = "name" | "size" | "date" | "-name" | "-size" | "-date";
const CHILDREN_SORTS: ReadonlySet<string> = new Set<ChildrenSort>(["name", "size", "date", "-name", "-size", "-date"]);

const MAX_INT32 = 2 ** 31 - 1;

/**
 * A catalog id or entry index as a URL path segment. Refs come back from the sidecar as JSON, so their
 * fields are only typed as numbers; anything but a non-negative int32 (what the server route accepts)
 * throws instead of reaching a request URL.
 */
export function idSegment(value: number): string {
  const n = Number(value);
  if (typeof value !== "number" || !Number.isInteger(n) || n < 0 || n > MAX_INT32) {
    throw new RangeError(`not a valid id: ${String(value)}`);
  }
  return String(n);
}

/** An {@link EntryRef} as the `{catalog}-{index}` path segment the server route parses. */
export function refSegment(ref: EntryRef): string {
  return `${idSegment(ref.catalogId)}-${idSegment(ref.entryIndex)}`;
}

/**
 * Join `baseUrl` with `path` segments, percent-encoding each segment (so a segment can never add a `/`,
 * `?` or `#`), then append `query`. ASP.NET decodes route values, so `refSegment` output (`{catalog}-{index}`)
 * round-trips unchanged.
 */
export function buildApiUrl(
  baseUrl: string,
  path: readonly (string | number)[],
  query?: URLSearchParams | Record<string, string>,
): string {
  const base = baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
  const url = `${base}/${path.map((seg) => encodeURIComponent(String(seg))).join("/")}`;
  const qs = (query instanceof URLSearchParams ? query : new URLSearchParams(query)).toString();
  return qs ? `${url}?${qs}` : url;
}

/**
 * HTTP + SSE client for the cdeApi sidecar. Every request carries the handshake token. SSE is read
 * over fetch's streaming body (EventSource can't set the auth header) and parsed event-by-event.
 */
export class CdeApiClient {
  constructor(private readonly baseUrl: string, private readonly token: string) {}

  /** Build an endpoint URL; every path segment and query value is percent-encoded here, never by callers. */
  private url(path: readonly (string | number)[], query?: URLSearchParams | Record<string, string>): string {
    return buildApiUrl(this.baseUrl, path, query);
  }

  private headers(extra?: Record<string, string>): Record<string, string> {
    return { "X-CDE-Token": this.token, ...extra };
  }

  async health(): Promise<boolean> {
    const r = await fetch(this.url(["health"]), { headers: this.headers() });
    return r.ok;
  }

  async catalogs(): Promise<CatalogsResponse> {
    const r = await fetch(this.url(["catalogs"]), { headers: this.headers() });
    if (!r.ok) throw new Error(`catalogs: ${r.status}`);
    return r.json();
  }

  async children(
    ref: EntryRef,
    /** `hasChildren: false` leaves every `hasChildren` false (skips the server's sub-folder walk). */
    opts?: { foldersOnly?: boolean; skip?: number; take?: number; sort?: ChildrenSort; hasChildren?: boolean },
  ): Promise<DirectoryNode[]> {
    const q = new URLSearchParams();
    if (opts?.foldersOnly) q.set("foldersOnly", "true");
    if (opts?.hasChildren === false) q.set("hasChildren", "false");
    if (opts?.skip != null) q.set("skip", String(opts.skip));
    if (opts?.take != null) q.set("take", String(opts.take));
    if (opts?.sort && CHILDREN_SORTS.has(opts.sort)) q.set("sort", opts.sort);
    const r = await fetch(
      this.url(["entries", refSegment(ref), "children"], q),
      { headers: this.headers() },
    );
    if (!r.ok) throw new Error(`children: ${r.status}`);
    return r.json();
  }

  async path(ref: EntryRef): Promise<DirectoryNode[]> {
    const r = await fetch(this.url(["entries", refSegment(ref), "path"]), {
      headers: this.headers(),
    });
    if (!r.ok) throw new Error(`path: ${r.status}`);
    return r.json();
  }

  /** Depth-limited treemap rows for a directory (small entries merged server-side). */
  async treemap(
    ref: EntryRef,
    opts: { depth: number; minFraction: number; signal?: AbortSignal },
  ): Promise<TreemapResponse> {
    const q = new URLSearchParams({ depth: String(opts.depth), minFraction: String(opts.minFraction) });
    const r = await fetch(this.url(["entries", refSegment(ref), "treemap"], q), {
      headers: this.headers(),
      signal: opts.signal,
    });
    if (!r.ok) throw new Error(`treemap: ${r.status} ${await r.text()}`);
    return r.json();
  }

  async shellCommands(): Promise<{ id: number; label: string }[]> {
    const r = await fetch(this.url(["shell", "commands"]), { headers: this.headers() });
    if (!r.ok) throw new Error(`shell/commands: ${r.status}`);
    return r.json();
  }

  async shell(req: ShellRequest): Promise<void> {
    const r = await fetch(this.url(["shell"]), {
      method: "POST",
      headers: this.headers({ "Content-Type": "application/json" }),
      body: JSON.stringify(req),
    });
    if (!r.ok) throw new ShellError(r.status, await shellReason(r));
  }

  async getUiState(): Promise<Record<string, unknown>> {
    const r = await fetch(this.url(["ui-state"]), { headers: this.headers() });
    if (!r.ok) throw new Error(`ui-state: ${r.status}`);
    return r.json();
  }

  async putUiState(state: Record<string, unknown>): Promise<void> {
    await fetch(this.url(["ui-state"]), {
      method: "PUT",
      headers: this.headers({ "Content-Type": "application/json" }),
      body: JSON.stringify(state),
    });
  }

  /** The colour-rules text; the sidecar creates the file from its defaults when missing. */
  async getColourRules(): Promise<string> {
    const r = await fetch(this.url(["colour-rules"]), { headers: this.headers() });
    if (!r.ok) throw new Error(`colour-rules: ${r.status}`);
    return r.text();
  }

  /** The sidecar's built-in default rules text (single source for "Reset to defaults"). */
  async getColourRuleDefaults(): Promise<string> {
    const r = await fetch(this.url(["colour-rules", "defaults"]), { headers: this.headers() });
    if (!r.ok) throw new Error(`colour-rules/defaults: ${r.status}`);
    return r.text();
  }

  async putColourRules(text: string): Promise<void> {
    const r = await fetch(this.url(["colour-rules"]), {
      method: "PUT",
      headers: this.headers({ "Content-Type": "text/plain; charset=utf-8" }),
      body: text,
    });
    if (!r.ok) throw new Error(`colour-rules: ${r.status}`);
  }

  /**
   * Reload catalogs, streaming load progress then a final done event. When `path` is given, the
   * sidecar loads .cdex catalogs from that folder (and one level down) instead of its default.
   */
  async reload(handlers: ReloadHandlers, path?: string): Promise<void> {
    const url = this.url(["session", "reload"], path ? { path } : undefined);
    await this.streamSse(
      url,
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
   * Rescan one catalog in the background. Resolves with the result on `done`, or null when aborted
   * (the sidecar cancels the scan and leaves the catalog untouched). Throws {@link RescanFailure}
   * for a refusal (unknown / already running / root unreachable) or an in-stream `error` event.
   */
  async rescan(catalogId: number, handlers: RescanHandlers): Promise<RescanResult | null> {
    // Assigned inside the SSE callback; the casts stop TS narrowing them to `null` afterwards.
    let result = null as RescanResult | null;
    let failure = null as RescanErrorBody | null;
    try {
      await this.streamSse(
        this.url(["catalogs", idSegment(catalogId), "rescan"]),
        { method: "POST", headers: this.headers(), signal: handlers.signal },
        (event, data) => {
          if (event === "progress") handlers.onProgress?.(JSON.parse(data));
          else if (event === "done") result = JSON.parse(data);
          else if (event === "error") failure = JSON.parse(data);
        },
      );
    } catch (e) {
      if (e instanceof ApiError) throw new RescanFailure(parseRescanRefusal(e));
      throw e;
    }
    if (failure) throw new RescanFailure(failure);
    return result;
  }

  /**
   * Everything the New Catalog dialog needs before Create: cde's default file name for the root, the working
   * folder, whether the target exists / is a loaded catalog, writability, same-root catalogs.
   */
  async preflightCreate(req: CreatePreflightRequest, signal?: AbortSignal): Promise<CreatePreflightResult> {
    const r = await fetch(this.url(["catalogs", "preflight"]), {
      method: "POST",
      headers: this.headers({ "Content-Type": "application/json" }),
      body: JSON.stringify(req),
      signal,
    });
    if (!r.ok) throw new Error(`catalogs/preflight: ${r.status} ${await r.text()}`);
    return r.json();
  }

  /**
   * Scan a new root into a new catalog in the working folder. Resolves with the result on `done`, or null when
   * aborted (the sidecar cancels and leaves no file and no session change, unless the cancel lost the race with the
   * final rename). Throws {@link CreateFailure} for a refusal or an in-stream `error` event.
   */
  async createCatalog(req: CreateCatalogRequest, handlers: CreateHandlers): Promise<CreateResult | null> {
    let result = null as CreateResult | null;
    let failure = null as CreateErrorBody | null;
    try {
      await this.streamSse(
        this.url(["catalogs"]),
        {
          method: "POST",
          headers: this.headers({ "Content-Type": "application/json" }),
          body: JSON.stringify(req),
          signal: handlers.signal,
        },
        (event, data) => {
          if (event === "progress") handlers.onProgress?.(JSON.parse(data));
          else if (event === "done") result = JSON.parse(data);
          else if (event === "error") failure = JSON.parse(data);
        },
      );
    } catch (e) {
      if (e instanceof ApiError) throw new CreateFailure(parseCreateRefusal(e));
      throw e;
    }
    if (failure) throw new CreateFailure(failure);
    return result;
  }

  /**
   * Run a search. Returns when the stream completes (done) or is aborted. Throws with the server's
   * 400 validation message when the query is invalid.
   */
  async search(query: SearchQuery, handlers: SearchHandlers): Promise<void> {
    await this.streamSse(
      this.url(["search"]),
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
  /**
   * Convert the legacy catalogs the last load listed to .cdex, one at a time. Resolves with the report on `done`,
   * or null when aborted (the catalogs already converted stay converted). Throws {@link MigrateFailure} for a
   * refusal or an in-stream `error`. Loads nothing: reload afterwards to see the new catalogs.
   */
  async migrateLegacy(handlers: MigrateHandlers): Promise<MigrateResultBody | null> {
    let result = null as MigrateResultBody | null;
    let failure = null as MigrateErrorBody | null;
    try {
      await this.streamSse(
        this.url(["catalogs", "migrate"]),
        { method: "POST", headers: this.headers(), signal: handlers.signal },
        (event, data) => {
          if (event === "progress") handlers.onProgress?.(JSON.parse(data));
          else if (event === "done") result = JSON.parse(data);
          else if (event === "error") failure = JSON.parse(data);
        },
      );
    } catch (e) {
      if (e instanceof ApiError) throw new MigrateFailure(parseMigrateRefusal(e));
      throw e;
    }
    if (failure) throw new MigrateFailure(failure);
    return result;
  }

  private async streamSse(
    url: string,
    init: RequestInit,
    onEvent: (event: string, data: string) => void,
  ): Promise<void> {
    const resp = await fetch(url, init);
    if (!resp.ok) {
      const msg = await resp.text();
      throw new ApiError(resp.status, msg, msg || `request failed: ${resp.status}`);
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
