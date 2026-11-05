import { describe, expect, it } from "vitest";
import { ApiError } from "./api/client";
import { EVERYTHING_SYNTAX_HINT, includePathControl, kindControls, searchErrorMessage, searchFromHereQuery } from "./everythingSearch";

describe("searchErrorMessage", () => {
  it("shows a refused query as an invalid query with the server message", () => {
    const e = new ApiError(400, "Missing a term after '|' (at position 2)", "x");
    expect(searchErrorMessage(e)).toBe("Invalid query: Missing a term after '|' (at position 2)");
  });

  it("falls back to the error message when a 400 has no body", () => {
    expect(searchErrorMessage(new ApiError(400, "", "request failed: 400"))).toBe("Invalid query: request failed: 400");
  });

  it("keeps other failures as errors", () => {
    expect(searchErrorMessage(new ApiError(500, "boom", "boom"))).toBe("Error: boom");
    expect(searchErrorMessage(new Error("network down"))).toBe("Error: network down");
  });
});

describe("EVERYTHING_SYNTAX_HINT", () => {
  it("names every iteration-1 operator", () => {
    for (const op of ["space=AND", "|=OR", "!=NOT", "<>=group", '"phrase"', "path:", "no-path:", "file:", "folder:"]) expect(EVERYTHING_SYNTAX_HINT).toContain(op);
  });
});

describe("kindControls", () => {
  it("shows the saved Files/Folders choice and keeps the boxes usable in plain search", () => {
    expect(kindControls(false, true, false)).toEqual({ files: true, folders: false, disabled: false });
    expect(kindControls(false, false, true)).toEqual({ files: false, folders: true, disabled: false });
  });

  it("shows both ticked and greyed out in Everything mode, whatever was saved", () => {
    expect(kindControls(true, false, false)).toEqual({ files: true, folders: true, disabled: true });
    expect(kindControls(true, true, false)).toEqual({ files: true, folders: true, disabled: true });
  });

  it("gives the saved choice back when Everything is switched off", () => {
    const saved = { files: false, folders: true };
    expect(kindControls(true, saved.files, saved.folders).disabled).toBe(true);
    expect(kindControls(false, saved.files, saved.folders)).toEqual({ ...saved, disabled: false });
  });
});

describe("includePathControl", () => {
  it("shows the saved Include path choice and keeps the box usable in plain search", () => {
    expect(includePathControl(false, true)).toEqual({ checked: true, disabled: false });
    expect(includePathControl(false, false)).toEqual({ checked: false, disabled: false });
  });

  it("shows it unticked and greyed out in Everything mode, whatever was saved", () => {
    expect(includePathControl(true, true)).toEqual({ checked: false, disabled: true });
    expect(includePathControl(true, false)).toEqual({ checked: false, disabled: true });
  });

  it("gives the saved choice back when Everything is switched off", () => {
    expect(includePathControl(false, true)).toEqual({ checked: true, disabled: false });
  });
});

describe("searchFromHereQuery", () => {
  it("scopes to the catalog and the folder's contents, ready for the search term", () => {
    expect(searchFromHereQuery("C:/Users/jo", "D:/cats/C-Win-C__Users.cdex")).toBe("catalog:C-Win-C__Users path:C:/Users/jo/ ");
    expect(searchFromHereQuery("C:\\Users\\jo", "C-Win.cdex")).toBe("catalog:C-Win path:C:\\Users\\jo\\ ");
  });

  it("quotes a term that holds a space or an operator character", () => {
    expect(searchFromHereQuery("C:\\My Docs", "my cats.cdex")).toBe('catalog:"my cats" path:"C:\\My Docs\\" ');
    expect(searchFromHereQuery("/a|b", "x.cdex")).toBe('catalog:x path:"/a|b/" ');
  });

  it("keeps a root's own separator and drops the catalog term when the catalog is unknown", () => {
    expect(searchFromHereQuery("C:\\", "")).toBe("path:C:\\ ");
    expect(searchFromHereQuery("/", "")).toBe("path:/ ");
  });
});
