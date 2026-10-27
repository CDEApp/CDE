import { describe, expect, it } from "vitest";
import { formatReleaseDate, releaseDateText, DEV_BUILD_TEXT } from "./buildInfo";

describe("formatReleaseDate", () => {
  it("formats an ISO date as a long UTC date", () => {
    expect(formatReleaseDate("2026-10-27")).toBe("27 October 2026");
    expect(formatReleaseDate("2026-01-05T23:59:59Z")).toBe("5 January 2026");
  });
  it("returns null for junk or empty input", () => {
    expect(formatReleaseDate("")).toBeNull();
    expect(formatReleaseDate("not a date")).toBeNull();
  });
});

describe("releaseDateText", () => {
  it("shows the development text in dev", () => {
    expect(releaseDateText("2026-10-27", true)).toBe(DEV_BUILD_TEXT);
    expect(DEV_BUILD_TEXT).toBe("development build");
  });
  it("shows the formatted date in a build", () => {
    expect(releaseDateText("2026-10-27", false)).toBe("27 October 2026");
  });
  it("falls back to unknown when the stamp is unusable", () => {
    expect(releaseDateText("", false)).toBe("unknown");
  });
});
