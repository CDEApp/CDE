import { describe, expect, it } from "vitest";
import {
  hashOptions,
  hashPreviewText,
  hashProgressNote,
  hashResultNote,
  hashToast,
  isLegacyCatalog,
} from "./hashJobs";

describe("hashOptions", () => {
  it("is null when nothing narrows or widens the default selection", () => {
    expect(hashOptions("", false)).toBeNull();
    expect(hashOptions("   ", false)).toBeNull();
  });

  it("trims the filter and carries the flag", () => {
    expect(hashOptions(" size:>2gb ", false)).toEqual({ filter: "size:>2gb", includeUniqueSizes: false });
    expect(hashOptions("", true)).toEqual({ filter: null, includeUniqueSizes: true });
  });
});

describe("hashPreviewText", () => {
  it("gives the file count, the size and the already hashed count", () => {
    expect(hashPreviewText({ files: 1234, bytes: 3 * 1024 ** 3, alreadyHashed: 12 })).toBe(
      "1,234 files, 3 GB will be hashed (12 already hashed)",
    );
  });

  it("says one file in the singular and says so when there is nothing to hash", () => {
    expect(hashPreviewText({ files: 1, bytes: 2048, alreadyHashed: 0 })).toBe("1 file, 2 KB will be hashed (0 already hashed)");
    expect(hashPreviewText({ files: 0, bytes: 0, alreadyHashed: 5 })).toBe("No files will be hashed (5 already hashed)");
  });
});

describe("hash job text", () => {
  const p = { filesDone: 40, filesTotal: 1000, bytesDone: 1024 ** 2, bytesTotal: 5 * 1024 ** 3 };

  it("shows the pass, the files and the bytes of a hashing phase", () => {
    expect(hashProgressNote("hashing", p)).toBe("partial hashes · 40 of 1,000 files · 1 MB of 5 GB");
    expect(hashProgressNote("hashing-full", p)).toMatch(/^full hashes · 40 of 1,000 files/);
  });

  it("shows only the phase before the first report and while saving", () => {
    expect(hashProgressNote("hashing", null)).toBe("partial hashes");
    expect(hashProgressNote("saving", p)).toBe("saving");
    expect(hashProgressNote("swapping", p)).toBe("swapping in");
  });

  it("reports the result, with failures", () => {
    expect(hashResultNote({ filesHashed: 1500, filesFailed: 0 })).toBe("1,500 files hashed");
    expect(hashResultNote({ filesHashed: 1, filesFailed: 3 })).toBe("1 file hashed; 3 could not be read");
  });

  it("turns the toast orange when files failed", () => {
    expect(hashToast({ filesHashed: 2, filesFailed: 0 })).toMatchObject({
      color: "green",
      message: expect.stringContaining("Hashed 2 files."),
    });
    const t = hashToast({ filesHashed: 2, filesFailed: 1 });
    expect(t.color).toBe("orange");
    expect(t.message).toContain("1 file could not be read and stay unhashed");
  });
});

describe("isLegacyCatalog", () => {
  it("is true only for a .cde source file", () => {
    expect(isLegacyCatalog({ sourcePath: "C:\\x\\a.cde" })).toBe(true);
    expect(isLegacyCatalog({ sourcePath: "C:\\x\\a.cdex" })).toBe(false);
    expect(isLegacyCatalog({ sourcePath: null })).toBe(false);
  });
});
