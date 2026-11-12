import { refToString } from "./api/types";
import type { SearchResultRow } from "./api/types";
import { toHRString } from "./format";

export interface ResultsSummary {
  scope: "selection" | "results" | "shown";
  count: number;
  files: number;
  folders: number;
  /** Folder sizes are left out: a folder's size already contains its files, which may be in the same selection. */
  fileBytes: number;
}

interface Tally {
  files: number;
  folders: number;
  fileBytes: number;
}

function add(t: Tally, row: SearchResultRow) {
  if (row.isDirectory) t.folders++;
  else {
    t.files++;
    t.fileBytes += row.size;
  }
}

export function summarizeRows(rows: readonly SearchResultRow[], selected: ReadonlySet<string>, filtered = false): ResultsSummary {
  const all: Tally = { files: 0, folders: 0, fileBytes: 0 };
  const picked: Tally = { files: 0, folders: 0, fileBytes: 0 };
  let anyPicked = false;
  for (const row of rows) {
    const isPicked = selected.size > 0 && selected.has(refToString(row.ref));
    anyPicked ||= isPicked;
    add(all, row);
    if (isPicked) add(picked, row);
  }
  const t = anyPicked ? picked : all;
  const scope = anyPicked ? "selection" : filtered ? "shown" : "results";
  return { scope, count: t.files + t.folders, files: t.files, folders: t.folders, fileBytes: t.fileBytes };
}

export function summaryText(s: ResultsSummary): string {
  const folders = s.folders > 0 ? ` (${s.folders.toLocaleString()} ${s.folders === 1 ? "folder" : "folders"})` : "";
  const n = s.count.toLocaleString();
  const head = s.scope === "selection" ? `${n} selected` : s.scope === "shown" ? `${n} shown` : `All ${n}`;
  const files = `${s.files.toLocaleString()} ${s.files === 1 ? "file" : "files"}`;
  return `${head}${folders}, ${toHRString(s.fileBytes)} in ${files}`;
}
