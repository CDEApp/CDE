import { useCallback, useRef, useState } from "react";
import type { CdeApiClient } from "../api/client";
import type { SearchQuery, SearchResultRow } from "../api/types";
import { withDuplicateCounts } from "../resultColumns";
import { staleResultsBanner } from "../rescan";
import { searchErrorMessage } from "../everythingSearch";
import { nextSearchLimit } from "../searchLimit";
import type { UiStateManager } from "../state/uiState";

export interface UseSearchOptions {
  getClient: () => CdeApiClient;
  getUi: () => UiStateManager | null;
  /** Called when a search starts, so the caller can switch to the results view. */
  onStart: () => void;
  wantsDuplicateCounts: () => boolean;
}

/** Streaming search: the results, busy/timing text, the last query, and which catalogs were rescanned since. */
export function useSearch({ getClient, getUi, onStart, wantsDuplicateCounts }: UseSearchOptions) {
  const searchAbort = useRef<AbortController | null>(null);
  // The query behind the results on screen: a refused query leaves it alone, so Search again and Raise limit act on them.
  const lastQueryRef = useRef<SearchQuery | null>(null);
  const [results, setResults] = useState<SearchResultRow[]>([]);
  const [searchEpoch, setSearchEpoch] = useState(0);
  const [busy, setBusy] = useState(false);
  const [timing, setTiming] = useState("");
  const [limitReachedCount, setLimitReachedCount] = useState<number | null>(null);
  const [queryLimit, setQueryLimit] = useState<number | null>(null);
  // Catalogs rescanned since the current results were produced (drives the stale banner).
  const [rescannedSinceSearch, setRescannedSinceSearch] = useState<ReadonlySet<number>>(new Set());

  const runSearch = useCallback(
    async (requested: SearchQuery) => {
      const query = withDuplicateCounts(requested, wantsDuplicateCounts());
      const client = getClient();
      const ui = getUi();
      setQueryLimit(query.limitResultCount ?? null);
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

      searchAbort.current?.abort(); // a newer search supersedes a running one
      const abort = new AbortController();
      searchAbort.current = abort;
      const isLatest = () => searchAbort.current === abort; // a superseded search must not touch the state
      const start = performance.now();
      const buffer: SearchResultRow[] = [];
      // The previous results stay until the search really starts: a refused query (a 400, such as a malformed
      // Everything query) leaves them alone and only shows the message.
      let started = false;
      const begin = () => {
        if (started) return;
        started = true;
        lastQueryRef.current = query;
        setResults([]);
        setSearchEpoch((n) => n + 1);
        setLimitReachedCount(null);
        setRescannedSinceSearch(new Set()); // fresh results are not stale
      };
      let flush: ReturnType<typeof setTimeout> | null = null;
      const scheduleFlush = () => {
        if (flush) return;
        flush = setTimeout(() => {
          flush = null;
          if (isLatest()) setResults([...buffer]);
        }, 100);
      };

      try {
        await client.search(query, {
          signal: abort.signal,
          onResult: (row) => {
            if (!isLatest()) return;
            begin();
            buffer.push(row);
            scheduleFlush();
          },
          onProgress: (p) => {
            if (!isLatest()) return;
            begin();
            setTiming(`${p.total > 0 ? Math.round((100 * p.count) / p.total) : 0}%`);
          },
          onDone: (count, limitReached) => {
            if (!isLatest()) return;
            begin();
            setResults([...buffer]);
            setLimitReachedCount(limitReached ? count : null);
            setTiming(`${count.toLocaleString()} results in ${Math.round(performance.now() - start)} ms`);
          },
        });
      } catch (e) {
        if (isLatest()) setTiming(searchErrorMessage(e));
      } finally {
        if (flush) clearTimeout(flush);
        if (isLatest()) {
          if (started) setResults([...buffer]);
          setBusy(false);
          searchAbort.current = null;
        }
      }
    },
    [getClient, getUi, onStart, wantsDuplicateCounts],
  );

  const cancelSearch = useCallback(() => {
    searchAbort.current?.abort();
  }, []);

  const searchAgain = useCallback(async () => {
    const q = lastQueryRef.current;
    if (q) await runSearch(q);
  }, [runSearch]);

  const hasLastQuery = lastQueryRef.current !== null;
  const getLastQuery = useCallback(() => lastQueryRef.current, []);
  const raiseLimit = useCallback(async () => {
    const q = lastQueryRef.current;
    if (q) await runSearch({ ...q, limitResultCount: nextSearchLimit(q.limitResultCount ?? 10000) });
  }, [runSearch]);

  /** A catalog was rescanned: results pointing into it are now out of date. */
  const markRescanned = useCallback((catalogId: number) => {
    setRescannedSinceSearch((prev) => new Set(prev).add(catalogId));
  }, []);

  const staleBanner = (nameOf: (catalogId: number) => string) =>
    staleResultsBanner(results, rescannedSinceSearch, nameOf);

  return {
    results, searchEpoch, busy, timing, limitReachedCount, queryLimit, runSearch, cancelSearch, searchAgain, raiseLimit,
    getLastQuery, hasLastQuery, markRescanned, staleBanner,
  };
}
