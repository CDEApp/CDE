import { useCallback, useRef, useState } from "react";
import type { CdeApiClient } from "../api/client";
import type { SearchQuery, SearchResultRow } from "../api/types";
import { staleResultsBanner } from "../rescan";
import { searchErrorMessage } from "../everythingSearch";
import type { UiStateManager } from "../state/uiState";

export interface UseSearchOptions {
  getClient: () => CdeApiClient;
  getUi: () => UiStateManager | null;
  /** Called when a search starts, so the caller can switch to the results view. */
  onStart: () => void;
}

/** Streaming search: the results, busy/timing text, the last query, and which catalogs were rescanned since. */
export function useSearch({ getClient, getUi, onStart }: UseSearchOptions) {
  const searchAbort = useRef<AbortController | null>(null);
  const lastQueryRef = useRef<SearchQuery | null>(null); // for the stale-results "Search again"
  const [results, setResults] = useState<SearchResultRow[]>([]);
  const [busy, setBusy] = useState(false);
  const [timing, setTiming] = useState("");
  // Catalogs rescanned since the current results were produced (drives the stale banner).
  const [rescannedSinceSearch, setRescannedSinceSearch] = useState<ReadonlySet<number>>(new Set());

  const runSearch = useCallback(
    async (query: SearchQuery) => {
      const client = getClient();
      const ui = getUi();
      lastQueryRef.current = query;
      ui?.pushSearchHistory(query.pattern);
      ui?.update({
        regexMode: query.regexMode,
        everythingMode: query.everythingMode,
        includePath: query.includePath,
        includeFiles: query.includeFiles,
        includeFolders: query.includeFolders,
        limitResultCount: query.limitResultCount,
      });

      onStart();
      setBusy(true);
      setTiming("Searching…");

      const abort = new AbortController();
      searchAbort.current = abort;
      const start = performance.now();
      const buffer: SearchResultRow[] = [];
      // The previous results stay until the search really starts: a refused query (a 400, such as a malformed
      // Everything query) leaves them alone and only shows the message.
      let started = false;
      const begin = () => {
        if (started) return;
        started = true;
        setResults([]);
        setRescannedSinceSearch(new Set()); // fresh results are not stale
      };
      let flush: ReturnType<typeof setTimeout> | null = null;
      const scheduleFlush = () => {
        if (flush) return;
        flush = setTimeout(() => {
          flush = null;
          setResults([...buffer]);
        }, 100);
      };

      try {
        await client.search(query, {
          signal: abort.signal,
          onResult: (row) => {
            begin();
            buffer.push(row);
            scheduleFlush();
          },
          onProgress: (p) => {
            begin();
            setTiming(`${p.total > 0 ? Math.round((100 * p.count) / p.total) : 0}%`);
          },
          onDone: (count) => {
            begin();
            setResults([...buffer]);
            setTiming(`${count.toLocaleString()} results in ${Math.round(performance.now() - start)} ms`);
          },
        });
      } catch (e) {
        setTiming(searchErrorMessage(e));
      } finally {
        if (flush) clearTimeout(flush);
        if (started) setResults([...buffer]);
        setBusy(false);
        searchAbort.current = null;
      }
    },
    [getClient, getUi, onStart],
  );

  const cancelSearch = useCallback(() => {
    searchAbort.current?.abort();
  }, []);

  const searchAgain = useCallback(async () => {
    const q = lastQueryRef.current;
    if (q) await runSearch(q);
  }, [runSearch]);

  /** A catalog was rescanned: results pointing into it are now out of date. */
  const markRescanned = useCallback((catalogId: number) => {
    setRescannedSinceSearch((prev) => new Set(prev).add(catalogId));
  }, []);

  const staleBanner = (nameOf: (catalogId: number) => string) =>
    staleResultsBanner(results, rescannedSinceSearch, nameOf);

  return { results, busy, timing, runSearch, cancelSearch, searchAgain, markRescanned, staleBanner };
}
