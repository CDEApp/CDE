import type { CatalogInfo, HashJobProgress, HashOptions, HashPreflightResult, HashResult, JobPhase, JobResult } from "./api/types";
import { toHRString } from "./format";

const n = (v: number) => v.toLocaleString("en-US");
const files = (v: number) => `${n(v)} ${v === 1 ? "file" : "files"}`;

export const COMPARED_ACROSS_CATALOGS = "Files are compared by size across the catalogs you pick.";

export function isLegacyCatalog(c: Pick<CatalogInfo, "sourcePath">): boolean {
  return /\.cde$/i.test(c.sourcePath ?? "");
}

export function hashOptions(filter: string, includeUniqueSizes: boolean): HashOptions | null {
  const f = filter.trim();
  return f === "" && !includeUniqueSizes ? null : { filter: f === "" ? null : f, includeUniqueSizes };
}

export function hashPreviewText(p: HashPreflightResult): string {
  const already = `${n(p.alreadyHashed)} already hashed`;
  if (p.files === 0) return `No files will be hashed (${already})`;
  return `${files(p.files)}, ${toHRString(p.bytes)} will be hashed (${already})`;
}

const PHASES: Partial<Record<JobPhase, string>> = {
  hashing: "partial hashes",
  "hashing-full": "full hashes",
  saving: "saving",
  swapping: "swapping in",
};

export function hashPhaseLabel(phase: JobPhase): string {
  return PHASES[phase] ?? phase;
}

export function hashProgressNote(phase: JobPhase, p: HashJobProgress | null | undefined): string {
  const parts = [hashPhaseLabel(phase)];
  if (p && (phase === "hashing" || phase === "hashing-full") && p.filesTotal > 0) {
    parts.push(`${n(p.filesDone)} of ${files(p.filesTotal)}`, `${toHRString(p.bytesDone)} of ${toHRString(p.bytesTotal)}`);
  }
  return parts.join(" · ");
}

export function hashResultNote(r: HashResult): string {
  const failed = r.filesFailed > 0 ? `; ${n(r.filesFailed)} could not be read` : "";
  return `${files(r.filesHashed)} hashed${failed}`;
}

export function createHashOutcome(r: JobResult, note: string | null | undefined): { text: string; clean: boolean } | null {
  if (r.hash) return { text: hashResultNote(r.hash), clean: r.hash.filesFailed === 0 };
  return note ? { text: `${note}. The catalog was kept without hashes`, clean: false } : null;
}

export function hashToast(r: HashResult): { title: string; message: string; color: "green" | "orange" } {
  const failed =
    r.filesFailed > 0 ? ` ${files(r.filesFailed)} could not be read and stay unhashed.` : "";
  return {
    title: "Hashing complete",
    message: `Hashed ${files(r.filesHashed)}.${failed} Search again to see the new hashes.`,
    color: r.filesFailed > 0 ? "orange" : "green",
  };
}
