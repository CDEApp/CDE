import { describe, expect, it } from "vitest";
import {
  compareVersions,
  draftsForTag,
  latestDraft,
  latestStable,
  nextPrerelease,
  parseReleases,
  parseVersion,
  validatePrerelease,
  validateReleaseTag,
  validateStable,
} from "./release-version.mjs";

describe("validateReleaseTag", () => {
  const tags = ["1.7.1", "2.0.0"];
  it("accepts a higher stable tag, whether or not it is in the list yet", () => {
    expect(validateReleaseTag("2.1.0", tags).errors).toEqual([]);
    expect(validateReleaseTag("2.1.0", [...tags, "2.1.0"]).errors).toEqual([]);
  });
  it("accepts a re-run of the tag that is the latest stable tag", () => {
    expect(validateReleaseTag("2.0.0", tags).errors).toEqual([]);
  });
  it("rejects a stable tag lower than the latest stable tag, in the list or not", () => {
    expect(validateReleaseTag("1.9.0", tags).errors.join()).toContain("not higher than the latest tag 2.0.0");
    expect(validateReleaseTag("1.9.0", [...tags, "1.9.0"]).errors).toHaveLength(1);
  });
  it("ignores pre-release tags when comparing a stable tag", () => {
    expect(validateReleaseTag("2.1.0", ["2.0.0", "2.1.0-rc.2"]).errors).toEqual([]);
  });
  it("accepts a pre-release on a higher base, new or a re-run", () => {
    expect(validateReleaseTag("2.1.0-beta.1", tags).errors).toEqual([]);
    expect(validateReleaseTag("2.1.0-beta.1", [...tags, "2.1.0-beta.1"]).errors).toEqual([]);
  });
  it("rejects a pre-release whose base is not above the latest stable tag", () => {
    expect(validateReleaseTag("2.0.0-rc.1", [...tags, "2.0.0-rc.1"]).errors.join()).toContain("not higher than");
    expect(validateReleaseTag("1.9.0-beta.1", tags).errors).toHaveLength(1);
  });
  it("rejects a tag that is not a version", () => {
    expect(validateReleaseTag("v2.1.0", tags).errors).toHaveLength(1);
    expect(validateReleaseTag("2.1.0-beta.0", tags).errors).toHaveLength(1);
  });
});

describe("releases JSON", () => {
  const rel = (id, tag_name, created_at, draft = true) => ({ id, tag_name, created_at, draft });
  const releases = [
    rel(1, "2.0.0", "2022-10-03", false),
    rel(2, "2.1.0", "2026-01-01"),
    rel(3, "2.1.0-beta.1", "2026-03-01"),
    rel(4, "2.2.0", "2026-02-01"),
    rel(5, "2.2.0", "2026-02-15"),
    rel(6, "untagged-abc", "2026-04-01"),
  ];

  describe("latestDraft", () => {
    it("is null with no drafts", () => {
      expect(latestDraft([])).toBeNull();
      expect(latestDraft([rel(1, "2.0.0", "2022-10-03", false)])).toBeNull();
    });
    it("takes the newest stable-named draft", () => {
      expect(latestDraft(releases)).toBe("2.2.0");
    });
    it("ignores pre-release-named and odd drafts, and published releases", () => {
      expect(latestDraft([rel(3, "2.1.0-beta.1", "2026-03-01"), rel(6, "untagged-abc", "2026-04-01")])).toBeNull();
      expect(latestDraft([rel(1, "9.0.0", "2030-01-01", false), rel(2, "2.1.0", "2026-01-01")])).toBe("2.1.0");
    });
  });

  describe("draftsForTag", () => {
    it("lists the ids newest first", () => {
      expect(draftsForTag("2.2.0", releases)).toEqual([5, 4]);
    });
    it("is empty for no draft, and ignores published releases", () => {
      expect(draftsForTag("2.0.0", releases)).toEqual([]);
      expect(draftsForTag("9.9.9", releases)).toEqual([]);
    });
    it("finds a pre-release draft by its exact tag", () => {
      expect(draftsForTag("2.1.0-beta.1", releases)).toEqual([3]);
    });
  });

  describe("parseReleases", () => {
    const page1 = JSON.stringify([rel(1, "2.1.0", "a")]);
    const page2 = JSON.stringify([rel(2, "2.2.0", "b")]);
    it("reads a single array", () => {
      expect(parseReleases(page1)).toHaveLength(1);
    });
    it("reads pages run together (gh --paginate)", () => {
      expect(parseReleases(`${page1}\n${page2}\n`).map((r) => r.id)).toEqual([1, 2]);
    });
    it("reads an array of pages (gh --paginate --slurp)", () => {
      expect(parseReleases(`[${page1},${page2}]`).map((r) => r.id)).toEqual([1, 2]);
    });
    it("is not fooled by brackets inside a string", () => {
      const tricky = JSON.stringify([{ id: 1, body: 'a ] [ b "] [" \\', draft: true, tag_name: "2.1.0", created_at: "a" }]);
      expect(parseReleases(`${tricky}${page2}`).map((r) => r.id)).toEqual([1, 2]);
    });
    it("reads empty output", () => {
      expect(parseReleases("")).toEqual([]);
      expect(parseReleases("[]")).toEqual([]);
    });
  });
});

