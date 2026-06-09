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
