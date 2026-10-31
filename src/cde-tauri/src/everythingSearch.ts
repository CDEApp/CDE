import { ApiError } from "./api/client";

/** One-line syntax reminder for the Everything checkbox tooltip. */
export const EVERYTHING_SYNTAX_HINT = 'Everything-style search: space=AND  |=OR  !=NOT  <>=group  "phrase"  path:term  no-path:term. Turns Regex off.';

/**
 * The status text for a search that threw. A 400 is the server refusing the query (for example a malformed
 * Everything query, whose message ends with "(at position N)"), so it reads as an invalid query, not a failure.
 */
export function searchErrorMessage(e: unknown): string {
  if (e instanceof ApiError && e.status === 400) return `Invalid query: ${e.body || e.message}`;
  return `Error: ${(e as Error).message}`;
}
