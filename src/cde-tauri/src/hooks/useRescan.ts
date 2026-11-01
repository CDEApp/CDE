import { useCallback, useRef, useState } from "react";
import { notifications } from "@mantine/notifications";
import { CdeApiClient, RescanFailure } from "../api/client";
import type { CatalogsResponse } from "../api/types";
import { skipSummary } from "../format";
import { beginRescan, emptyRescanState, endRescan, isRescanning, updateRescan, type RescanState } from "../rescan";

export interface UseRescanOptions {
  getClient: () => CdeApiClient;
  getCatalogs: () => CatalogsResponse | null;
  /** Runs once the sidecar has swapped the rescanned catalog in: refresh the views. A throw becomes a "not refreshed" toast. */
  afterSwap: (client: CdeApiClient, catalogId: number) => Promise<void>;
}

/** Background rescans: which catalogs are rescanning, their cancel handles, and the unreachable-root modal. */
export function useRescan({ getClient, getCatalogs, afterSwap }: UseRescanOptions) {
  const [rescans, setRescans] = useState<RescanState>(emptyRescanState);
  const rescanAborts = useRef(new Map<number, AbortController>());
  const [unreachableRoot, setUnreachableRoot] = useState<string | null>(null);
  // The ref is the source of truth, so a second click in the same tick sees a rescan as running; the state mirrors it
  // for rendering. Only written from handlers, never during render.
  const rescansRef = useRef(rescans);
  const update = useCallback((change: (s: RescanState) => RescanState) => {
    rescansRef.current = change(rescansRef.current);
    setRescans(rescansRef.current);
  }, []);
  const getRescans = useCallback(() => rescansRef.current, []);

  const rescanCatalog = useCallback(
    async (catalogId: number) => {
      const client = getClient();
      if (isRescanning(rescansRef.current, catalogId)) return;
      const before = getCatalogs()?.catalogs.find((c) => c.catalogId === catalogId);
      const label = before?.rootPath ?? `catalog ${catalogId}`;
      const abort = new AbortController();
      rescanAborts.current.set(catalogId, abort);
      update((s) => beginRescan(s, catalogId, label));
      try {
        // Phase 1: the rescan's own outcome decides the success / failure toast.
        let finished = false;
        let entries = 0;
        let skipped: string | null = null;
        try {
          const result = await client.rescan(catalogId, {
            signal: abort.signal,
            onProgress: (p) =>
              update((s) => updateRescan(s, catalogId, { phase: p.phase, entriesScanned: p.entriesScanned })),
          });
          finished = result !== null;
          entries = result?.entries ?? 0;
          skipped = skipSummary(result?.excludedFolderCount, result?.accessDeniedFolderCount, result?.unreadableFolderCount);
          if (!finished) {
            // Cancelled. If the cancel lost the race with the swap the catalog did change: detect that.
            try {
              const now = (await client.catalogs()).catalogs.find((c) => c.catalogId === catalogId);
              if (now && before && now.scanEndUtcTicks !== before.scanEndUtcTicks) {
                finished = true;
                entries = now.dirEntryCount + now.fileEntryCount;
              }
            } catch {
              /* could not check: report it as cancelled */
            }
          }
        } catch (e) {
          if (e instanceof RescanFailure && e.rootNotAccessible) {
            setUnreachableRoot(e.error.rootPath || label);
          } else {
            notifications.show({
              color: "red",
              title: "Rescan failed",
              message: `${label}: ${e instanceof Error ? e.message : String(e)}. The catalog was left unchanged.`,
            });
          }
          return;
        }

        if (!finished) {
          notifications.show({ color: "gray", title: "Rescan cancelled", message: `${label} was left unchanged.` });
          return;
        }

        // Phase 2: the swap succeeded. A failed view refresh must not be reported as a failed rescan.
        try {
          await afterSwap(client, catalogId);
          notifications.show({
            color: "green",
            title: "Rescan complete",
            message: `Rescanned ${label} — ${entries.toLocaleString()} entries${skipped ? `; ${skipped}` : ""}`,
          });
        } catch (e) {
          notifications.show({
            color: "orange",
            title: "Rescan complete, view not refreshed",
            message: `Rescanned ${label}, but refreshing the view failed: ${
              e instanceof Error ? e.message : String(e)
            } — use Reload.`,
          });
        }
      } finally {
        rescanAborts.current.delete(catalogId);
        update((s) => endRescan(s, catalogId));
      }
    },
    [getClient, getCatalogs, afterSwap, update],
  );

  const cancelRescan = useCallback((catalogId: number) => {
    rescanAborts.current.get(catalogId)?.abort();
  }, []);

  const clearUnreachable = useCallback(() => setUnreachableRoot(null), []);

  return { rescans, getRescans, unreachableRoot, clearUnreachable, rescanCatalog, cancelRescan };
}
