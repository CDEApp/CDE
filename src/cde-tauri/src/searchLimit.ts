/** The result limits the search bar offers, ascending. */
export const SEARCH_LIMITS = [100, 1000, 10000, 100000];

/** The next limit up from `current`: the next offered one, or 10x past the largest. */
export function nextSearchLimit(current: number): number {
  return SEARCH_LIMITS.find((l) => l > current) ?? current * 10;
}
