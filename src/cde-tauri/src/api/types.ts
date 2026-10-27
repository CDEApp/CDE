// TypeScript mirrors of the cdeAppCore DTOs that cross the cdeApi boundary.
// JSON is camelCase (configured by AppCoreJsonContext).

export interface EntryRef {
  catalogId: number;
  entryIndex: number;
}

export function refToString(r: EntryRef): string {
  return `${r.catalogId}-${r.entryIndex}`;
}

export interface SearchQuery {
  pattern: string;
  regexMode?: boolean;
  includePath?: boolean;
  includeFiles?: boolean;
  includeFolders?: boolean;
  limitResultCount?: number;
  fromSizeEnable?: boolean;
  fromSize?: number;
  toSizeEnable?: boolean;
  toSize?: number;
  fromDateEnable?: boolean;
  fromDate?: string; // ISO date
  toDateEnable?: boolean;
  toDate?: string;
  fromHourEnable?: boolean;
  fromHour?: string; // "HH:mm:ss"
  toHourEnable?: boolean;
  toHour?: string;
  notOlderThanEnable?: boolean;
  notOlderThan?: string;
}

export interface SearchResultRow {
  ref: EntryRef;
  name: string;
  size: number;
  modified: string;
  isModifiedBad: boolean;
  isDirectory: boolean;
  isReparsePoint: boolean;
  fullPath: string;
  parentPath: string;
  catalogName: string;
}

export interface DirectoryNode {
  ref: EntryRef;
  name: string;
  fullPath: string;
  isDirectory: boolean;
  hasChildren: boolean;
  size: number;
  modified: string;
  isModifiedBad: boolean;
  isReparsePoint: boolean;
}

/** One flat treemap row, linked to its parent by `parentId` (null only for the mapped root). */
export interface TreemapRow {
  nodeId: string;
  parentId: string | null;
  name: string;
  /** Real size in bytes (a directory's aggregated subtree size). */
  size: number;
  /** Tile area contribution: 0 for an expanded directory (its children carry the area). */
  value: number;
  isDirectory: boolean;
  /** Entries folded into a "(N smaller …)" row, else 0. */
  mergedCount: number;
  /** Null for a merged row. */
  ref: EntryRef | null;
}

export interface TreemapResponse {
  root: EntryRef;
  rootFullPath: string;
  rootSize: number;
  depth: number;
  truncated: boolean;
  rows: TreemapRow[];
}

export interface CatalogInfo {
  catalogId: number;
  rootPath: string;
  volumeName: string;
  dirEntryCount: number;
  fileEntryCount: number;
  driveLetterHint: string;
  rootSize: number;
  availSpace: number;
  totalSpace: number;
  scanStartUtcTicks: number;
  scanEndUtcTicks: number;
  actualFileName: string;
  defaultFileName: string;
  description: string;
  /** The exact file this catalog was loaded from (a rescan saves over it). */
  sourcePath: string | null;
  /** Catalog was scanned following junctions (#66); false for older catalogs. */
  followJunctions: boolean;
}

export interface CatalogsResponse {
  catalogs: CatalogInfo[];
  /** The working folder: where catalogs were last loaded from and new ones are written; null before any load. */
  catalogDir: string | null;
  catalogsLoaded: number;
  totalEntries: number;
  memoryBytes: number;
}

export interface SearchProgress {
  count: number;
  total: number;
  elapsed: string;
}

export interface CatalogLoadProgress {
  current: number;
  total: number;
  message: string;
}

export interface RescanProgress {
  catalogId: number;
  /** "scanning" | "writing" | "swapping" */
  phase: string;
  entriesScanned: number;
  currentPath: string | null;
}

export interface RescanResult {
  catalogId: number;
  rootPath: string;
  /** Files + directories in the new catalog (root excluded, as in the status bar total). */
  entries: number;
  savedPath: string;
}

/** Wire shape of a refusal (JSON, status 404/409/422) or an in-stream `error` event. */
export interface RescanErrorBody {
  code: "unknown-catalog" | "already-running" | "root-not-accessible" | "rescan-failed" | string;
  message: string;
  rootPath?: string | null;
}

// ---------------------------------------------------------------- new catalog (#82)

/** Body of POST /catalogs/preflight. */
export interface CreatePreflightRequest {
  rootPath: string;
  /** The name being typed (no extension); omit to check the default name. */
  fileName?: string | null;
}

/** What the New Catalog dialog needs before Create (POST /catalogs/preflight). */
export interface CreatePreflightResult {
  /** cde's default name for the root, without extension. */
  defaultFileName: string | null;
  /** The name that was checked (requested, else default), without extension. */
  fileName: string;
  fileNameError: string | null;
  /** The working folder new catalogs are written to; null before any load. */
  catalogDir: string | null;
  /** Full path the catalog would be written to; null if not computable. */
  targetPath: string | null;
  exists: boolean;
  /** When `exists`: the loaded catalog backed by that file (Rescan existing), else null. */
  existingCatalogId: number | null;
  writable: boolean;
  sameRootCatalogIds: number[];
  rootAccessible: boolean;
  /** A catalog load is running. */
  busy: boolean;
}

/** Body of POST /catalogs. */
export interface CreateCatalogRequest {
  rootPath: string;
  /** Without extension; `.cdex` is appended by the sidecar. */
  fileName: string;
  description: string;
  followJunctions: boolean;
}

export interface CreateProgress {
  /** "scanning" | "writing" | "saving" */
  phase: string;
  entriesScanned: number;
  currentPath: string | null;
}

export interface CreateResult {
  /** The new catalog's id; existing catalogs keep theirs. */
  catalogId: number;
  rootPath: string;
  entries: number;
  savedPath: string;
}

/** Wire shape of a create refusal (JSON, 400/409/422) or an in-stream `error` event. */
export interface CreateErrorBody {
  code:
    | "invalid-file-name"
    | "busy"
    | "file-exists"
    | "no-catalog-dir"
    | "root-not-accessible"
    | "folder-not-writable"
    | "create-failed"
    | string;
  message: string;
  rootPath?: string | null;
  /** For `file-exists`: the loaded catalog backed by that file, if any. */
  existingCatalogId?: number | null;
}

export type ShellAction ="open" | "explore" | "properties" | "custom";

export interface ShellRequest {
  ref: string;
  action: ShellAction;
  commandId?: number;
}
