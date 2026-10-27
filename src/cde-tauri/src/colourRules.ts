import type { CSSProperties } from "react";

/**
 * User-defined colour rules (issue #67). Plain text, one rule per line: `pattern` + whitespace +
 * `style`. `#` lines are comments. First matching line wins. This module is pure (no DOM / React
 * runtime) so it can be unit-tested in Node.
 *
 * Gradient text (#105): `background` (gradients allowed, `url(` is not), `background-clip` and
 * `-webkit-background-clip` are accepted, plus `-webkit-text-fill-color` (the robust transparent
 * fill on WebKit webviews). Example:
 *   \.mp4$  background: linear-gradient(90deg, #3b82f6, #a855f7); background-clip: text; -webkit-background-clip: text; color: transparent
 * Gradient text needs `background-clip: text` (and the `-webkit-` form) plus a transparent text
 * colour (`color: transparent` or `-webkit-text-fill-color: transparent`). The order of the
 * declarations does not matter: the parser emits `background` first, as the shorthand would reset
 * the clip otherwise. With a clip present, the background properties and `color` are applied to the
 * Name label only (see `splitRowAndNameStyle`), so the gradient is clipped to the name text, not
 * stretched across the Size/Modified cells.
 */

export interface ColourRule {
  /** 1-based line number in the source text. */
  line: number;
  pattern: string;
  /** True for the reserved `[folder]` pseudo-pattern (matches every directory). */
  isFolder: boolean;
  /** Compiled case-insensitive regex; null for `[folder]`. */
  regex: RegExp | null;
  style: CSSProperties;
  /** Original style text, for previews. */
  styleText: string;
}

export interface RuleProblem {
  line: number;
  message: string;
}

export interface ParsedRules {
  rules: ColourRule[];
  problems: RuleProblem[];
}

/** CSS property (kebab-case) -> React style key. Everything else is flagged and ignored. */
const ALLOWED: Record<string, keyof CSSProperties> = {
  color: "color",
  "background-color": "backgroundColor",
  "font-weight": "fontWeight",
  "font-style": "fontStyle",
  "text-decoration": "textDecoration",
  opacity: "opacity",
  background: "background",
  "background-clip": "backgroundClip",
  "-webkit-background-clip": "WebkitBackgroundClip",
  "-webkit-text-fill-color": "WebkitTextFillColor",
};

