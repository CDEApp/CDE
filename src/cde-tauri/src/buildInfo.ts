export const DEV_BUILD_TEXT = "development build";
export const REPO_URL = "https://github.com/CDEApp/CDE";

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

/** "2026-10-27" or a full ISO timestamp -> "27 October 2026" (UTC, so it does not depend on the time zone); null if unparseable. */
export function formatReleaseDate(iso: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return `${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]} ${d.getUTCFullYear()}`;
}

/** What the About dialog shows as the release date. */
export function releaseDateText(buildDate: string, isDev: boolean): string {
  if (isDev) return DEV_BUILD_TEXT;
  return formatReleaseDate(buildDate) ?? "unknown";
}
