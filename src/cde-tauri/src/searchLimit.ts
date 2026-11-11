export const SEARCH_LIMITS = [100, 1000, 10000, 100000];

export function nextSearchLimit(current: number): number {
  return SEARCH_LIMITS.find((l) => l > current) ?? current * 10;
}
