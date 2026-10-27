import { describe, expect, it } from "vitest";
import { createRequestGuard } from "./requestGuard";

describe("createRequestGuard", () => {
  it("a request is current until something newer begins", () => {
    const g = createRequestGuard();
    const a = g.begin();
    expect(g.isCurrent(a)).toBe(true);
    const b = g.begin();
    expect(g.isCurrent(a)).toBe(false);
    expect(g.isCurrent(b)).toBe(true);
  });

  it("invalidate (a reset) makes every in-flight request stale", () => {
    const g = createRequestGuard();
    const a = g.begin();
    g.invalidate();
    expect(g.isCurrent(a)).toBe(false);
  });

  it("requests begun after a reset are current again", () => {
    const g = createRequestGuard();
    g.invalidate();
    expect(g.isCurrent(g.begin())).toBe(true);
  });
});
