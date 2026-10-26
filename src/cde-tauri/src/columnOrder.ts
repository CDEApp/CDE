/**
 * Reconcile a persisted column order with the current column ids: unknown or duplicate ids are
 * dropped and any column missing from the saved order (e.g. one added in a later version) is
 * appended in its default position, so a stale ui-state can never hide or duplicate a column.
 */
export function normalizeColumnOrder(saved: unknown, defaultIds: readonly string[]): string[] {
  const known = new Set(defaultIds);
  const seen = new Set<string>();
  const order: string[] = [];
  if (Array.isArray(saved)) {
    for (const id of saved) {
      if (typeof id === "string" && known.has(id) && !seen.has(id)) {
        seen.add(id);
        order.push(id);
      }
    }
  }
  for (const id of defaultIds) if (!seen.has(id)) order.push(id);
  return order;
}

/**
 * Move column `id` so it lands at insertion point `toIndex` — a gap index 0..order.length in the
 * order as it was before the move (0 = before the first column, length = after the last).
 * Returns the same array when the move is a no-op.
 */
export function moveColumn(order: readonly string[], id: string, toIndex: number): readonly string[] {
  const from = order.indexOf(id);
  if (from < 0) return order;
  const to = Math.max(0, Math.min(order.length, toIndex));
  // Dropping into the gap just before or just after itself leaves it where it is.
  if (to === from || to === from + 1) return order;
  const next = order.filter((c) => c !== id);
  next.splice(to > from ? to - 1 : to, 0, id);
  return next;
}