describe("parseVersion", () => {
  it("parses a stable version", () => {
    expect(parseVersion("2.1.0")).toMatchObject({ major: 2, minor: 1, patch: 0, channel: null, n: null });
  });
  it("parses a pre-release", () => {
    expect(parseVersion("2.1.0-beta.3")).toMatchObject({ major: 2, minor: 1, patch: 0, channel: "beta", n: 3 });
  });
  it.each(["v2.1.0", "2.1", "2.1.0-beta", "2.1.0-beta.0", "2.1.0-beta.01", "2.1.0-gamma.1", "2.1.0-rc.1.2", "01.2.3", "", "2.1.0+x"])(
    "rejects %s",
    (v) => expect(parseVersion(v)).toBeNull(),
  );
  it("rejects non-strings", () => {
    expect(parseVersion(undefined)).toBeNull();
  });
});

describe("compareVersions", () => {
  it("orders the base numerically, not as text", () => {
    expect(compareVersions("2.10.0", "2.9.0")).toBeGreaterThan(0);
    expect(compareVersions("1.7.1", "2.0.0")).toBeLessThan(0);
  });
  it("orders alpha < beta < rc < stable for the same base", () => {
    const order = ["2.1.0-alpha.1", "2.1.0-beta.1", "2.1.0-rc.1", "2.1.0"];
    for (let i = 0; i < order.length; i++) {
      for (let j = 0; j < order.length; j++) {
        expect(Math.sign(compareVersions(order[i], order[j]))).toBe(Math.sign(i - j));
      }
    }
  });
  it("orders the number numerically", () => {
    expect(compareVersions("2.1.0-beta.10", "2.1.0-beta.9")).toBeGreaterThan(0);
  });
  it("puts a pre-release of a higher base above a lower stable", () => {
    expect(compareVersions("2.1.0-alpha.1", "2.0.0")).toBeGreaterThan(0);
  });
  it("treats equal versions as equal", () => {
    expect(compareVersions("2.1.0-rc.2", "2.1.0-rc.2")).toBe(0);
  });
});

describe("latestStable", () => {
  it("ignores pre-releases and other tags", () => {
    expect(latestStable(["1.7.1", "2.0.0", "2.1.0-beta.1", "foo", "v9.0.0"])).toBe("2.0.0");
  });
  it("is null when there is no stable tag", () => {
    expect(latestStable(["2.1.0-beta.1", "x"])).toBeNull();
    expect(latestStable([])).toBeNull();
  });
  it("picks the highest, whatever the order", () => {
    expect(latestStable(["2.9.0", "2.10.0", "2.2.0"])).toBe("2.10.0");
  });
});

