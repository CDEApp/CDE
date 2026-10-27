import { describe, expect, it } from "vitest";
import { buildApiUrl, idSegment, refSegment } from "./client";
import { refToString } from "./types";

describe("buildApiUrl", () => {
  const base = "http://127.0.0.1:5000";

  it("joins plain segments", () => {
    expect(buildApiUrl(base, ["catalogs"])).toBe("http://127.0.0.1:5000/catalogs");
    expect(buildApiUrl(base, ["catalogs", 3, "rescan"])).toBe("http://127.0.0.1:5000/catalogs/3/rescan");
  });

  it("leaves refToString output unchanged so the server route parses it", () => {
    const ref = { catalogId: 2, entryIndex: 1234 };
    expect(buildApiUrl(base, ["entries", refToString(ref), "children"])).toBe(
      "http://127.0.0.1:5000/entries/2-1234/children",
    );
  });

  it("percent-encodes segments so they cannot add path, query or fragment parts", () => {
    expect(buildApiUrl(base, ["entries", "a/b?c#d", "path"])).toBe(
      "http://127.0.0.1:5000/entries/a%2Fb%3Fc%23d/path",
    );
  });

  it("appends and encodes query values", () => {
    expect(buildApiUrl(base, ["session", "reload"], { path: "C:\\a b&c" })).toBe(
      "http://127.0.0.1:5000/session/reload?path=C%3A%5Ca+b%26c",
    );
    expect(buildApiUrl(base, ["x"], new URLSearchParams({ depth: "3" }))).toBe("http://127.0.0.1:5000/x?depth=3");
  });

  it("omits the query string when empty and tolerates a trailing slash on the base", () => {
    expect(buildApiUrl(`${base}/`, ["x"], new URLSearchParams())).toBe("http://127.0.0.1:5000/x");
  });
});

describe("refSegment / idSegment", () => {
  it("formats a ref the way the server route parses it", () => {
    expect(refSegment({ catalogId: 2, entryIndex: 1234 })).toBe("2-1234");
    expect(refSegment({ catalogId: 0, entryIndex: 0 })).toBe("0-0");
    expect(idSegment(7)).toBe("7");
  });

  it("rejects anything the server route would not accept", () => {
    // A ref comes back from the sidecar as JSON, so at runtime its fields need not be numbers.
    const bad = [-1, 1.5, Number.NaN, Number.POSITIVE_INFINITY, 2 ** 31, "1/../x", "3", null, undefined];
    for (const v of bad) {
      expect(() => idSegment(v as number)).toThrow();
      expect(() => refSegment({ catalogId: v as number, entryIndex: 1 })).toThrow();
      expect(() => refSegment({ catalogId: 1, entryIndex: v as number })).toThrow();
    }
  });
});
