import { useEffect, useMemo, useRef, useState } from "react";
import type { CdeApiClient } from "../api/client";
import type { HashPreflightRequest, HashPreflightResult } from "../api/types";
import { createRequestGuard } from "../requestGuard";

export type HashPreview =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "ready"; result: HashPreflightResult }
  | { status: "error"; message: string };

export const HASH_PREVIEW_DEBOUNCE_MS = 300;

export function useHashPreview(client: CdeApiClient | null, request: HashPreflightRequest | null): HashPreview {
  const [preview, setPreview] = useState<HashPreview>({ status: "idle" });
  const guard = useRef(createRequestGuard()).current;
  const key = request === null ? null : JSON.stringify(request);
  const stable = useMemo(() => (key === null ? null : (JSON.parse(key) as HashPreflightRequest)), [key]);

  useEffect(() => {
    const token = guard.begin();
    if (stable === null || client === null) {
      setPreview({ status: "idle" });
      return;
    }
    setPreview({ status: "loading" });
    const abort = new AbortController();
    const timer = window.setTimeout(() => {
      client
        .preflightHash(stable, abort.signal)
        .then((result) => {
          if (guard.isCurrent(token)) setPreview({ status: "ready", result });
        })
        .catch((e) => {
          if (!guard.isCurrent(token) || (e as Error)?.name === "AbortError") return;
          setPreview({ status: "error", message: e instanceof Error ? e.message : String(e) });
        });
    }, HASH_PREVIEW_DEBOUNCE_MS);
    return () => {
      window.clearTimeout(timer);
      abort.abort();
      guard.invalidate();
    };
  }, [client, stable, guard]);

  return preview;
}
