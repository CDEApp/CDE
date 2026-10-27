import { afterEach, describe, expect, it, vi } from "vitest";
import { createRuleResolver, parseColourRules, parseStyle, splitRowAndNameStyle } from "./colourRules";

describe("parseStyle with the browser's CSS.supports", () => {
  // Node has no CSS global; stand in for the webview's, accepting only known values.
  const known = new Set(["color:red", "color:var(--dir)", "font-weight:bold"]);
  afterEach(() => vi.unstubAllGlobals());

  it("flags values the browser would ignore, so they can't silently shadow later rules", () => {
    vi.stubGlobal("CSS", { supports: (p: string, v: string) => known.has(`${p}:${v}`) });

    expect(parseStyle("notacolour")).toEqual({ style: {}, problems: ["invalid value for `color`"] });
    expect(parseStyle("color: red; font-weight: heavy")).toEqual({
      style: { color: "red" },
      problems: ["invalid value for `font-weight`"],
    });
    expect(parseStyle("var(--dir)").style).toEqual({ color: "var(--dir)" });
  });

  it("skips a rule left with no valid style", () => {
    vi.stubGlobal("CSS", { supports: (p: string, v: string) => known.has(`${p}:${v}`) });

    const parsed = parseColourRules("\\.sys$  notacolour\n\\.sys$  red");
    expect(parsed.rules.map((r) => r.line)).toEqual([2]);
    expect(parsed.problems.map((p) => p.line)).toContain(1);
  });
});

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

describe("background / gradient text (#105)", () => {
  const GRAD = "linear-gradient(90deg, #3b82f6, #a855f7)";
  const supports = (_p: string, v: string) => !v.includes("nonsense");
  afterEach(() => vi.unstubAllGlobals());

  it("parses the issue's gradient-text rule with no problems", () => {
    vi.stubGlobal("CSS", { supports });
    const parsed = parseColourRules(
      `\\.mp4$   background: ${GRAD}; background-clip: text; -webkit-background-clip: text; color: transparent`,
    );
    expect(parsed.problems).toEqual([]);
    expect(parsed.rules[0].style).toEqual({
      background: GRAD,
      backgroundClip: "text",
      WebkitBackgroundClip: "text",
      color: "transparent",
    });
  });

  it("maps -webkit-text-fill-color", () => {
    vi.stubGlobal("CSS", { supports });
    expect(parseStyle("-webkit-text-fill-color: transparent")).toEqual({
      style: { WebkitTextFillColor: "transparent" },
      problems: [],
    });
  });

  it("flags invalid gradient values", () => {
    vi.stubGlobal("CSS", { supports });
    expect(parseStyle("background: linear-gradient(nonsense)")).toEqual({
      style: {},
      problems: ["invalid value for `background`"],
    });
  });

  it("flags url() even inside a gradient list", () => {
    vi.stubGlobal("CSS", { supports });
    const r = parseStyle(`background: ${GRAD}, url(x)`);
    expect(r.style).toEqual({});
    expect(r.problems).toEqual(["invalid value for `background`"]);
  });

  it("flags background + background-color on one line and keeps background", () => {
    vi.stubGlobal("CSS", { supports });
    const msg = "background and background-color both set; background-color ignored";
    expect(parseStyle("background-color: red; background: blue")).toEqual({ style: { background: "blue" }, problems: [msg] });
    expect(parseStyle("background: blue; background-color: red")).toEqual({ style: { background: "blue" }, problems: [msg] });
  });
});

describe("splitRowAndNameStyle", () => {
  it("sends clip, text fill and the colour to the name; the rest stays on the row", () => {
    const { row, name } = splitRowAndNameStyle({
      background: "linear-gradient(red, blue)",
      backgroundClip: "text",
      WebkitBackgroundClip: "text",
      WebkitTextFillColor: "transparent",
      fontWeight: "bold",
      color: "green",
    });
    expect(name).toEqual({
      background: "linear-gradient(red, blue)",
      backgroundClip: "text",
      WebkitBackgroundClip: "text",
      WebkitTextFillColor: "transparent",
      color: "green",
    });
    expect(row).toEqual({ fontWeight: "bold" });
  });

  it.each(["transparent", "Transparent", "rgba(0,0,0,0)", "#0000", "#00000000"])(
    "moves color %s to the name so other cells stay visible",
    (color) => {
      const { row, name } = splitRowAndNameStyle({ background: "red", backgroundClip: "text", color, opacity: 0.5 });
      expect(name).toEqual({ background: "red", backgroundClip: "text", color });
      expect(row).toEqual({ opacity: 0.5 });
    },
  );

  it("a text-fill-only rule moves its colour too", () => {
    const { row, name } = splitRowAndNameStyle({ WebkitTextFillColor: "transparent", color: "#0000" });
    expect(name).toEqual({ WebkitTextFillColor: "transparent", color: "#0000" });
    expect(row).toBeUndefined();
  });

  it("without a clip or text fill, background and colour stay on the row", () => {
    const { row, name } = splitRowAndNameStyle({ background: "red", color: "green" });
    expect(name).toBeUndefined();
    expect(row).toEqual({ background: "red", color: "green" });
  });

  it("handles undefined", () => {
    expect(splitRowAndNameStyle(undefined)).toEqual({ row: undefined, name: undefined });
  });
});

describe("background shorthand order (#105)", () => {
  it("emits background before its longhands whatever order the user wrote", () => {
    const r = parseStyle(
      "background-clip: text; -webkit-background-clip: text; color: transparent; background: linear-gradient(red, blue)",
    );
    expect(r.problems).toEqual([]);
    expect(Object.keys(r.style)).toEqual(["background", "backgroundClip", "WebkitBackgroundClip", "color"]);
  });
});
