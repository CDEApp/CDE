import { ApiError } from "./api/client";

/** One-line syntax reminder for the Everything checkbox tooltip. */
export const EVERYTHING_SYNTAX_HINT = 'Everything-style search: space=AND  |=OR  !=NOT  <>=group  "phrase"  path:term  no-path:term  file:  folder:. Turns Regex off. Include path and the Files/Folders boxes are ignored: use path:, file: or folder:.';

/**
 * The status text for a search that threw. A 400 is the server refusing the query (for example a malformed
 * Everything query, whose message ends with "(at position N)"), so it reads as an invalid query, not a failure.
 */
export function searchErrorMessage(e: unknown): string {
  if (e instanceof ApiError && e.status === 400) return `Invalid query: ${e.body || e.message}`;
  return `Error: ${(e as Error).message}`;
}

/**
 * What the Files and Folders boxes show. Everything mode searches both and takes file: and folder: from the query,
 * so the boxes show both ticked and greyed out. The saved choice is not touched, so unticking Everything brings it back.
 */
export function kindControls(everythingMode: boolean, includeFiles: boolean, includeFolders: boolean) {
  return everythingMode
    ? { files: true, folders: true, disabled: true }
    : { files: includeFiles, folders: includeFolders, disabled: false };
}

/**
 * What the Include path box shows. Everything mode reaches the path with path: in the query, so the box shows
 * unticked and greyed out. The saved choice is not touched, so unticking Everything brings it back.
 */
export function includePathControl(everythingMode: boolean, includePath: boolean) {
  return everythingMode ? { checked: false, disabled: true } : { checked: includePath, disabled: false };
}
