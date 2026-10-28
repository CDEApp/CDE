import { afterEach, describe, expect, it, vi } from "vitest";
import { buildApiUrl, CdeApiClient, idSegment, MigrateFailure, refSegment } from "./client";
import { refToString, type MigrateProgress } from "./types";

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

describe("migrateLegacy", () => {
  const client = new CdeApiClient("http://127.0.0.1:5000", "tok");
  const sse = (...events: [string, unknown][]) =>
    new Response(events.map(([e, d]) => `event: ${e}\ndata: ${JSON.stringify(d)}\n\n`).join(""), { status: 200 });

  afterEach(() => vi.unstubAllGlobals());

  it("posts to /catalogs/migrate with the token, reports progress and resolves with the report", async () => {
    const fetch = vi.fn().mockResolvedValue(
      sse(
        ["progress", { current: 1, total: 2, path: "C:\\a.cde" }],
        ["progress", { current: 2, total: 2, path: "C:\\b.cde" }],
        ["done", { migrated: 2, total: 2, failures: [], warnings: [] }],
      ),
    );
    vi.stubGlobal("fetch", fetch);
    const progress: MigrateProgress[] = [];

    const result = await client.migrateLegacy({ onProgress: (p) => progress.push(p) });

    expect(fetch.mock.calls[0][0]).toBe("http://127.0.0.1:5000/catalogs/migrate");
    expect(fetch.mock.calls[0][1]).toMatchObject({ method: "POST", headers: { "X-CDE-Token": "tok" } });
    expect(progress.map((p) => p.current)).toEqual([1, 2]);
    expect(result).toEqual({ migrated: 2, total: 2, failures: [], warnings: [] });
  });

  it("throws MigrateFailure with the refusal's code", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ code: "busy", message: "job running" }), { status: 409 })),
    );

    const err = await client.migrateLegacy({}).catch((e: unknown) => e);

    expect(err).toBeInstanceOf(MigrateFailure);
    expect((err as MigrateFailure).error).toEqual({ code: "busy", message: "job running" });
  });

  it("throws MigrateFailure for an in-stream error", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(sse(["error", { code: "migrate-failed", message: "disk full" }])));

    await expect(client.migrateLegacy({})).rejects.toMatchObject({ error: { code: "migrate-failed" } });
  });

  it("resolves null when the stream ends without done (cancelled)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(sse(["progress", { current: 1, total: 9, path: "a.cde" }])));

    expect(await client.migrateLegacy({})).toBeNull();
  });
});
