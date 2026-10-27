// Pure logic for the "New Catalog" dialog: file-name rules (mirroring the sidecar's CatalogNaming, which stays
// authoritative) and the derivation of what the dialog shows from the preflight answer.
// No React, no network: unit-tested in newCatalog.test.ts.

import type { CreatePreflightResult } from "./api/types";

export const CATALOG_EXTENSION = ".cdex";
export const MAX_NAME_LENGTH = 150;

const RESERVED = new Set([
  "CON", "PRN", "AUX", "NUL",
  "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
  "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
]);

/** Trim, and drop a trailing `.cdex` the user typed (the extension is always appended). */
export function normalizeFileName(name: string): string {
  const t = (name ?? "").trim();
  return t.toLowerCase().endsWith(CATALOG_EXTENSION) ? t.slice(0, -CATALOG_EXTENSION.length).trimEnd() : t;
}

/** Why a (normalised, extension-less) name can't be used as a file name, or null when it is fine. */
export function validateFileName(name: string): string | null {
  if (!name || name.trim() === "") return "Enter a file name.";
  if (name.length > MAX_NAME_LENGTH) return `The file name is too long (max ${MAX_NAME_LENGTH} characters).`;
  if (/[\\/]/.test(name)) return "The file name can't contain path separators (\\ or /).";
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001f]/.test(name)) return "The file name can't contain control characters.";
  const bad = [...new Set(name.match(/[:*?"<>|]/g) ?? [])];
  if (bad.length > 0) return `The file name can't contain: ${bad.join(" ")}`;
  if (name.endsWith(".") || name.endsWith(" ")) return "The file name can't end with a dot or a space.";
  const stem = name.split(".")[0].trimEnd();
  if (RESERVED.has(stem.toUpperCase())) return `${stem} is a reserved name.`;
  return null;
}

/**
 * The catalog a create just added, found by the exact `targetPath` the sidecar reported (preflight). The sidecar
 * builds a catalog's `sourcePath` from the same string, so no case or separator rules are guessed here: on a
 * case-sensitive file system `Foo.cdex` and `foo.cdex` are different files.
 */
export function findCatalogByTarget<T extends { sourcePath: string | null }>(
  catalogs: readonly T[],
  targetPath: string | null,
): T | undefined {
  if (!targetPath) return undefined;
  return catalogs.find((c) => c.sourcePath === targetPath);
}

/**
 * Whether a preflight answer describes the dialog as it is now: asked for this root AND this effective name.
 * Anything else (the root was just changed, the user kept typing) is stale and must not enable Create.
 */
export function isPreflightCurrent(
  preflight: CreatePreflightResult | null,
  preflightRoot: string | null,
  rootPath: string,
  effectiveName: string,
): preflight is CreatePreflightResult {
  return preflight !== null && preflightRoot === rootPath && preflight.fileName === effectiveName;
}

export interface DialogInput {
  rootPath: string;
  /** What the user typed; null = untouched (the default name is used). */
  fileName: string | null;
  /** The latest preflight answer, or null while none has arrived. */
  preflight: CreatePreflightResult | null;
  /** The root that `preflight` / `preflightError` were asked for; answers for another root are ignored. */
  preflightRoot: string | null;
  /** The preflight call itself failed. */
  preflightError?: string | null;
}

export interface DialogState {
  /** The name Create will use (no extension). */
  effectiveName: string;
  /** Waiting for a preflight that matches the current name. */
  checking: boolean;
  nameError: string | null;
  /** The target file exists (never overwritten). */
  existsError: string | null;
  /** The existing file is a loaded catalog, so "Rescan existing" can be offered. */
  canRescanExisting: boolean;
  existingCatalogId: number | null;
  folderError: string | null;
  rootError: string | null;
  busyError: string | null;
  preflightError: string | null;
  /** Another loaded catalog has the same root: allowed. */
  sameRootWarning: string | null;
  canCreate: boolean;
}

export function deriveDialogState(input: DialogInput): DialogState {
  const { rootPath, fileName } = input;
  // Answers for another root (the user just pressed "Change…") are ignored entirely, even for one render.
  const sameRoot = input.preflightRoot === rootPath;
  const preflight = sameRoot ? input.preflight : null;
  const preflightError = sameRoot ? (input.preflightError ?? null) : null;
  const typed = fileName !== null;
  const effectiveName = typed ? normalizeFileName(fileName) : (preflight?.defaultFileName ?? "");

  // A preflight for a different name (the user kept typing) says nothing about this one.
  const current = isPreflightCurrent(preflight, input.preflightRoot, rootPath, effectiveName) ? preflight : null;
  const checking = current === null && preflightError === null;

  // The client rules apply as soon as there is a name to judge; before the default arrives there is none.
  let nameError: string | null = null;
  if (typed || preflight !== null) {
    nameError = validateFileName(effectiveName);
  }
  if (nameError === null && current?.fileNameError) nameError = current.fileNameError;

  const exists = !!current?.exists;
  const existingCatalogId = exists ? (current?.existingCatalogId ?? null) : null;
  const canRescanExisting = exists && existingCatalogId !== null;
  const existsError = exists
    ? canRescanExisting
      ? `${effectiveName}${CATALOG_EXTENSION} already exists in ${current!.catalogDir} and is a loaded catalog. It won't be overwritten.`
      : `${effectiveName}${CATALOG_EXTENSION} already exists in ${current!.catalogDir} (not a loaded catalog). It won't be overwritten; choose another name.`
    : null;

  let folderError: string | null = null;
  if (current) {
    if (!current.catalogDir) folderError = "There is no catalog folder to save to. Use File > Open Folder… first.";
    else if (!current.writable) folderError = `The catalog folder ${current.catalogDir} doesn't exist or can't be written to.`;
  }

  const rootError = current && !current.rootAccessible ? `${rootPath} can't be reached.` : null;
  const busyError = current?.busy ? "Catalogs are being loaded; try again when that finishes." : null;

  const same = current?.sameRootCatalogIds.length ?? 0;
  const sameRootWarning =
    same > 0
      ? `${same} loaded ${same === 1 ? "catalog already has" : "catalogs already have"} this root. You can still create another.`
      : null;

  const canCreate =
    current !== null &&
    nameError === null &&
    !exists &&
    folderError === null &&
    rootError === null &&
    busyError === null;

  return {
    effectiveName,
    checking,
    nameError,
    existsError,
    canRescanExisting,
    existingCatalogId,
    folderError,
    rootError,
    busyError,
    preflightError,
    sameRootWarning,
    canCreate,
  };
}
