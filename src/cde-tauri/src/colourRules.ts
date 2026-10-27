import type { CSSProperties } from "react";

/**
 * User-defined colour rules (issue #67). Plain text, one rule per line: `pattern` + whitespace +
 * `style`. `#` lines are comments. First matching line wins. This module is pure (no DOM / React
 * runtime) so it can be unit-tested in Node.
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
};

const FOLDER_PATTERN = "[folder]";
const FORBIDDEN_VALUE = /url\s*\(|expression\s*\(|@import|[{}<>]/i;

export function parseStyle(text: string): { style: CSSProperties; problems: string[] } {
  const style: Record<string, string> = {};
  const problems: string[] = [];
  const t = text.trim();

  if (!t.includes(":")) {
    // Bare colour (also var(--x), rgb(...), #hex) = `color: <value>`.
    if (FORBIDDEN_VALUE.test(t)) problems.push("invalid value for `color`");
    else if (t) style.color = t;
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
    } else if (FORBIDDEN_VALUE.test(value)) {
      problems.push(`invalid value for \`${prop}\``);
    } else {
      style[key] = value;
    }
  }
  return { style: style as CSSProperties, problems };
}

export function parseColourRules(text: string): ParsedRules {
  const rules: ColourRule[] = [];
  const problems: RuleProblem[] = [];
  const lines = text.split(/\r\n|\r|\n/);

  lines.forEach((raw, i) => {
    const line = i + 1;
    const trimmed = raw.trim();
    if (!trimmed || trimmed.startsWith("#")) return;

    const m = /^(\S+)\s*(.*)$/.exec(trimmed)!;
    const pattern = m[1];
    const styleText = m[2].trim();
    if (!styleText) {
      problems.push({ line, message: "missing style" });
      return;
    }

    const isFolder = pattern.toLowerCase() === FOLDER_PATTERN;
    let regex: RegExp | null = null;
    if (!isFolder) {
      try {
        regex = new RegExp(pattern, "i");
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
