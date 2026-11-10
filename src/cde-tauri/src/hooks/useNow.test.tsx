// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useNow } from "./useNow";

describe("useNow", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-01T00:00:00Z"));
  });
  afterEach(() => vi.useRealTimers());

  it("moves forward on each interval, so ages and staleness keep up while the window stays open", () => {
    const { result } = renderHook(() => useNow(60_000));
    expect(result.current.toISOString()).toBe("2026-01-01T00:00:00.000Z");
    act(() => vi.advanceTimersByTime(60_000));
    expect(result.current.toISOString()).toBe("2026-01-01T00:01:00.000Z");
  });
});
