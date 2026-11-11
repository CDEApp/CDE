import type { SearchResultRow } from "./api/types";
import { RESULT_COLUMN_BY_ID, visibleColumnIds } from "./resultColumns";

/**
 * Spreadsheet formula injection guard (CSV export only; the CLI CSV stays exact for scripts). A cell whose first
 * character is =, +, -, @, tab or CR is evaluated as a formula by Excel/Sheets, and file names are attacker
 * controlled. The OWASP-recommended fix is a leading single quote, which spreadsheets show as a text marker. It is
 * applied to the text columns only: Size is a number and Modified an ISO timestamp, and both must stay parseable.
 */
export function neutraliseFormula(text: string): string {
  return /^[=+\-@\t\r]/.test(text) ? `'${text}` : text;
}

/** RFC 4180 field: quoted only when it holds a comma, quote or line break; quotes are doubled. */
export function csvField(text: string): string {
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/**
 * CSV text (header + one CRLF-terminated line per row) of `rows` with the columns the results list
 * shows, in its order (the persisted order and visible set; stale or partial values are reconciled
 * to the known columns).
 */
export function resultsToCsv(rows: readonly SearchResultRow[], columnOrder: unknown, visibleColumns?: unknown): string {
  const cols = visibleColumnIds(columnOrder, visibleColumns).map((id) => RESULT_COLUMN_BY_ID.get(id)!);
  const lines = [cols.map((c) => csvField(c.header)).join(",")];
  for (const r of rows) lines.push(cols.map((c) => csvField(c.guardFormula ? neutraliseFormula(c.csv(r)) : c.csv(r))).join(","));
  return lines.join("\r\n") + "\r\n";
}

/** UTF-8 bytes with a leading BOM: Excel assumes the ANSI code page without it and garbles non-ASCII names. */
export function encodeCsv(csv: string): Uint8Array {
  const body = new TextEncoder().encode(csv);
  const out = new Uint8Array(3 + body.length);
  out.set([0xef, 0xbb, 0xbf]);
  out.set(body, 3);
  return out;
}

/** Suggested save-dialog file name, stamped with the local time so repeated exports do not collide. */
export function exportFileName(now: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  const date = `${now.getFullYear()}${p(now.getMonth() + 1)}${p(now.getDate())}`;
  return `cde-results-${date}-${p(now.getHours())}${p(now.getMinutes())}${p(now.getSeconds())}.csv`;
}
