const SUFFIX = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB"];

/** Human-readable size, mirroring cdeAppCore SizeFormatter.ToHRString. */
export function toHRString(val: number): string {
  if (val === 0) return "0";
  const place = Math.floor(Math.log(val) / Math.log(1024));
  const num = Math.round((val / Math.pow(1024, place)) * 10) / 10;
  return `${num} ${SUFFIX[place]}`;
}

/** Directory-listing size cell: raw size for files, annotated for directories. */
export function sizeCell(isDirectory: boolean, size: number, isReparsePoint: boolean): string {
  if (!isDirectory) return String(size);
  return `${toHRString(size)} <Dir${isReparsePoint ? " R" : ""}>`;
}

export function formatDate(iso: string, isModifiedBad: boolean): string {
  if (isModifiedBad) return "<Bad Date>";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "<Bad Date>";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}/${pad(d.getMonth() + 1)}/${pad(d.getDate())} ${pad(d.getHours())}:${pad(
    d.getMinutes(),
  )}:${pad(d.getSeconds())}`;
}

export function mb(bytes: number): string {
  return `${Math.round(bytes / (1024 * 1024)).toLocaleString()} MB`;
}

const SIZE_UNITS: Record<string, number> = {
  "": 1,
  B: 1,
  K: 1024,
  KB: 1024,
  M: 1024 ** 2,
  MB: 1024 ** 2,
  G: 1024 ** 3,
  GB: 1024 ** 3,
  T: 1024 ** 4,
  TB: 1024 ** 4,
  P: 1024 ** 5,
  PB: 1024 ** 5,
};

/**
 * Parse a human-readable size into bytes: "25 KB", "2.5 MB", "4GB", "100" (bare number = bytes).
 * Units are 1024-based and case-insensitive, with optional space. Returns null for empty or
 * unparseable input.
 */
export function parseSize(text: string): number | null {
  const s = text.trim();
  if (s === "") return null;
  const m = /^([0-9]*\.?[0-9]+)\s*([a-zA-Z]*)$/.exec(s);
  if (!m) return null;
  const mult = SIZE_UNITS[m[2].toUpperCase()];
  if (mult === undefined) return null;
  return Math.round(parseFloat(m[1]) * mult);
}
