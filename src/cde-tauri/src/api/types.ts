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
  /** Fill `duplicateCount` on each row. Costs one pass over every loaded catalog's hashes, so set it only when shown. */
  includeDuplicateCounts?: boolean;
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
  /** Full hash as 32 lowercase hex digits (`hash:` takes it); absent for a folder or a file with no usable full hash. */
  contentHash?: string;
  /** Other files in the loaded catalogs with the same `contentHash` (0 = unique); absent without a hash or `includeDuplicateCounts`. */
  duplicateCount?: number;
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
  /** On the final report only: the result limit stopped the search and more matches exist. */
  limitReached?: boolean;
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

/** Which files a hash run reads. It belongs to one run and is never saved. */
export interface HashOptions {
  /** An Everything-style query a file must match (the search bar's syntax); blank or absent selects every file. */
  filter?: string | null;
  /** Also hash files whose size no other file in the run's catalogs has; each selected file then gets a full hash. */
  includeUniqueSizes?: boolean;
}

/** Body of POST /catalogs/hash/preflight: the same catalog set and options as a "hash" job. */
export interface HashPreflightRequest {
  catalogIds: number[];
  hash?: HashOptions | null;
}

/** What a hash job with the same catalogs and options would read (POST /catalogs/hash/preflight). */
export interface HashPreflightResult {
  /** Selected files the run reads in its first pass. */
  files: number;
  /** Their total size. The first pass reads only the start of each; possible duplicates are then read whole. */
  bytes: number;
  /** Selected files whose hash the run keeps as it is. */
  alreadyHashed: number;
}

/**
 * The current pass of a running hash job. The counts start again at 0 when the phase goes from "hashing" to
 * "hashing-full". `bytesDone` grows as each file ends, not while it is read.
 */
export interface HashJobProgress {
  filesDone: number;
  filesTotal: number;
  bytesDone: number;
  bytesTotal: number;
}

export interface HashResult {
  /** Files given a new or upgraded hash. */
  filesHashed: number;
  /** Files that could not be read; they stay unhashed. */
  filesFailed: number;
}

export type JobKind = "rescan" | "create" | "hash";
export type JobState = "queued" | "running" | "succeeded" | "failed" | "cancelled";
export type JobPhase = "queued" | "scanning" | "writing" | "swapping" | "saving" | "hashing" | "hashing-full";

/** A hash job fills only `hash`: its `catalogId` and `rootPath` are null and its counts 0. */
export interface JobResult {
  catalogId: number | null;
  rootPath: string | null;
  /** Files + directories in the new catalog (root excluded, as in the status bar total). */
  entries: number;
  savedPath: string;
  excludedFolderCount?: number;
  accessDeniedFolderCount?: number;
  unreadableFolderCount?: number;
  hash?: HashResult | null;
}

/** The result of a rescan or a create, which always names its catalog. */
export type ScanJobResult = JobResult & { catalogId: number; rootPath: string };

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
    | "invalid-filter"
    | "legacy-catalog"
    | "hash-failed"
    | string;
  message: string;
  rootPath?: string | null;
}

export interface JobDto {
  id: number;
  kind: JobKind;
  title: string;
  /** Rescan: the catalog. Create: null until it succeeds, then the new id. Hash: null. */
  catalogId: number | null;
  /** Hash: the catalogs the run hashes together, ascending. Null for the other kinds. */
  catalogIds?: number[] | null;
  /** Catalog file name without extension. Hash: the names of its catalogs, joined with ", ". */
  catalogName: string;
  state: JobState;
  queuedAtUtc: string;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
  phase: JobPhase;
  entriesScanned: number;
  /** Hash: the counts of the current pass, from the first report on. Null for the other kinds. */
  hash?: HashJobProgress | null;
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
  | { kind: "create"; rootPath: string; fileName?: string; description?: string; followJunctions?: boolean; hash?: HashOptions }
  | { kind: "hash"; catalogIds: number[]; hash?: HashOptions | null };

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