const FOLDER_PATTERN = "[folder]";
const FORBIDDEN_VALUE = /url\s*\(|expression\s*\(|@import|[{}<>]/i;

/**
 * Whether the webview accepts `value` for `prop`. A value it would ignore must be flagged, or the
 * rule would still win first-match and silently shadow later rules. Unchecked where there is no
 * CSS.supports (plain Node, i.e. unit tests).
 */
function isValidValue(prop: string, value: string): boolean {
  if (FORBIDDEN_VALUE.test(value)) return false;
  return typeof CSS === "undefined" || typeof CSS.supports !== "function" || CSS.supports(prop, value);
}

export function parseStyle(text: string): { style: CSSProperties; problems: string[] } {
  const style: Record<string, string> = {};
  const problems: string[] = [];
  const t = text.trim();

  if (!t.includes(":")) {
    // Bare colour (also var(--x), rgb(...), #hex) = `color: <value>`.
    if (!t) return { style, problems };
    if (isValidValue("color", t)) style.color = t;
    else problems.push("invalid value for `color`");
    return { style, problems };
  }

  for (const decl of t.split(";")) {
    const d = decl.trim();
    if (!d) continue;
    const colon = d.indexOf(":");
    const prop = (colon < 0 ? d : d.slice(0, colon)).trim().toLowerCase();
    const value = colon < 0 ? "" : d.slice(colon + 1).trim();
    const key = ALLOWED[prop];
    if (!key) {
      problems.push(`unknown property \`${prop}\``);
    } else if (!value) {
      problems.push(`missing value for \`${prop}\``);
    } else if (!isValidValue(prop, value)) {
      problems.push(`invalid value for \`${prop}\``);
    } else {
      style[key] = value;
    }
  }
  // React warns on shorthand + longhand together; the shorthand wins.
  if (style.background !== undefined && style.backgroundColor !== undefined) {
    delete style.backgroundColor;
    problems.push("background and background-color both set; background-color ignored");
  }
  // `background` resets background-clip, and React assigns keys in object order, so put the
  // shorthand first: the user's longhands then survive whatever order they were written in.
  if (style.background !== undefined) {
    const { background, ...rest } = style;
    return { style: { background, ...rest } as CSSProperties, problems };
  }
  return { style: style as CSSProperties, problems };
}

/** A rule with any of these is gradient text; it (and its colour) goes to the Name label only. */
const CLIP_KEYS = ["backgroundClip", "WebkitBackgroundClip", "WebkitTextFillColor"] as const;
const NAME_ONLY_KEYS = new Set<string>(["background", "backgroundClip", "WebkitBackgroundClip", "WebkitTextFillColor", "color"]);

const splitCache = new WeakMap<object, { row: CSSProperties | undefined; name: CSSProperties | undefined }>();

/**
 * Splits a rule style into what goes on the row/cell and what goes on the Name label. If the rule
 * has any of `background-clip`, `-webkit-background-clip` or `-webkit-text-fill-color` (gradient
 * text), those plus `background` and `color` go to the name, whatever the colour value (so
 * `Transparent`, `rgba(0,0,0,0)`, `#0000` cannot blank the Size/Modified cells). Otherwise the
 * whole style stays on the row. `background-color` and everything else always stay on the row.
 * Results are cached per style object (rule styles are stable), so rows don't allocate.
 */
export function splitRowAndNameStyle(style: CSSProperties | undefined): {
  row: CSSProperties | undefined;
  name: CSSProperties | undefined;
} {
  if (!style) return { row: undefined, name: undefined };
  const cached = splitCache.get(style);
  if (cached) return cached;
  const row: Record<string, unknown> = {};
  const name: Record<string, unknown> = {};
  const gradientText = CLIP_KEYS.some((k) => (style as Record<string, unknown>)[k] !== undefined);
  for (const [k, v] of Object.entries(style)) {
    if (gradientText && NAME_ONLY_KEYS.has(k)) name[k] = v;
    else row[k] = v;
  }
  const result = {
    row: Object.keys(row).length ? (row as CSSProperties) : undefined,
    name: Object.keys(name).length ? (name as CSSProperties) : undefined,
  };
  splitCache.set(style, result);
  return result;
}

export function parseColourRules(text: string): ParsedRules {
  const rules: ColourRule[] = [];
  const problems: RuleProblem[] = [];
  const lines = text.split(/\r\n|\r|\n/);

  lines.forEach((raw, i) => {
    const line = i + 1;
    const trimmed = raw.trim();
    if (!trimmed || trimmed.startsWith("#")) return;

    // Pattern is the first whitespace-delimited token; the rest is the style text.
    const split = trimmed.search(/\s/);
    const pattern = split < 0 ? trimmed : trimmed.slice(0, split);
    const styleText = split < 0 ? "" : trimmed.slice(split).trim();
    if (!styleText) {
      problems.push({ line, message: "missing style" });
      return;
    }

    const isFolder = pattern.toLowerCase() === FOLDER_PATTERN;
    let regex: RegExp | null = null;
    if (!isFolder) {
      try {
        regex = new RegExp(pattern, "i"); // NOSONAR S2631: regexes are user-written (accepted risk, #67); docs and default rules warn about it
      } catch {
        problems.push({ line, message: "invalid regex" });
        return;
      }
    }

    const parsed = parseStyle(styleText);
    for (const message of parsed.problems) problems.push({ line, message });
    if (Object.keys(parsed.style).length === 0) {
      problems.push({ line, message: "no valid style" });
      return;
    }
    rules.push({ line, pattern, isFolder, regex, style: parsed.style, styleText });
  });

  return { rules, problems };
}

export type StyleResolver = (name: string, isDirectory: boolean) => CSSProperties | undefined;

/** Bound on cached names so scrolling through millions of results can't grow without limit. */
const CACHE_LIMIT = 20000;

/**
 * Builds a resolver for a rule set: first matching rule wins, top to bottom. Results (including
 * misses) are cached per name; a rule change means building a new resolver, which drops the cache.
 * With `skipFolder`, `[folder]` rules are ignored (the tree is all folders and keeps its look).
 */
export function createRuleResolver(rules: ColourRule[], opts?: { skipFolder?: boolean }): StyleResolver {
  const active = opts?.skipFolder ? rules.filter((r) => !r.isFolder) : rules;
  let cache = new Map<string, CSSProperties | undefined>();

  return (name, isDirectory) => {
    const key = (isDirectory ? "d" : "f") + name;
    if (cache.has(key)) return cache.get(key);

    let found: CSSProperties | undefined;
    for (const r of active) {
      if (r.isFolder ? isDirectory : r.regex!.test(name)) {
        found = r.style;
        break;
      }
    }
    if (cache.size >= CACHE_LIMIT) cache = new Map();
    cache.set(key, found);
    return found;
  };
}
