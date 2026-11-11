import { refToString } from "./api/types";
import type { SearchResultRow } from "./api/types";
import { toHRString } from "./format";

export interface ResultsSummary {
  scope: "selection" | "results";
  count: number;
  files: number;
  folders: number;
  /** Folder sizes are left out: a folder's size already contains its files, which may be in the same selection. */
  fileBytes: number;
}

/** One pass over `rows`: the selected rows when `selected` is non-empty, otherwise all of them. */
export function summarizeRows(rows: readonly SearchResultRow[], selected: ReadonlySet<string>): ResultsSummary {
  const scope = selected.size > 0 ? "selection" : "results";
  let files = 0;
  let folders = 0;
  let fileBytes = 0;
  for (const row of rows) {
    if (scope === "selection" && !selected.has(refToString(row.ref))) continue;
    if (row.isDirectory) folders++;
    else {
      files++;
      fileBytes += row.size;
    }
  }
  return { scope, count: files + folders, files, folders, fileBytes };
}

export function summaryText(s: ResultsSummary): string {
  const folders = s.folders > 0 ? ` (${s.folders.toLocaleString()} ${s.folders === 1 ? "folder" : "folders"})` : "";
  const head = s.scope === "selection" ? `${s.count.toLocaleString()} selected` : `All ${s.count.toLocaleString()}`;
  const files = `${s.files.toLocaleString()} ${s.files === 1 ? "file" : "files"}`;
  return `${head}${folders}, ${toHRString(s.fileBytes)} in ${files}`;
}
