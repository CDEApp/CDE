import { describe, expect, it } from "vitest";
import { nextSearchLimit } from "./searchLimit";

describe("nextSearchLimit", () => {
  it("steps to the next offered limit", () => {
    expect(nextSearchLimit(100)).toBe(1000);
    expect(nextSearchLimit(10000)).toBe(100000);
  });

  it("goes 10x past the largest offered limit", () => {
    expect(nextSearchLimit(100000)).toBe(1000000);
  });
});
