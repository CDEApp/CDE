import { describe, expect, it } from "vitest";
import { ApiError } from "./api/client";
import { EVERYTHING_SYNTAX_HINT, searchErrorMessage } from "./everythingSearch";

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
    for (const op of ["space=AND", "|=OR", "!=NOT", "<>=group", '"phrase"', "path:", "no-path:"]) expect(EVERYTHING_SYNTAX_HINT).toContain(op);
  });
});
