import { describe, expect, it } from "vitest";
import type { CreatePreflightResult } from "./api/types";
import { deriveDialogState, normalizeFileName, validateFileName } from "./newCatalog";

function pf(over: Partial<CreatePreflightResult> = {}): CreatePreflightResult {
  return {
    defaultFileName: "D-Photos",
    fileName: "D-Photos",
    fileNameError: null,
    catalogDir: "C:\\catalogs",
    targetPath: "C:\\catalogs\\D-Photos.cdex",
    exists: false,
    existingCatalogId: null,
    writable: true,
    sameRootCatalogIds: [],
    rootAccessible: true,
    busy: false,
    ...over,
  };
}

describe("normalizeFileName", () => {
  it.each([
    ["D-Photos", "D-Photos"],
    ["  D-Photos  ", "D-Photos"],
    ["D-Photos.cdex", "D-Photos"],
    ["D-Photos.CDEX", "D-Photos"],
    ["", ""],
  ])("%j -> %j", (input, expected) => {
    expect(normalizeFileName(input)).toBe(expected);
  });
});

describe("validateFileName", () => {
  it.each(["D-Photos", "my catalog 2024", "a.b"])("accepts %j", (name) => {
    expect(validateFileName(name)).toBeNull();
  });

  it.each(["", "   ", "a\\b", "a/b", "a:b", "a*b", "a?b", 'a"b', "a<b", "a>b", "a|b", "name.", "name ", "CON", "nul.txt", "com1"])(
    "rejects %j with a reason",
    (name) => {
      expect(validateFileName(name)).toBeTruthy();
    },
  );

  it("rejects control characters and over-long names", () => {
    expect(validateFileName("a\u0001b")).toBeTruthy();
    expect(validateFileName("a".repeat(151))).toBeTruthy();
    expect(validateFileName("a".repeat(150))).toBeNull();
  });
});

describe("deriveDialogState", () => {
  const base = { rootPath: "D:\\Photos", fileName: null as string | null };

  it("is checking, with Create disabled, until the preflight arrives", () => {
    const s = deriveDialogState({ ...base, preflight: null });
    expect(s.checking).toBe(true);
    expect(s.canCreate).toBe(false);
    expect(s.nameError).toBeNull();
  });

  it("enables Create for a clean preflight and uses the default name", () => {
    const s = deriveDialogState({ ...base, preflight: pf() });
    expect(s.effectiveName).toBe("D-Photos");
    expect(s.canCreate).toBe(true);
    expect(s.nameError).toBeNull();
    expect(s.existsError).toBeNull();
    expect(s.folderError).toBeNull();
    expect(s.rootError).toBeNull();
    expect(s.sameRootWarning).toBeNull();
  });

  it("treats a preflight for a different name as stale (still checking)", () => {
    const s = deriveDialogState({ ...base, fileName: "other", preflight: pf() });
    expect(s.effectiveName).toBe("other");
    expect(s.checking).toBe(true);
    expect(s.canCreate).toBe(false);
  });

  it("reports a bad typed name immediately, without waiting for the server", () => {
    const s = deriveDialogState({ ...base, fileName: "a/b", preflight: null });
    expect(s.nameError).toMatch(/separator/i);
    expect(s.canCreate).toBe(false);
  });

  it("strips a typed .cdex before comparing with the preflight", () => {
    const s = deriveDialogState({ ...base, fileName: "D-Photos.cdex", preflight: pf() });
    expect(s.effectiveName).toBe("D-Photos");
    expect(s.canCreate).toBe(true);
  });

  it("shows the server's name error when the client rules pass", () => {
    const s = deriveDialogState({ ...base, preflight: pf({ fileNameError: "nope" }) });
    expect(s.nameError).toBe("nope");
    expect(s.canCreate).toBe(false);
  });

  it("errors on an existing file and offers Rescan existing only when it is loaded", () => {
    const loaded = deriveDialogState({ ...base, preflight: pf({ exists: true, existingCatalogId: 2 }) });
    expect(loaded.existsError).toContain("D-Photos.cdex");
    expect(loaded.canRescanExisting).toBe(true);
    expect(loaded.existingCatalogId).toBe(2);
    expect(loaded.canCreate).toBe(false);

    const notLoaded = deriveDialogState({ ...base, preflight: pf({ exists: true, existingCatalogId: null }) });
    expect(notLoaded.existsError).toMatch(/not (a )?loaded|isn't loaded|not currently loaded/i);
    expect(notLoaded.canRescanExisting).toBe(false);
    expect(notLoaded.canCreate).toBe(false);
  });

  it("disables Create when the folder is not writable or unknown", () => {
    const unwritable = deriveDialogState({ ...base, preflight: pf({ writable: false }) });
    expect(unwritable.folderError).toContain("C:\\catalogs");
    expect(unwritable.canCreate).toBe(false);

    const none = deriveDialogState({ ...base, preflight: pf({ catalogDir: null, writable: false, targetPath: null }) });
    expect(none.folderError).toMatch(/folder/i);
    expect(none.canCreate).toBe(false);
  });

  it("disables Create when the root can't be reached", () => {
    const s = deriveDialogState({ ...base, preflight: pf({ rootAccessible: false }) });
    expect(s.rootError).toContain("D:\\Photos");
    expect(s.canCreate).toBe(false);
  });

  it("warns, but allows, when loaded catalogs have the same root", () => {
    const one = deriveDialogState({ ...base, preflight: pf({ sameRootCatalogIds: [3] }) });
    expect(one.sameRootWarning).toMatch(/1 loaded catalog/);
    expect(one.canCreate).toBe(true);

    const two = deriveDialogState({ ...base, preflight: pf({ sameRootCatalogIds: [0, 3] }) });
    expect(two.sameRootWarning).toMatch(/2 loaded catalogs/);
    expect(two.canCreate).toBe(true);
  });

  it("disables Create while catalogs are loading", () => {
    const s = deriveDialogState({ ...base, preflight: pf({ busy: true }) });
    expect(s.busyError).toBeTruthy();
    expect(s.canCreate).toBe(false);
  });

  it("surfaces a failed preflight call and keeps Create disabled", () => {
    const s = deriveDialogState({ ...base, preflight: null, preflightError: "network down" });
    expect(s.checking).toBe(false);
    expect(s.preflightError).toBe("network down");
    expect(s.canCreate).toBe(false);
  });

  it("falls back to the typed name when the default is empty and nothing was typed", () => {
    const s = deriveDialogState({ ...base, preflight: pf({ defaultFileName: "", fileName: "" }) });
    expect(s.effectiveName).toBe("");
    expect(s.nameError).toBeTruthy();
    expect(s.canCreate).toBe(false);
  });
});
