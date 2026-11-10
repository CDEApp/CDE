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
  /** Everything-style query (space=AND, |=OR, !=NOT, <>=group, "phrase"); overrides regexMode. */
  everythingMode?: boolean;
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
  /** UTC ticks of the catalog file's last write; 0 when unknown. */
  fileModifiedUtcTicks: number;
}

export interface CatalogsResponse {
  catalogs: CatalogInfo[];
  /** The working folder: where catalogs were last loaded from and new ones are written; null before any load. */
  catalogDir: string | null;
  catalogsLoaded: number;
  totalEntries: number;
  memoryBytes: number;
  /** Legacy .cde catalogs the last load found and did not load (no .cdex beside them); offered for migration. */
  legacyCatalogs: LegacyCatalog[];
  /** AppConfig.Scan.StaleAfterDays: a catalog scanned longer ago than this many days is stale; 0 = no marker. */
  staleAfterDays: number;
  /** AppConfig.Scan.RescanStaleOnStartup: rescan the stale catalogs once after the first load. */
  rescanStaleOnStartup: boolean;
}

export interface LegacyCatalog {
  path: string;
  sizeBytes: number;
  /** Its folder can be written to, so it can be migrated there. */
  writable: boolean;
}

/** Migration progress: starting catalog `current` of `total`. */
export interface MigrateProgress {
  current: number;
  total: number;
  path: string;
}

export interface MigrateIssue {
  path: string;
  message: string;
}

/** The migrate `done` payload. */
export interface MigrateResultBody {
  migrated: number;
  total: number;
  /** Not converted; still legacy catalogs. */
  failures: MigrateIssue[];
  /** Converted, but the source could not be renamed (or a .cdex appeared meanwhile). */
  warnings: MigrateIssue[];
}

/** A migrate refusal (409 busy / 422 nothing-to-migrate) or in-stream `error`. */
export interface MigrateErrorBody {
  code: string;
  message: string;
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

export type JobKind = "rescan" | "create";
export type JobState = "queued" | "running" | "succeeded" | "failed" | "cancelled";
export type JobPhase = "queued" | "scanning" | "writing" | "swapping" | "saving";

export interface JobResult {
  catalogId: number;
  rootPath: string;
  /** Files + directories in the new catalog (root excluded, as in the status bar total). */
  entries: number;
  savedPath: string;
  excludedFolderCount?: number;
  accessDeniedFolderCount?: number;
  unreadableFolderCount?: number;
}

export interface JobError {
  code:
    | "unknown-catalog"
    | "already-running"
    | "busy"
    | "catalog-unavailable"
    | "file-exists"
    | "root-not-accessible"
    | "no-source-file"
    | "invalid-file-name"
    | "rescan-failed"
    | "create-failed"
    | string;
  message: string;
  rootPath?: string | null;
}

export interface JobDto {
  id: number;
  kind: JobKind;
  title: string;
  /** Rescan: the catalog. Create: null until it succeeds, then the new id. */
  catalogId: number | null;
  /** Catalog file name without extension. */
  catalogName: string;
  state: JobState;
  queuedAtUtc: string;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
  phase: JobPhase;
  entriesScanned: number;
  note: string;
  result: JobResult | null;
  error: JobError | null;
}

export interface JobsSnapshot {
  jobs: JobDto[];
  maxConcurrent: number;
}

export type SubmitJobRequest =
  | { kind: "rescan"; catalogId: number }
  | { kind: "create"; rootPath: string; fileName?: string; description?: string; followJunctions?: boolean };

export type ShellAction ="open" | "explore" | "properties" | "custom";

export interface ShellRequest {
  ref: string;
  action: ShellAction;
  commandId?: number;
}

/**
 * Exact tally of everything below one both-sides folder pair (`POST /compare/summaries`). The file
 * counts cover files and mismatched-kind pairs; a one-sided entry counts once in `leftOnly` or
 * `rightOnly` and is not descended. The folder counts cover both-sides folder pairs nested at any
 * depth below the pair (the pair itself is never counted): `sameFolders` have an identical subtree,
 * `differentFolders` do not. `leftNewerFiles` and `rightNewerFiles` are subsets of `differentFiles`;
 * `leftNewerFolders` and `rightNewerFolders` are subsets of `differentFolders` whose own two modified
 * times are valid and differ by more than the tolerance.
 */
export interface SubtreeSummary {
  sameFiles: number;
  differentFiles: number;
  leftNewerFiles: number;
  rightNewerFiles: number;
  leftOnly: number;
  rightOnly: number;
  sameFolders: number;
  differentFolders: number;
  leftNewerFolders: number;
  rightNewerFolders: number;
}