describe("nextPrerelease", () => {
  const tags = ["2.0.0", "2.1.0-beta.1", "2.1.0-beta.2", "2.1.0-alpha.4", "2.2.0-beta.7"];
  it("starts at 1", () => {
    expect(nextPrerelease("2.1.0", "rc", tags)).toBe("2.1.0-rc.1");
  });
  it("is the highest N for that base and channel plus one", () => {
    expect(nextPrerelease("2.1.0", "beta", tags)).toBe("2.1.0-beta.3");
    expect(nextPrerelease("2.1.0", "alpha", tags)).toBe("2.1.0-alpha.5");
  });
  it("counts per base", () => {
    expect(nextPrerelease("2.2.0", "beta", tags)).toBe("2.2.0-beta.8");
    expect(nextPrerelease("2.3.0", "beta", tags)).toBe("2.3.0-beta.1");
  });
  it("uses the highest N even with gaps", () => {
    expect(nextPrerelease("2.1.0", "beta", ["2.1.0-beta.1", "2.1.0-beta.5"])).toBe("2.1.0-beta.6");
  });
  it("rejects a bad base or channel", () => {
    expect(() => nextPrerelease("2.1.0-beta.1", "beta", [])).toThrow();
    expect(() => nextPrerelease("2.1", "beta", [])).toThrow();
    expect(() => nextPrerelease("2.1.0", "gamma", [])).toThrow();
  });
});

describe("validateStable", () => {
  const tags = ["1.7.1", "2.0.0", "2.1.0-beta.1"];
  it("accepts a higher version", () => {
    expect(validateStable("2.1.0", tags).errors).toEqual([]);
  });
  it("accepts the stable of an existing pre-release base", () => {
    expect(validateStable("2.1.0", ["2.0.0", "2.1.0-rc.2"]).errors).toEqual([]);
  });
  it("rejects a pre-release version and a v prefix", () => {
    expect(validateStable("2.1.0-beta.2", tags).errors).toHaveLength(1);
    expect(validateStable("v2.1.0", tags).errors).toHaveLength(1);
  });
  it("rejects an existing tag", () => {
    expect(validateStable("2.0.0", tags).errors.join()).toContain("already exists");
  });
  it("rejects a version that is not higher than the latest stable tag", () => {
    expect(validateStable("1.9.0", tags).errors.join()).toContain("not higher than the latest tag 2.0.0");
  });
  it("names the tag and the draft when the draft is behind", () => {
    const { errors } = validateStable("1.9.0", tags, { fromDraft: true });
    expect(errors.join()).toContain("draft says 1.9.0");
    expect(errors.join()).toContain("latest tag is 2.0.0");
  });
  it("accepts anything when there is no tag yet", () => {
    expect(validateStable("0.0.1", []).errors).toEqual([]);
  });
});

describe("validatePrerelease", () => {
  const tags = ["2.0.0", "2.1.0-rc.1"];
  it("accepts a new tag on a higher base", () => {
    expect(validatePrerelease("2.2.0-beta.1", tags)).toEqual({ errors: [], warnings: [] });
  });
  it("rejects a stable version", () => {
    expect(validatePrerelease("2.2.0", tags).errors).toHaveLength(1);
  });
  it("rejects an existing tag", () => {
    expect(validatePrerelease("2.1.0-rc.1", tags).errors.join()).toContain("already exists");
  });
  it("rejects a base that is not higher than the latest stable", () => {
    expect(validatePrerelease("2.0.0-beta.1", tags).errors.join()).toContain("not higher than the latest stable tag 2.0.0");
    expect(validatePrerelease("1.9.0-beta.1", tags).errors).toHaveLength(1);
  });
  it("warns, but allows, going backwards in channel", () => {
    const r = validatePrerelease("2.1.0-alpha.1", tags);
    expect(r.errors).toEqual([]);
    expect(r.warnings.join()).toContain("2.1.0-rc.1");
  });
  it("does not warn for a later number in the same channel", () => {
    expect(validatePrerelease("2.1.0-rc.2", tags).warnings).toEqual([]);
  });
});
