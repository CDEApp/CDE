/**
 * Last-request-wins guard for async navigation (issue #80): a response is applied only if no newer
 * request began, and no reset (`invalidate`, e.g. a catalog reload) happened, since it started.
 */
export interface RequestGuard {
  /** Start a request; the returned token stays current until the next begin / invalidate. */
  begin: () => number;
  /** Make every in-flight request stale. */
  invalidate: () => void;
  isCurrent: (token: number) => boolean;
}

export function createRequestGuard(): RequestGuard {
  let seq = 0;
  return {
    begin: () => ++seq,
    invalidate: () => {
      seq++;
    },
    isCurrent: (token) => token === seq,
  };
}
