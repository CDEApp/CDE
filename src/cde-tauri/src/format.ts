const SUFFIX = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB"];

/**
 * Human-readable size, mirroring cdeAppCore SizeFormatter.ToHRString: rounds half away from zero.
 * Cases shared with the C# tests live in src/testdata/size-format-cases.json.
 */
export function toHRString(val: number): string {
  if (val === 0) return "0";
  let place = Math.floor(Math.log(val) / Math.log(1024));
  let num = Math.round((val / Math.pow(1024, place)) * 10) / 10;
  if (num >= 1024 && place < SUFFIX.length - 1) {
    // 1023.96 KB rounds to 1024; show it as 1 MB instead.
    place++;
    num = Math.round((val / Math.pow(1024, place)) * 10) / 10;
  }
  return `${num} ${SUFFIX[place]}`;
}

/**
 * Size cell for the directory listing and search results: human-readable, annotated for directories.
 * Directories match cdeAppCore's FormatDirectorySizeCell; files differ (cdeWin still shows raw bytes).
 */
export function sizeCell(isDirectory: boolean, size: number, isReparsePoint: boolean): string {
  if (!isDirectory) return toHRString(size);
  return `${toHRString(size)} <Dir${isReparsePoint ? " R" : ""}>`;
}

/** Size cell tooltip: the exact byte count, digit-grouped for `locale` (default: the user's). */
export function sizeTitle(size: number, locale?: string): string {
  return `${size.toLocaleString(locale)} ${size === 1 ? "byte" : "bytes"}`;
}

/**
 * A catalog's display name: the base name of its file (`CatalogInfo.actualFileName`, which may be
 * a bare name or a full path) without a `.cdex` / `.cde` extension. Empty when there is none.
 */
export function catalogName(fileName: string | null | undefined): string {
  if (!fileName) return "";
  const base = fileName.slice(Math.max(fileName.lastIndexOf("\\"), fileName.lastIndexOf("/")) + 1);
  return base.replace(/\.cdex?$/i, "");
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
  const m = /^(\d*\.\d+|\d+)\s*([a-zA-Z]*)$/.exec(s);
  if (!m) return null;
  const mult = SIZE_UNITS[m[2].toUpperCase()];
  if (mult === undefined) return null;
  return Math.round(Number.parseFloat(m[1]) * mult);
}

export type AgeUnit = "hours" | "days" | "months" | "years";

export const AGE_UNITS: { value: AgeUnit; label: string }[] = [
  { value: "hours", label: "Hour(s)" },
  { value: "days", label: "Day(s)" },
  { value: "months", label: "Month(s)" },
  { value: "years", label: "Year(s)" },
];

/**
 * Parse a "not older than" amount: a whole number ≥ 0, else null. Blank is null (invalid when the
 * filter is enabled), so an unfilled box can't silently mean "0 = only things modified just now".
 */
export function parseAge(text: string): number | null {
  const s = text.trim();
  return /^[0-9]+$/.test(s) ? Number(s) : null;
}

/**
 * The cutoff for "modified within the last `amount` `unit`": `now` stepped back in local time,
 * like cdeWin's DateTime.Now.AddX(-amount). Months/years clamp the day to the target month's
 * length (Mar 31 − 1 month = Feb 28/29), matching .NET AddMonths rather than JS Date's roll-over.
 */
export function notOlderThanCutoff(amount: number, unit: AgeUnit, now: Date = new Date()): Date {
  const d = new Date(now);
  switch (unit) {
    case "hours":
      d.setHours(d.getHours() - amount);
      return d;
    case "days":
      d.setDate(d.getDate() - amount);
      return d;
    case "months":
      return addMonthsClamped(now, -amount);
    case "years":
      return addMonthsClamped(now, -12 * amount);
  }
}

function addMonthsClamped(from: Date, months: number): Date {
  const total = from.getFullYear() * 12 + from.getMonth() + months;
  const year = Math.floor(total / 12);
  const month = total - year * 12;
  const lastDay = new Date(year, month + 1, 0).getDate();
  return new Date(year, month, Math.min(from.getDate(), lastDay),
    from.getHours(), from.getMinutes(), from.getSeconds(), from.getMilliseconds());
}

/**
 * Format as a local wall-clock ISO timestamp with no offset ("2026-10-27T09:30:00"). The API binds
 * it as an unspecified-kind DateTime with these clock values, which is how catalog modified times
 * (local) are compared — a UTC "Z" string would shift the cutoff by the UTC offset.
 */
export function toLocalIsoString(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}
