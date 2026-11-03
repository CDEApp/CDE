/** One line of the Search Syntax help: what to type and what it does. */
export interface HelpRow {
  syntax: string;
  meaning: string;
}

export interface HelpSection {
  title: string;
  rows: HelpRow[];
}

/** The text of Help > Search Syntax: the Everything-style search operators, modifiers and filters. */
export const EVERYTHING_HELP: HelpSection[] = [
  {
    title: "Operators",
    rows: [
      { syntax: "a b", meaning: "space is AND: the name has both a and b" },
      { syntax: "a | b", meaning: "OR: the name has a or b (a space binds tighter than |)" },
      { syntax: "!a", meaning: "NOT: the name does not have a" },
      { syntax: "<a | b> c", meaning: "group with < >: the name has a or b, and also c" },
      { syntax: "\"a b\"", meaning: "phrase: the exact text a b, and operators inside the quotes are plain text" },
    ],
  },
  {
    title: "Modifiers",
    rows: [
      { syntax: "path:term", meaning: "match the full path, not just the name. Directly in front of one word, phrase or group, with no space" },
      { syntax: "no-path:term", meaning: "match the name only" },
      { syntax: "regex:term", meaning: "the term is a regular expression, matched anywhere in the text, ignoring case. Quote one with a space or |: regex:\"^a|b$\"" },
      { syntax: "path:regex:term", meaning: "modifiers chain: a regular expression on the full path" },
      { syntax: "path:<a | b>", meaning: "a modifier can go in front of a group; the innermost modifier wins" },
      { syntax: "!path:term", meaning: "put ! in front of the modifier to negate it" },
    ],
  },
  {
    title: "Filters",
    rows: [
      { syntax: "file:", meaning: "files only" },
      { syntax: "folder:", meaning: "folders only" },
      { syntax: "!file:", meaning: "NOT a file, so folders" },
      { syntax: "<file: a> | <folder: b>", meaning: "files with a, or folders with b" },
    ],
  },
  {
    title: "Wildcards",
    rows: [
      { syntax: "*", meaning: "zero or more characters, but not a backslash: *.txt is a name that ends in .txt" },
      { syntax: "**", meaning: "zero or more characters including backslashes: path:c:\\**\\*.txt" },
      { syntax: "?", meaning: "exactly one character, but not a backslash: ?at matches cat but not chat" },
    ],
  },
  {
    title: "Functions",
    rows: [
      { syntax: "size:>10mb", meaning: "size is greater than 10 MB. Also <, <=, >=, == (exactly), != (not exactly), and ranges 2mb..3mb or 2mb-3mb" },
      { syntax: "size:1kb", meaning: "a bare size matches its whole unit: 1024 to 2047 bytes. Units: b, kb, mb, gb, tb (binary, 1 kb is 1024 bytes)" },
      { syntax: "date-modified:today", meaning: "modified today. Also yesterday, tomorrow. dm: is short for date-modified:" },
      { syntax: "dm:2026-07-02", meaning: "modified on that day. Dates are YYYY[-MM[-DD[Thh[:mm[:ss[.sss]]]]]]: dm:2026 is the whole year" },
      { syntax: "dm:2026-07-02..2026-07-03", meaning: "modified in a range, including the whole last day" },
      { syntax: "dm:3days", meaning: "modified in the last 3 days. Also secs, mins, hours, weeks, months, years. dm:>3days is newer, dm:<3days is older" },
    ],
  },
  {
    title: "Examples",
    rows: [
      { syntax: "do win", meaning: "matches Windows (both do and win are in the name)" },
      { syntax: "folder:music", meaning: "folders whose name has music" },
      { syntax: "file: .mp3", meaning: "files whose name has .mp3" },
      { syntax: "path:system32 hosts", meaning: "hosts in a path that has system32" },
      { syntax: "txt | log !tmp", meaning: "names with txt, or with log but not tmp" },
      { syntax: "file: size:>100mb dm:1week", meaning: "files over 100 MB changed in the last week" },
      { syntax: "regex:^IMG_\\d+", meaning: "names that start with IMG_ and digits" },
    ],
  },
];

/** Closing notes for the Search Syntax help. */
export const EVERYTHING_HELP_NOT_SUPPORTED: string[] = [
  "Terms are case-insensitive substrings of the name. A term with * or ? must match the whole name (or the whole path after path:).",
  "Include path and the Files/Folders choice are ignored in this mode: use path:, file: and folder:.",
  "size: and date-modified: apply together with the Size and Date controls: an entry must pass both.",
  "Not supported yet: ext: and other functions, size:unknown, the \: alias and the global ?path: form.",
];
