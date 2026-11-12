import type { SearchQuery, SearchResultRow } from "./api/types";
import { filterTerm } from "./everythingSearch";

export type FilterMode = "substring" | "regex";
export type FilterField = "name" | "path";

export type CompiledFilter =
  | { kind: "none" }
  | { kind: "ok"; test: (row: SearchResultRow) => boolean }
  | { kind: "error"; message: string };

const NONE: CompiledFilter = { kind: "none" };

export function compileFilter(text: string, mode: FilterMode, field: FilterField): CompiledFilter {
  if (text === "") return NONE;
  const textOf = field === "path" ? (r: SearchResultRow) => r.fullPath : (r: SearchResultRow) => r.name;
  if (mode === "substring") {
    const needle = text.toLowerCase();
    return { kind: "ok", test: (r) => textOf(r).toLowerCase().includes(needle) };
  }
  try {
    const re = new RegExp(text, "i");
    return { kind: "ok", test: (r) => re.test(textOf(r)) };
  } catch (e) {
    return { kind: "error", message: e instanceof Error ? e.message : String(e) };
  }
}

export function countText(shown: number, total: number): string {
  return `${shown.toLocaleString()} of ${total.toLocaleString()} shown`;
}

export function combineQuery(last: SearchQuery, text: string, mode: FilterMode, field: FilterField): SearchQuery {
  const parts: string[] = [];
  const pattern = last.pattern ?? "";
  if (pattern.trim() !== "") {
    parts.push(
      last.everythingMode
        ? `<${pattern}>`
        : filterTerm(pattern, last.includePath ? "path" : "name", !!last.regexMode),
    );
  }
  if (!last.everythingMode) {
    const files = last.includeFiles ?? true;
    const folders = last.includeFolders ?? true;
    if (files && !folders) parts.push("file:");
    else if (folders && !files) parts.push("folder:");
  }
  parts.push(filterTerm(text, field, mode === "regex"));
  return {
    ...last,
    pattern: parts.join(" "),
    everythingMode: true,
    regexMode: false,
    includePath: false,
    includeFiles: true,
    includeFolders: true,
  };
}
