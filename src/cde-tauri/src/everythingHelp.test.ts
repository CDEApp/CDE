import { describe, expect, it } from "vitest";
import { EVERYTHING_HELP, EVERYTHING_HELP_NOT_SUPPORTED } from "./everythingHelp";

const syntaxes = () => EVERYTHING_HELP.flatMap((s) => s.rows.map((r) => r.syntax));

describe("EVERYTHING_HELP", () => {
  it("has the four sections in order", () => {
    expect(EVERYTHING_HELP.map((s) => s.title)).toEqual(["Operators", "Modifiers", "Filters", "Examples"]);
  });

  it("lists every supported operator, modifier and filter", () => {
    const all = syntaxes().join("\n");
    for (const item of ["a b", "|", "!", "<", "\"", "path:", "no-path:", "file:", "folder:"]) expect(all).toContain(item);
    expect(EVERYTHING_HELP[0].rows[0].meaning).toContain("space is AND");
  });

  it("gives every row a meaning", () => {
    for (const s of EVERYTHING_HELP) for (const r of s.rows) expect(r.meaning.trim()).not.toBe("");
  });

  it("includes the worked examples from the feature request", () => {
    const all = syntaxes();
    expect(all).toContain("do win");
    expect(all).toContain("folder:music");
    expect(all).toContain("file: .mp3");
  });

  it("says what is not supported yet and which controls are ignored", () => {
    const text = EVERYTHING_HELP_NOT_SUPPORTED.join(" ");
    for (const item of ["wildcards", "ext:", "\:", "?path:", "Include path", "Files/Folders"]) expect(text).toContain(item);
  });
});
