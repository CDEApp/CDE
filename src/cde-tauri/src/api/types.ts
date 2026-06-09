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
}

export interface CatalogsResponse {
  catalogs: CatalogInfo[];
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

export type ShellAction = "open" | "explore" | "properties" | "custom";

export interface ShellRequest {
  ref: string;
  action: ShellAction;
  commandId?: number;
}
