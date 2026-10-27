import { describe, expect, it } from "vitest";
import { createRuleResolver, parseColourRules, parseStyle } from "./colourRules";

describe("parseStyle", () => {
  it("treats a bare colour as color", () => {
    expect(parseStyle("red")).toEqual({ style: { color: "red" }, problems: [] });
    expect(parseStyle("#2a9d8f").style).toEqual({ color: "#2a9d8f" });
  });

  it("allows var() and function colours as bare values", () => {
    expect(parseStyle("var(--dir)").style).toEqual({ color: "var(--dir)" });
    expect(parseStyle("rgb(1, 2, 3)").style).toEqual({ color: "rgb(1, 2, 3)" });
  });

  it("parses declarations into camelCase keys", () => {
    const r = parseStyle(
      "color: grey; font-style: italic; background-color: #111; font-weight: bold; text-decoration: underline; opacity: 0.5",
    );
    expect(r.problems).toEqual([]);
    expect(r.style).toEqual({
      color: "grey",
      fontStyle: "italic",
      backgroundColor: "#111",
      fontWeight: "bold",
      textDecoration: "underline",
      opacity: "0.5",
    });
  });

  it("flags and ignores disallowed properties but keeps valid ones", () => {
    const r = parseStyle("color: red; margin: 10px");
    expect(r.style).toEqual({ color: "red" });
    expect(r.problems).toEqual(["unknown property `margin`"]);
  });

  it("is case-insensitive for property names and tolerates a trailing semicolon", () => {
    expect(parseStyle("COLOR: Red;").style).toEqual({ color: "Red" });
  });

  it("flags a declaration with no value", () => {
    const r = parseStyle("color:");
    expect(r.style).toEqual({});
    expect(r.problems).toEqual(["missing value for `color`"]);
  });

  it("rejects url() and expression() values", () => {
    expect(parseStyle("background-color: url(http://x/y.png)").problems).toEqual([
      "invalid value for `background-color`",
    ]);
    expect(parseStyle("url(http://x)").style).toEqual({});
  });
});

describe("parseColourRules", () => {
  it("skips comments and blank lines and records line numbers", () => {
    const { rules, problems } = parseColourRules("# header\n\n\\.sys$   red\n  # indented comment\n^a   blue\n");
    expect(problems).toEqual([]);
    expect(rules.map((r) => [r.line, r.pattern])).toEqual([[3, "\\.sys$"], [5, "^a"]]);
  });

  it("handles CRLF line endings", () => {
    const { rules } = parseColourRules("a red\r\nb blue\r\n");
    expect(rules.map((r) => r.pattern)).toEqual(["a", "b"]);
  });

  it("splits the pattern at the first whitespace; \\s encodes a space", () => {
    const { rules } = parseColourRules("my\\sfile   color: red;  font-style: italic");
    expect(rules).toHaveLength(1);
    expect(rules[0].regex!.test("My File.txt")).toBe(true);
    expect(rules[0].style).toEqual({ color: "red", fontStyle: "italic" });
  });

  it("compiles regexes case-insensitively", () => {
    const { rules } = parseColourRules("\\.SYS$ red");
    expect(rules[0].regex!.test("driver.sys")).toBe(true);
  });

  it("reports an invalid regex with its line number and skips the rule", () => {
    const { rules, problems } = parseColourRules("ok red\n(unclosed red\nalso blue");
    expect(rules.map((r) => r.line)).toEqual([1, 3]);
    expect(problems).toEqual([{ line: 2, message: "invalid regex" }]);
  });

  it("reports a missing style", () => {
    const { rules, problems } = parseColourRules("lonely");
    expect(rules).toEqual([]);
    expect(problems).toEqual([{ line: 1, message: "missing style" }]);
  });

  it("reports disallowed properties with the line, and drops a rule left with no styles", () => {
    const { rules, problems } = parseColourRules("a color: red; margin: 1px\nb margin: 1px");
    expect(rules.map((r) => r.line)).toEqual([1]);
    expect(problems).toEqual([
      { line: 1, message: "unknown property `margin`" },
      { line: 2, message: "unknown property `margin`" },
      { line: 2, message: "no valid style" },
    ]);
  });

  it("marks [folder] as a pseudo-pattern with no regex", () => {
    const { rules } = parseColourRules("[folder]   var(--dir)");
    expect(rules[0].isFolder).toBe(true);
    expect(rules[0].regex).toBeNull();
    expect(rules[0].style).toEqual({ color: "var(--dir)" });
  });

  it("keeps the style text for previews", () => {
    const { rules } = parseColourRules("x   color: red; opacity: 0.5");
    expect(rules[0].styleText).toBe("color: red; opacity: 0.5");
  });
});

describe("createRuleResolver", () => {
  const text = "[folder] var(--dir)\n\\.sys$ red\n\\.(jpg|png)$ #2a9d8f\n^~\\$|\\.tmp$ color: grey; font-style: italic";
  const { rules } = parseColourRules(text);

  it("matches files by name", () => {
    const resolve = createRuleResolver(rules);
    expect(resolve("driver.SYS", false)).toEqual({ color: "red" });
    expect(resolve("pic.png", false)).toEqual({ color: "#2a9d8f" });
    expect(resolve("x.tmp", false)).toEqual({ color: "grey", fontStyle: "italic" });
    expect(resolve("plain.txt", false)).toBeUndefined();
  });

  it("[folder] matches every directory and no file", () => {
    const resolve = createRuleResolver(rules);
    expect(resolve("anything", true)).toEqual({ color: "var(--dir)" });
    expect(resolve("anything", false)).toBeUndefined();
  });

  it("first matching line wins, top to bottom", () => {
    const r = parseColourRules("\\.sys$ red\n\\.sys$ blue\n[folder] green\n^x green2").rules;
    const resolve = createRuleResolver(r);
    expect(resolve("a.sys", false)).toEqual({ color: "red" });
    // an earlier regex rule wins over a later [folder] rule
    expect(resolve("dir.sys", true)).toEqual({ color: "red" });
    expect(resolve("xdir", true)).toEqual({ color: "green" });
  });

  it("name rules apply to folders too", () => {
    const resolve = createRuleResolver(parseColourRules("^node_modules$ grey").rules);
    expect(resolve("node_modules", true)).toEqual({ color: "grey" });
  });

  it("skipFolder ignores [folder] rules only", () => {
    const resolve = createRuleResolver(rules, { skipFolder: true });
    expect(resolve("anything", true)).toBeUndefined();
    expect(resolve("driver.sys", true)).toEqual({ color: "red" });
  });

  it("caches per name and returns the same object", () => {
    const resolve = createRuleResolver(rules);
    expect(resolve("a.sys", false)).toBe(resolve("a.sys", false));
  });

  it("caches misses without re-running regexes", () => {
    let calls = 0;
    const regex = { test: () => (calls++, false) } as unknown as RegExp;
    const resolve = createRuleResolver([
      { line: 1, pattern: "x", isFolder: false, regex, style: { color: "red" }, styleText: "red" },
    ]);
    expect(resolve("n", false)).toBeUndefined();
    expect(resolve("n", false)).toBeUndefined();
    expect(calls).toBe(1);
  });

  it("a new resolver (rules changed) does not see old cached results", () => {
    const a = createRuleResolver(parseColourRules("x red").rules);
    expect(a("x", false)).toEqual({ color: "red" });
    const b = createRuleResolver(parseColourRules("x blue").rules);
    expect(b("x", false)).toEqual({ color: "blue" });
  });
});
